using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.GameModes;

namespace Sanguo.AI
{
    /// <summary>Receives the (projected) event stream of its seat.</summary>
    public interface IGameEventObserver
    {
        void Observe(GameEvent e);
    }

    /// <summary>
    /// Identity-mode allegiance inference from public behaviour only. Every player gets an
    /// "affinity" to the lord: attacking the lord or loyal-looking players lowers it, protecting the
    /// lord or attacking rebel-looking players raises it, and revealed roles of the dead anchor it.
    /// Hostility is then derived from the observer's own (secret) role.
    /// </summary>
    public sealed class IdentityRelationEstimator : IRelationEstimator, IGameEventObserver
    {
        private struct Interaction
        {
            public int Source;
            public int Target;
            public float Weight; // > 0 harmful, < 0 helpful
            public string CardId;
        }

        private readonly List<Interaction> _interactions = new List<Interaction>();
        private readonly Dictionary<int, Role> _revealed = new Dictionary<int, Role>();
        private readonly List<KeyValuePair<int, Role>> _kills = new List<KeyValuePair<int, Role>>(); // killer, victim role
        private float[] _affinity = Array.Empty<float>();
        private bool _dirty = true;

        public void Observe(GameEvent e)
        {
            switch (e)
            {
                case TargetSelectedEvent t when t.SourceId >= 0 && t.SourceId != t.TargetId:
                    Add(t.SourceId, t.TargetId, 1f, t.CardId);
                    break;
                case DamageAppliedEvent d when d.SourceId >= 0 && d.SourceId != d.TargetId:
                    Add(d.SourceId, d.TargetId, 0.5f * d.Amount, d.CardId);
                    break;
                case HealAppliedEvent h when h.SourceId >= 0 && h.SourceId != h.TargetId:
                    Add(h.SourceId, h.TargetId, -1.5f, null);
                    break;
                case PlayerDiedEvent pd:
                    if (pd.RevealedRole != Role.None && pd.RevealedRole != Role.Unknown) _revealed[pd.PlayerId] = pd.RevealedRole;
                    if (pd.KillerId >= 0 && pd.KillerId != pd.PlayerId) _kills.Add(new KeyValuePair<int, Role>(pd.KillerId, pd.RevealedRole));
                    _dirty = true;
                    break;
                case RoleRevealedEvent rr:
                    _revealed[rr.PlayerId] = rr.Role;
                    _dirty = true;
                    break;
            }
        }

        private void Add(int source, int target, float weight, string cardId)
        {
            _interactions.Add(new Interaction { Source = source, Target = target, Weight = weight, CardId = cardId });
            _dirty = true;
        }

        /// <summary>Current affinity to the lord (positive = seems loyal).</summary>
        public float Affinity(PlayerPerspective view, int playerId)
        {
            Recompute(view);
            return playerId >= 0 && playerId < _affinity.Length ? _affinity[playerId] : 0f;
        }

        private void Recompute(PlayerPerspective view)
        {
            if (!_dirty && _affinity.Length == view.PlayerCount) return;
            int n = view.PlayerCount;
            int lord = FindLord(view);
            var baseAff = new float[n];
            foreach (var kv in _kills)
            {
                if (kv.Key < 0 || kv.Key >= n) continue;
                if (kv.Value == Role.Rebel) baseAff[kv.Key] += 2f;
                else if (kv.Value == Role.Loyalist) baseAff[kv.Key] -= 2f;
            }
            foreach (var it in _interactions)
            {
                if (it.Target != lord || it.Source < 0 || it.Source >= n) continue;
                if (IsAreaCard(view, it.CardId)) continue;
                baseAff[it.Source] -= 2f * Signed(view, it);
            }
            var aff = (float[])baseAff.Clone();
            for (int iteration = 0; iteration < 3; iteration++)
            {
                var next = (float[])baseAff.Clone();
                foreach (var it in _interactions)
                {
                    if (it.Target == lord || it.Source < 0 || it.Source >= n || it.Target < 0 || it.Target >= n) continue;
                    if (IsAreaCard(view, it.CardId)) continue;
                    float targetLean = Math.Max(-1f, Math.Min(1f, KnownLean(it.Target) ?? aff[it.Target] / 2f));
                    // Harming a loyal-looking player makes you look disloyal, and vice versa.
                    next[it.Source] -= 0.5f * Signed(view, it) * targetLean;
                }
                aff = next;
            }
            for (int i = 0; i < n; i++)
            {
                var known = KnownLean(i);
                if (known.HasValue) aff[i] = known.Value * 3f;
            }
            if (lord >= 0) aff[lord] = 3f;
            _affinity = aff;
            _dirty = false;
        }

