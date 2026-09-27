using System;
using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Skills;
using Sanguo.Utils;

namespace Sanguo.AI
{
    /// <summary>Everything a decision maker receives. No access to hidden information.</summary>
    public sealed class AIDecisionContext
    {
        public AIDecisionContext(PlayerPerspective view, PendingRequest request, IRandom random)
        {
            View = view;
            Request = request;
            Random = random;
        }

        public PlayerPerspective View { get; }
        public PendingRequest Request { get; }
        public IRandom Random { get; }
        public IRulesQuery Rules => View.Rules;
    }

    /// <summary>
    /// Pluggable decision maker. The heuristic implementation below is the default; Monte Carlo,
    /// reinforcement learning or LLM-backed players implement the same interface.
    /// </summary>
    public interface IAIDecisionMaker
    {
        /// <summary>Returns a command answering <see cref="AIDecisionContext.Request"/> (PlayerId/RequestId are filled by the caller).</summary>
        GameCommand Decide(AIDecisionContext context);
    }

    /// <summary>
    /// Rule-based AI: AIController → generate all legal actions → score each with
    /// <see cref="AIActionEvaluator"/> → execute the best (or end the phase when nothing is worth it).
    /// </summary>
    public sealed class HeuristicAI : IAIDecisionMaker
    {
        private readonly AIActionEvaluator _evaluator;
        private readonly AISkillAdvisorRegistry _advisors;
        private readonly List<AICandidate> _candidates = new List<AICandidate>(64);

        public HeuristicAI(AIWeights weights = null, AISkillAdvisorRegistry advisors = null)
        {
            _evaluator = new AIActionEvaluator(weights);
            _advisors = advisors ?? AISkillAdvisorRegistry.CreateDefault();
        }

        public AIActionEvaluator Evaluator => _evaluator;

