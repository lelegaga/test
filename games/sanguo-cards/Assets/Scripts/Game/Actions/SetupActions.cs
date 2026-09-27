using System;
using System.Collections.Generic;
using Sanguo.Characters;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Skills;
using Sanguo.Utils;

namespace Sanguo.Game
{
    /// <summary>
    /// Preparation: seats/teams/roles (mode) → deck → characters, group by group as the mode orders
    /// them (none / random / choose from private options) → opening hands.
    /// </summary>
    public sealed class GameSetupAction : GameAction
    {
        private readonly IReadOnlyList<PlayerSetup> _setups;
        private readonly Dictionary<int, CharacterData> _chosen = new Dictionary<int, CharacterData>();
        private readonly HashSet<int> _applied = new HashSet<int>();
        private readonly List<ChooseCharacterRequest> _requests = new List<ChooseCharacterRequest>();
        private IReadOnlyList<IReadOnlyList<PlayerState>> _groups;
        private List<CharacterData> _pool;
        private int _poolIndex;
        private int _groupIndex;
        private int _stage;

        public GameSetupAction(IReadOnlyList<PlayerSetup> setups)
        {
            _setups = setups;
        }

        public override ActionResult Step(GameContext ctx)
        {
            var s = ctx.State;
            switch (_stage)
            {
                case 0:
                    ctx.Mutator.TransitionPhase(GamePhase.Preparing);
                    ctx.Mode.SetupPlayers(new GameSetupContext(s, _setups, ctx.Random));
                    s.RebuildSeatOrder();
                    BuildDeck(ctx);
                    PrepareCharacterSelection(ctx);
                    _stage = 1;
                    return ActionResult.Continue;
                case 1:
                    return SelectNextGroup(ctx);
                case 2:
                    // Answers for the current group arrived; unpicked options go back into the pool.
                    foreach (var r in _requests)
                        if (ctx.Content.Characters.TryGet(r.SelectedCharacterId, out var c)) _chosen[r.PlayerId] = c;
                    ReturnUnpicked(ctx);
                    _requests.Clear();
                    ApplyGroup(ctx, _groups[_groupIndex]);
                    _groupIndex++;
                    _stage = 1;
                    return ActionResult.Continue;
                default:
                    foreach (var p in s.SeatOrder)
                    {
                        int count = ctx.Config.StartingHandSize + ctx.Mode.GetStartingHandBonus(s, p);
                        if (count > 0) ctx.Mutator.DrawCards(p, count, MoveReason.Deal);
                    }
                    return ActionResult.Done;
            }
        }

        private static void BuildDeck(GameContext ctx)
        {
            var s = ctx.State;
            var deck = ctx.Content.GetDeck(ctx.Config.DeckId).Build(ctx.Content.Cards, ctx.Config.ResolveDeckCopies());
            foreach (var card in deck)
            {
                s.RegisterCard(card);
                s.DrawPile.Add(card);
            }
            s.DrawPile.Shuffle(ctx.Random);
        }

        private void PrepareCharacterSelection(GameContext ctx)
        {
            var s = ctx.State;
            for (int i = 0; i < s.PlayerCount && i < _setups.Count; i++)
            {
                if (_setups[i].CharacterId != null && ctx.Content.Characters.TryGet(_setups[i].CharacterId, out var pre))
                    _chosen[i] = pre;
            }
            _groups = ctx.Config.CharacterSelection == CharacterSelectionMode.None
                ? new List<IReadOnlyList<PlayerState>> { new List<PlayerState>(s.SeatOrder) }
                : ctx.Mode.GetCharacterSelectionGroups(s);
            _pool = BuildPool(ctx);
            ctx.Random.Shuffle(_pool);
        }

        private ActionResult SelectNextGroup(GameContext ctx)
        {
            var config = ctx.Config;
            if (_groupIndex >= _groups.Count)
            {
                // Anyone a mode left out of the groups still needs a character.
                foreach (var p in ctx.State.SeatOrder) Apply(ctx, p);
                _stage = 3;
                return ActionResult.Continue;
            }
            var group = _groups[_groupIndex];
            if (config.CharacterSelection != CharacterSelectionMode.None)
            {
                // Share what is left of the roster fairly: on big tables every player of the group
                // gets fewer options instead of the last seats getting none.
                int needing = 0;
                foreach (var p in group)
                    if (!_chosen.ContainsKey(p.PlayerId)) needing++;
                int fairShare = needing > 0 ? Math.Max(1, RemainingInPool() / needing) : 1;
                foreach (var p in group)
                {
                    if (_chosen.ContainsKey(p.PlayerId)) continue;
                    if (config.CharacterSelection == CharacterSelectionMode.Random)
                    {
                        var c = NextFromPool();
                        if (c != null) _chosen[p.PlayerId] = c;
                        continue;
                    }
                    int count = Math.Min(ctx.Mode.GetCharacterChoiceCount(ctx.State, p, config.CharacterChoices), fairShare);
                    var options = new List<string>();
                    for (int k = 0; k < count; k++)
                    {
                        var c = NextFromPool();
                        if (c == null) break;
                        options.Add(c.Id);
                    }
                    if (options.Count > 0) _requests.Add(ctx.Requests.Open(ctx, new ChooseCharacterRequest(p.PlayerId, options)));
                }
            }
            if (_requests.Count > 0)
            {
                _stage = 2;
                return ActionResult.Wait;
            }
            ApplyGroup(ctx, group);
            _groupIndex++;
            return ActionResult.Continue;
        }