        private float? KnownLean(int playerId)
        {
            if (!_revealed.TryGetValue(playerId, out var role)) return null;
            switch (role)
            {
                case Role.Lord:
                case Role.Loyalist:
                    return 1f;
                case Role.Rebel:
                    return -1f;
                default:
                    return 0f;
            }
        }

        private static float Signed(PlayerPerspective view, Interaction it)
        {
            if (it.Weight < 0) return it.Weight; // helpful
            if (it.CardId == null) return it.Weight;
            var def = view.GetCardDefinition(it.CardId);
            if (def == null) return it.Weight;
            if (def.HasTag("heal")) return -it.Weight;
            if (def.HasTag("attack") || def.HasTag("control")) return it.Weight;
            return 0f;
        }

        private static bool IsAreaCard(PlayerPerspective view, string cardId)
        {
            if (cardId == null) return false;
            var def = view.GetCardDefinition(cardId);
            return def != null && def.HasTag("aoe");
        }

        private static int FindLord(PlayerPerspective view)
        {
            foreach (var p in view.Players)
                if (view.KnownRole(p.PlayerId) == Role.Lord) return p.PlayerId;
            return -1;
        }

        public float EstimateHostility(PlayerPerspective view, int targetId)
        {
            if (targetId == view.ViewerId) return -1f;
            var target = view.GetPlayer(targetId);
            if (!target.IsValid || !target.Alive) return 0f;
            int lord = FindLord(view);
            float aff = Affinity(view, targetId);
            var me = view.OwnRole;

            int rebels = Math.Max(0, view.AliveRoleCount(Role.Rebel));
            int renegades = Math.Max(0, view.AliveRoleCount(Role.Renegade));
            int loyalists = Math.Max(0, view.AliveRoleCount(Role.Loyalist));

            switch (me)
            {
                case Role.Lord:
                case Role.Loyalist:
                {
                    if (targetId == lord) return -1f;
                    int unknownHostile = rebels + renegades;
                    int unknownFriendly = me == Role.Loyalist ? loyalists - 1 : loyalists;
                    float prior = Prior(unknownHostile, unknownFriendly);
                    return Clamp(prior - aff / 2f);
                }
                case Role.Rebel:
                {
                    if (targetId == lord) return 1f;
                    float prior = Prior(loyalists + renegades, rebels - 1);
                    return Clamp(prior + aff / 2f);
                }
                case Role.Renegade:
                {
                    int alive = view.AliveCount;
                    if (rebels > 0)
                    {
                        // Keep the lord alive while rebels remain; hunt rebel-looking players.
                        if (targetId == lord) return alive <= 2 ? 1f : -0.7f;
                        float prior = Prior(rebels, loyalists);
                        return Clamp((prior - aff / 2f) * 0.8f);
                    }
                    // Only the lord side (and maybe another renegade) is left: loyalists first, lord last.
                    if (targetId == lord) return alive <= 2 ? 1f : 0.2f;
                    return 1f;
                }
                default:
                    return view.KnownHostility(targetId);
            }
        }

        /// <summary>Prior hostility for an unidentified player from how many enemies vs friends remain.</summary>
        private static float Prior(int hostile, int friendly)
        {
            hostile = Math.Max(0, hostile);
            friendly = Math.Max(0, friendly);
            if (hostile + friendly == 0) return 0f;
            float frac = (float)hostile / (hostile + friendly);
            return (2f * frac - 1f) * 0.5f;
        }

        private static float Clamp(float v) => Math.Max(-1f, Math.Min(1f, v));
    }

    /// <summary>Chooses the relation estimator suited to a mode (null when relations are public).</summary>
    public static class AIRelationEstimators
    {
        public static IRelationEstimator CreateFor(IGameMode mode)
        {
            return mode is IdentityMode ? new IdentityRelationEstimator() : null;
        }
    }
}