        public GameCommand Decide(AIDecisionContext c)
        {
            switch (c.Request)
            {
                case PlayActionRequest _:
                    return DecidePlay(c);
                case CardResponseRequest r:
                    return DecideResponse(c, r);
                case DiscardRequest d:
                    return DecideDiscard(c, d);
                case ChooseCardFromPlayerRequest pick:
                    return DecidePick(c, pick);
                case ChooseTargetsRequest t:
                    return DecideTargets(c, t);
                case ConfirmRequest confirm:
                {
                    var si = c.View.Self.FindSkill(confirm.SkillId);
                    bool yes = si == null || si.Skill.AIAcceptTrigger(c.View.Self);
                    return new RespondCommand { OptionIndex = yes ? 1 : 0 };
                }
                case ChooseCharacterRequest cc:
                    return DecideCharacter(c, cc);
                case ChooseOptionRequest _:
                    return new RespondCommand { OptionIndex = 0 };
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------ play phase

        public List<AICandidate> GeneratePlayCandidates(AIDecisionContext c)
        {
            _candidates.Clear();
            var view = c.View;
            var self = view.Self;
            var targets = ListPool<int>.Get();
            try
            {
                foreach (var card in self.HandCards)
                {
                    AddCardCandidates(c, card, card.Definition, null, targets);
                    foreach (var si in self.Skills)
                    {
                        if (si.Disabled) continue;
                        foreach (var asId in si.Skill.ConvertibleCardIds)
                        {
                            var asDef = view.GetCardDefinition(asId);
                            if (asDef == null || asId == card.CardId) continue;
                            AddCardCandidates(c, card, asDef, si.Skill.SkillId, targets);
                        }
                    }
                }
                foreach (var si in self.Skills)
                {
                    if (si.Disabled || !(si.Skill is ActiveSkillBase)) continue;
                    var advisor = _advisors.Find(si.Skill);
                    advisor?.AddCandidates(view, si, _evaluator, _candidates);
                }
            }
            finally
            {
                ListPool<int>.Release(targets);
            }
            // Never propose an illegal command: every candidate goes through the same rule checks
            // the server applies to human players.
            _candidates.RemoveAll(cand => !IsLegal(c, cand.Command));
            return _candidates;
        }

        private void AddCardCandidates(AIDecisionContext c, CardInstance card, CardBase def, string skillId, List<int> scratch)
        {
            var view = c.View;
            string asId = skillId != null ? def.CardId : null;
            if (!c.Rules.CanUseCard(view.ViewerId, card.InstanceId, skillId, asId).IsValid) return;
            if (!def.TargetRule.RequiresSelection)
            {
                var auto = new List<int>();
                AutoTargets(view, def, auto);
                float score = _evaluator.ScoreCardUse(view, def, auto);
                _candidates.Add(new AICandidate(Play(card, Array.Empty<int>(), skillId, asId), score, def.CardId));
                return;
            }
            scratch.Clear();
            c.Rules.GetLegalTargets(view.ViewerId, def, scratch);
            if (def.TargetRule.Kind == TargetKind.Single)
            {
                var one = new int[1];
                foreach (int t in scratch)
                {
                    one[0] = t;
                    float score = _evaluator.ScoreCardUse(view, def, one);
                    _candidates.Add(new AICandidate(Play(card, new[] { t }, skillId, asId), score, def.CardId + "->" + t));
                }
                return;
            }
            // Multiple targets: greedily take the best positive single-target scores.
            var scored = new List<KeyValuePair<int, float>>();
            var single = new int[1];
            foreach (int t in scratch)
            {
                single[0] = t;
                scored.Add(new KeyValuePair<int, float>(t, _evaluator.ScoreCardUse(view, def, single)));
            }
            scored.Sort((a, b) => b.Value.CompareTo(a.Value));
            var chosen = new List<int>();
            float total = 0f;
            int max = def.TargetRule.MaxTargets;
            foreach (var kv in scored)
            {
                if (chosen.Count >= max) break;
                if (kv.Value <= 0 && chosen.Count >= def.TargetRule.MinTargets) break;
                chosen.Add(kv.Key);
                total += kv.Value;
            }
            if (chosen.Count >= def.TargetRule.MinTargets)
                _candidates.Add(new AICandidate(Play(card, chosen.ToArray(), skillId, asId), total, def.CardId + "*"));
        }

        private static bool IsLegal(AIDecisionContext c, GameCommand cmd)
        {
            switch (cmd)
            {
                case PlayCardCommand play:
                    return c.Rules.ValidateCardUse(c.View.ViewerId, play).IsValid;
                case UseSkillCommand skill:
                    return c.Rules.ValidateSkillActivation(c.View.ViewerId, skill).IsValid;
                default:
                    return true;
            }
        }

        private static void AutoTargets(PlayerPerspective view, CardBase def, List<int> output)
        {
            var self = view.Self;
            switch (def.TargetRule.Kind)
            {
                case TargetKind.Self:
                    output.Add(self.PlayerId);
                    break;
                case TargetKind.AllOthers:
                case TargetKind.All:
                    foreach (var p in view.Players)
                    {
                        if (!p.Alive) continue;
                        if (def.TargetRule.Kind == TargetKind.AllOthers && p.PlayerId == self.PlayerId) continue;
                        if (def.TargetRule.Has(TargetFilter.Wounded) && !p.IsWounded) continue;
                        output.Add(p.PlayerId);
                    }
                    break;
            }
        }

        private static PlayCardCommand Play(CardInstance card, int[] targets, string skillId, string asId)
        {
            return new PlayCardCommand { CardInstanceId = card.InstanceId, TargetIds = targets, SkillId = skillId, AsCardId = asId };
        }

        private GameCommand DecidePlay(AIDecisionContext c)
        {
            var list = GeneratePlayCandidates(c);
            float bestScore = float.MinValue;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Score > bestScore) bestScore = list[i].Score;
            if (bestScore <= _evaluator.Weights.ActThreshold) return new EndTurnCommand();
            // Break (near) ties randomly; a deterministic order would make every bot gang up on
            // the lowest seat.
            const float epsilon = 0.01f;
            int ties = 0;
            GameCommand choice = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Score < bestScore - epsilon) continue;
                ties++;
                if (c.Random.Next(ties) == 0) choice = list[i].Command;
            }
            return choice;
        }

        // ------------------------------------------------------------------ responses

