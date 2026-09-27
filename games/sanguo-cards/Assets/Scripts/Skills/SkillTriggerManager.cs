using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.Utils;

namespace Sanguo.Skills
{
    /// <summary>
    /// Collects the skills reacting to a trigger timing and queues them for resolution.
    ///
    /// Flow: an action reaches a timing (e.g. damage before) → <see cref="Fire"/> scans every
    /// player's skills in seat order starting from the current player → matching skills form a
    /// <see cref="TriggerQueueAction"/> pushed on the action stack → the server resolves them one by
    /// one (asking owners of optional skills) before the firing action continues.
    ///
    /// Recursion control: triggers are resolved iteratively on the action stack, each firing builds
    /// its queue once (a skill reacts at most once per firing), and nesting of trigger queues is
    /// capped at <see cref="MaxTriggerDepth"/>: deeper firings are ignored, so mutually triggering
    /// skills cannot loop forever.
    /// </summary>
    public sealed class SkillTriggerManager
    {
        public const int MaxTriggerDepth = 8;

        /// <summary>Number of firings dropped by the depth guard (diagnostics/tests).</summary>
        public int SuppressedFirings { get; private set; }

        /// <summary>Queues matching skills. Returns true if anything was queued.</summary>
        public bool Fire(GameContext ctx, TriggerEventArgs args)
        {
            if (ctx.State.IsGameOver) return false;
            var s = ctx.State;
            var order = ListPool<PlayerState>.Get();
            List<TriggerQueueAction.Entry> entries = null;
            try
            {
                var start = s.CurrentPlayer ?? (s.SeatOrder.Count > 0 ? s.SeatOrder[0] : null);
                int n = s.SeatOrder.Count;
                int startIdx = start != null ? s.SeatIndexOf(start) : 0;
                for (int step = 0; step < n; step++) order.Add(s.SeatOrder[(startIdx + step) % n]);

                foreach (var p in order)
                {
                    if (!p.Alive && !IsDeadSubject(p, args)) continue;
                    for (int i = 0; i < p.Skills.Count; i++)
                    {
                        var si = p.Skills[i];
                        if (si.Disabled || !si.Skill.ListensTo(args.Timing)) continue;
                        if (!si.Skill.CanTrigger(ctx, p, args)) continue;
                        if (entries == null) entries = new List<TriggerQueueAction.Entry>();
                        entries.Add(new TriggerQueueAction.Entry(p, si));
                    }
                }
            }
            finally
            {
                ListPool<PlayerState>.Release(order);
            }

            if (entries == null) return false;
            if (ctx.Stack.CountOf<TriggerQueueAction>() >= MaxTriggerDepth)
            {
                SuppressedFirings++;
                return false;
            }
            args.EventId = s.NextTriggerEventId++;
            ctx.Push(new TriggerQueueAction(entries, args));
            return true;
        }

        internal static bool IsDeadSubject(PlayerState p, TriggerEventArgs args)
        {
            return args.Timing == TriggerTiming.OnDeath && args.PlayerId == p.PlayerId;
        }
    }

    /// <summary>Resolves the skills collected for one trigger firing, in order.</summary>
    public sealed class TriggerQueueAction : GameAction
    {
        public readonly struct Entry
        {
            public readonly PlayerState Owner;
            public readonly SkillInstance Skill;

            public Entry(PlayerState owner, SkillInstance skill)
            {
                Owner = owner;
                Skill = skill;
            }
        }

        private readonly List<Entry> _entries;
        private readonly TriggerEventArgs _args;
        private int _index;
        private ConfirmRequest _pending;

        public TriggerQueueAction(List<Entry> entries, TriggerEventArgs args)
        {
            _entries = entries;
            _args = args;
        }

        public override ActionResult Step(GameContext ctx)
        {
            while (_index < _entries.Count)
            {
                var e = _entries[_index];
                if (_pending != null)
                {
                    bool accepted = _pending.Accepted;
                    _pending = null;
                    _index++;
                    if (accepted && StillValid(ctx, e))
                    {
                        Activate(ctx, e);
                        return ActionResult.Continue;
                    }
                    continue;
                }

                if (!StillValid(ctx, e))
                {
                    _index++;
                    continue;
                }
                if (e.Skill.Skill.IsOptionalTrigger)
                {
                    _pending = ctx.Requests.Open(ctx, new ConfirmRequest(e.Owner.PlayerId, e.Skill.Skill.SkillId, "trigger"));
                    return ActionResult.Wait;
                }
                _index++;
                Activate(ctx, e);
                return ActionResult.Continue;
            }
            return ActionResult.Done;
        }

        private bool StillValid(GameContext ctx, Entry e)
        {
            if (!e.Owner.Alive && !SkillTriggerManager.IsDeadSubject(e.Owner, _args)) return false;
            return !e.Skill.Disabled && e.Skill.Skill.CanTrigger(ctx, e.Owner, _args);
        }

        private void Activate(GameContext ctx, Entry e)
        {
            ctx.Mutator.RecordSkillUse(e.Owner, e.Skill);
            ctx.Emit(new SkillActivatedEvent { PlayerId = e.Owner.PlayerId, SkillId = e.Skill.Skill.SkillId, Targets = System.Array.Empty<int>() });
            var action = e.Skill.Skill.CreateTriggerAction(ctx, e.Owner, _args);
            if (action != null) ctx.Push(action);
        }
    }
}