        private void ReturnUnpicked(GameContext ctx)
        {
            var back = new List<CharacterData>();
            foreach (var r in _requests)
            {
                foreach (var id in r.CharacterIds)
                {
                    if (id == r.SelectedCharacterId || !ctx.Content.Characters.TryGet(id, out var c)) continue;
                    if (!IsAlreadyChosen(c)) back.Add(c);
                }
            }
            if (back.Count == 0) return;
            var rest = _pool.GetRange(_poolIndex, _pool.Count - _poolIndex);
            rest.AddRange(back);
            ctx.Random.Shuffle(rest);
            _pool = rest;
            _poolIndex = 0;
        }

        private int RemainingInPool()
        {
            int n = 0;
            for (int i = _poolIndex; i < _pool.Count; i++)
                if (!IsAlreadyChosen(_pool[i])) n++;
            return n;
        }

        private CharacterData NextFromPool()
        {
            while (_poolIndex < _pool.Count)
            {
                var c = _pool[_poolIndex++];
                if (!IsAlreadyChosen(c)) return c;
            }
            return null;
        }

        private List<CharacterData> BuildPool(GameContext ctx)
        {
            var pool = new List<CharacterData>();
            var config = ctx.Config;
            if (config.CharacterPool.Count > 0)
            {
                foreach (var id in config.CharacterPool)
                    if (ctx.Content.Characters.TryGet(id, out var c) && !IsAlreadyChosen(c)) pool.Add(c);
            }
            else
            {
                foreach (var c in ctx.Content.Characters.All)
                    if (c.Id != config.DefaultCharacterId && !IsAlreadyChosen(c)) pool.Add(c);
            }
            return pool;
        }

        private bool IsAlreadyChosen(CharacterData c)
        {
            foreach (var v in _chosen.Values)
                if (ReferenceEquals(v, c)) return true;
            return false;
        }

        private void ApplyGroup(GameContext ctx, IReadOnlyList<PlayerState> group)
        {
            foreach (var p in group) Apply(ctx, p);
        }

        private void Apply(GameContext ctx, PlayerState p)
        {
            if (!_applied.Add(p.PlayerId)) return;
            var config = ctx.Config;
            bool useDefaultHp = config.CharacterSelection == CharacterSelectionMode.None && !_chosen.ContainsKey(p.PlayerId);
            if (!_chosen.TryGetValue(p.PlayerId, out var character))
            {
                if (!ctx.Content.Characters.TryGet(config.DefaultCharacterId, out character))
                    character = new CharacterData { Id = config.DefaultCharacterId, Name = "无名小卒", MaxHp = config.DefaultMaxHp };
            }
            p.Character = character;
            int baseHp = useDefaultHp ? config.DefaultMaxHp : character.MaxHp;
            p.MaxHp = baseHp + ctx.Mode.GetMaxHpBonus(ctx.State, p);
            p.Hp = p.MaxHp;
            p.Skills.Clear();
            var ev = new CharacterAssignedEvent { PlayerId = p.PlayerId, CharacterId = character.Id, Hp = p.Hp, MaxHp = p.MaxHp };
            bool lordSkills = ctx.Mode.CanUseLordSkills(ctx.State, p);
            foreach (var id in character.SkillIds) AddSkill(ctx, p, id, ev, lordSkills);
            foreach (var id in ctx.Mode.GetExtraSkillIds(ctx.State, p)) AddSkill(ctx, p, id, ev, true);
            ctx.Emit(ev);
        }

        private static void AddSkill(GameContext ctx, PlayerState p, string skillId, CharacterAssignedEvent ev, bool allowLordSkill)
        {
            if (!ctx.Content.Skills.TryGet(skillId, out var skill) || p.FindSkill(skillId) != null) return;
            if (skill.IsLordSkill && !allowLordSkill) return;
            p.Skills.Add(new SkillInstance(skill));
            ev.SkillIds.Add(skillId);
        }
    }
}