        private GameCommand DecideResponse(AIDecisionContext c, CardResponseRequest r)
        {
            var view = c.View;
            var self = view.Self;
            if (r.Purpose == "rescue")
            {
                bool wantsToSave = r.SubjectPlayerId == self.PlayerId || view.Hostility(r.SubjectPlayerId) < 0f;
                if (!wantsToSave) return new RespondCommand { Pass = true };
            }
            var ids = new List<int>();
            string skillId = null;
            foreach (var card in self.HandCards)
            {
                if (ids.Count >= r.Count) break;
                if (c.Rules.CanCardServeAs(self, card, r.RequiredCardId, null)) ids.Add(card.InstanceId);
            }
            if (ids.Count < r.Count)
            {
                // Try a conversion skill (all cards of one response must use the same skill).
                foreach (var si in self.Skills)
                {
                    if (si.Disabled) continue;
                    var conv = new List<int>();
                    foreach (var card in self.HandCards)
                    {
                        if (conv.Count >= r.Count) break;
                        if (c.Rules.CanCardServeAs(self, card, r.RequiredCardId, si.Skill.SkillId)) conv.Add(card.InstanceId);
                    }
                    if (conv.Count >= r.Count)
                    {
                        ids = conv;
                        skillId = si.Skill.SkillId;
                        break;
                    }
                }
            }
            if (ids.Count < r.Count) return new RespondCommand { Pass = true };
            return new RespondCommand { CardIds = ids.ToArray(), SkillId = skillId };
        }

        private GameCommand DecideDiscard(AIDecisionContext c, DiscardRequest d)
        {
            var view = c.View;
            var self = view.Self;
            var pool = new List<CardInstance>(self.HandCards);
            if (d.IncludeEquipment) self.Equipment.CopyTo(pool);
            pool.Sort((a, b) => _evaluator.KeepValue(view, a).CompareTo(_evaluator.KeepValue(view, b)));
            int n = Math.Min(d.Count, pool.Count);
            var ids = new int[n];
            for (int i = 0; i < n; i++) ids[i] = pool[i].InstanceId;
            return new RespondCommand { CardIds = ids };
        }

        private GameCommand DecidePick(AIDecisionContext c, ChooseCardFromPlayerRequest pick)
        {
            var view = c.View;
            var target = view.GetPlayer(pick.TargetId);
            float h = view.Hostility(pick.TargetId);
            bool hand = (pick.Zones & ZoneMask.Hand) != 0 && target.HandCount > 0;
            bool equip = (pick.Zones & ZoneMask.Equipment) != 0 && target.Equipment.Count > 0;
            bool judge = (pick.Zones & ZoneMask.Judge) != 0 && target.JudgeArea.Count > 0;

            if (h < 0 && judge) return new RespondCommand { PickZone = ZoneType.JudgeArea, PickIndex = 0 };
            if (h >= 0 && equip)
            {
                EquipSlot[] priority = { EquipSlot.Weapon, EquipSlot.DefensiveMount, EquipSlot.Armor, EquipSlot.OffensiveMount, EquipSlot.Treasure };
                foreach (var slot in priority)
                    if (target.Equipment.Get(slot) != null) return new RespondCommand { PickZone = ZoneType.Equipment, PickIndex = (int)slot };
            }
            if (hand) return new RespondCommand { PickZone = ZoneType.Hand, PickIndex = c.Random.Next(target.HandCount) };
            if (equip)
            {
                foreach (var card in target.Equipment.Cards)
                    return new RespondCommand { PickZone = ZoneType.Equipment, PickIndex = (int)EquipmentArea.SlotOf(card) };
            }
            return new RespondCommand { PickZone = ZoneType.JudgeArea, PickIndex = 0 };
        }

        private GameCommand DecideTargets(AIDecisionContext c, ChooseTargetsRequest t)
        {
            var view = c.View;
            var list = new List<int>(t.Candidates);
            bool harm = t.Purpose == "harm";
            list.Sort((a, b) =>
            {
                float ha = view.Hostility(a), hb = view.Hostility(b);
                int cmp = harm ? hb.CompareTo(ha) : ha.CompareTo(hb);
                return cmp != 0 ? cmp : view.GetPlayer(a).HandCount.CompareTo(view.GetPlayer(b).HandCount);
            });
            int n = Math.Max(t.Min, Math.Min(1, t.Max));
            n = Math.Min(n, list.Count);
            return new SelectTargetCommand { TargetIds = list.GetRange(0, n).ToArray() };
        }

        private GameCommand DecideCharacter(AIDecisionContext c, ChooseCharacterRequest cc)
        {
            int best = 0;
            int bestScore = int.MinValue;
            for (int i = 0; i < cc.CharacterIds.Count; i++)
            {
                if (!c.View.Content.Characters.TryGet(cc.CharacterIds[i], out var ch)) continue;
                int score = ch.MaxHp * 2 + ch.SkillIds.Count * 3;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return new RespondCommand { OptionIndex = best };
        }
    }
}
