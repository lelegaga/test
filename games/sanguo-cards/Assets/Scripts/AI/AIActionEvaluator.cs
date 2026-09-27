using System;
using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Effects;

namespace Sanguo.AI
{
    /// <summary>Tunable weights of the heuristic evaluator.</summary>
    public sealed class AIWeights
    {
        public float DamageValue = 10f;
        public float KillBonus = 22f;
        public float LowHpBonus = 4f;
        public float HealValue = 9f;
        public float CardValue = 3f;
        public float RemoveEnemyCard = 3.5f;
        public float RemoveEnemyEquipment = 5f;
        public float EquipValue = 6f;
        public float SkipPlayPhaseValue = 8f;
        /// <summary>Candidates must beat this score to be preferred over ending the play phase.</summary>
        public float ActThreshold = 0.5f;
        /// <summary>Healing at comfortable HP is worth less (keep heals for emergencies).</summary>
        public float ComfortableHealFactor = 0.35f;
    }

    /// <summary>A possible command with its score.</summary>
    public struct AICandidate
    {
        public GameCommand Command;
        public float Score;
        public string Reason;

        public AICandidate(GameCommand command, float score, string reason)
        {
            Command = command;
            Score = score;
            Reason = reason;
        }
    }

    /// <summary>
    /// Scores card uses by walking the card's effect list, so new cards built from existing effects
    /// are understood by the AI without extra code. Considers damage, heal and kill value, allies vs
    /// enemies (hostility), own HP and hand size.
    /// </summary>
    public sealed class AIActionEvaluator
    {
        private readonly Dictionary<string, float> _frequency = new Dictionary<string, float>();

        public AIActionEvaluator(AIWeights weights = null)
        {
            Weights = weights ?? new AIWeights();
        }

        public AIWeights Weights { get; }

        public float ScoreCardUse(PlayerPerspective view, CardBase def, IReadOnlyList<int> targets)
        {
            var self = view.Self;
            if (def is EquipmentCard eq) return ScoreEquip(view, eq);
            if (def.TargetRule.Kind == TargetKind.None) return ScoreEffects(view, def.Effects, self.PlayerId, self.PlayerId, def);
            if (def is TrickCard trick && trick.IsDelayed)
                return targets.Count > 0 ? view.Hostility(targets[0]) * Weights.SkipPlayPhaseValue * 0.75f : 0f;
            float total = 0f;
            foreach (int t in targets) total += ScoreEffects(view, def.Effects, self.PlayerId, t, def);
            return total;
        }

        public float ScoreEffects(PlayerPerspective view, IReadOnlyList<ICardEffect> effects, int sourceId, int targetId, CardBase def)
        {
            float total = 0f;
            if (effects == null) return total;
            for (int i = 0; i < effects.Count; i++) total += ScoreEffect(view, effects[i], sourceId, targetId, def);
            return total;
        }

        public float ScoreEffect(PlayerPerspective view, ICardEffect effect, int sourceId, int targetId, CardBase def)
        {
            switch (effect)
            {
                case DamageEffect d:
                    return ScoreDamage(view, d.Subject == EffectSubject.Source ? sourceId : targetId, d.Amount);
                case HealEffect h:
                    return ScoreHeal(view, h.Subject == EffectSubject.Source ? sourceId : targetId, h.Amount);
                case DrawCardEffect dc:
                    return -view.Hostility(dc.Subject == EffectSubject.Source ? sourceId : targetId) * dc.Count * Weights.CardValue;
                case DiscardEffect de:
                    return ScoreRemoval(view, targetId, de.Count, de.Chooser == EffectSubject.Source);
                case StealCardEffect _:
                    return ScoreRemoval(view, targetId, 1, true) + (view.GetPlayer(targetId).TotalCardCount > 0 ? Weights.CardValue : 0f);
                case RequireResponseEffect rr:
                {
                    float pFail = ProbabilityCannotRespond(view, targetId, rr.RequiredCardId, rr.Count);
                    float fail = ScoreEffects(view, rr.OnFail, sourceId, targetId, def);
                    float success = ScoreEffects(view, rr.OnSuccess, sourceId, targetId, def) + view.Hostility(targetId) * Weights.CardValue * 0.5f;
                    return pFail * fail + (1f - pFail) * success;
                }
                case DuelEffect du:
                {
                    int mine = CountUsableAs(view, du.RequiredCardId);
                    var target = view.GetPlayer(targetId);
                    float theirs = target.HandCount * Frequency(view, du.RequiredCardId);
                    float pWin = mine == 0 ? 0.25f : mine >= theirs + 1f ? 0.8f : 0.55f;
                    return pWin * ScoreDamage(view, targetId, du.Damage) + (1f - pWin) * ScoreDamage(view, sourceId, du.Damage);
                }
                case AOEEffect aoe:
                {
                    float total = 0f;
                    foreach (var p in view.Players)
                    {
                        if (!p.Alive || (aoe.Scope == AreaScope.AllOthers && p.PlayerId == sourceId)) continue;
                        total += ScoreEffects(view, aoe.Effects, sourceId, p.PlayerId, def);
                    }
                    return total;
                }
                case SkipPhaseEffect _:
                    return view.Hostility(targetId) * Weights.SkipPlayPhaseValue;
                default:
                    return 0f;
            }
        }

        public float ScoreDamage(PlayerPerspective view, int targetId, int amount)
        {
            var t = view.GetPlayer(targetId);
            if (!t.IsValid || !t.Alive || amount <= 0) return 0f;
            float h = view.Hostility(targetId);
            float v = amount * Weights.DamageValue;
            if (t.Hp - amount <= 0) v += Weights.KillBonus;
            else if (t.Hp - amount <= 1) v += Weights.LowHpBonus;
            // Damage to oneself or allies hurts more the lower their HP is.
            if (h < 0 && t.Hp - amount <= 0) v += Weights.KillBonus;
            return h * v;
        }

        public float ScoreHeal(PlayerPerspective view, int targetId, int amount)
        {
            var t = view.GetPlayer(targetId);
            if (!t.IsValid || !t.Alive) return 0f;
            int missing = t.MaxHp - t.Hp;
            if (missing <= 0) return -1f;
            float v = Math.Min(amount, missing) * Weights.HealValue;
            if (t.Hp <= 1) v *= 2f;
            else if (t.Hp >= 3) v *= Weights.ComfortableHealFactor;
            return -view.Hostility(targetId) * v;
        }

        private float ScoreRemoval(PlayerPerspective view, int targetId, int count, bool chosenBySource)
        {
            var t = view.GetPlayer(targetId);
            if (!t.IsValid || t.TotalCardCount == 0) return 0f;
            float h = view.Hostility(targetId);
            float per = t.Equipment.Count > 0 && chosenBySource ? Weights.RemoveEnemyEquipment : Weights.RemoveEnemyCard;
            // Removing a delayed trick from an ally is good.
            if (h < 0 && t.JudgeArea.Count > 0 && chosenBySource) return Weights.SkipPlayPhaseValue * 0.6f;
            return h * per * Math.Min(count, t.TotalCardCount);
        }

        private float ScoreEquip(PlayerPerspective view, EquipmentCard eq)
        {
            var current = view.Self.Equipment.Get(eq.Slot);
            if (current == null) return Weights.EquipValue;
            if (eq.Slot == EquipSlot.Weapon && current.Definition is EquipmentCard old)
            {
                int newRange = RangeOf(eq);
                int oldRange = RangeOf(old);
                if (newRange > oldRange) return Weights.EquipValue * 0.5f;
            }
            return -1f;
        }

        private static int RangeOf(EquipmentCard eq)
        {
            foreach (var c in eq.ContinuousEffects)
                if (c is AttackRangeEffect r) return r.Range;
            return 1;
        }

        /// <summary>Estimated chance that the target has fewer than <paramref name="count"/> usable response cards.</summary>
        public float ProbabilityCannotRespond(PlayerPerspective view, int targetId, string requiredCardId, int count)
        {
            var t = view.GetPlayer(targetId);
            if (!t.IsValid || t.HandCount == 0) return 1f;
            float q = Frequency(view, requiredCardId);
            float pHasOne = 1f - (float)Math.Pow(1f - q, t.HandCount);
            float pHasAll = (float)Math.Pow(pHasOne, Math.Max(1, count));
            return 1f - pHasAll;
        }

        public int CountUsableAs(PlayerPerspective view, string cardId)
        {
            int n = 0;
            var self = view.Self;
            foreach (var c in self.HandCards)
            {
                if (view.Rules.CanCardServeAs(self, c, cardId, null)) n++;
                else
                {
                    foreach (var si in self.Skills)
                    {
                        if (!si.Disabled && view.Rules.CanCardServeAs(self, c, cardId, si.Skill.SkillId))
                        {
                            n++;
                            break;
                        }
                    }
                }
            }
            return n;
        }

        private float Frequency(PlayerPerspective view, string cardId)
        {
            if (cardId == null) return 0f;
            if (!_frequency.TryGetValue(cardId, out float f))
            {
                f = view.DeckFrequency(cardId);
                _frequency[cardId] = f;
            }
            return f;
        }

        /// <summary>How much the AI wants to keep a card (used for discarding and paying costs).</summary>
        public float KeepValue(PlayerPerspective view, CardInstance card)
        {
            var self = view.Self;
            var def = card.Definition;
            if (!def.CanUseActively) return 6f + (self.Hp <= 2 ? 3f : 0f); // response-only cards (dodge)
            if (def.HasTag("heal")) return 9f;
            if (def.HasTag("attack")) return 5f;
            if (def is EquipmentCard eq) return self.Equipment.Get(eq.Slot) == null ? 5f : 1.5f;
            if (def is TrickCard) return 5.5f;
            return 4f;
        }
    }
}
