using System.Collections.Generic;
using System.Text;
using Sanguo.Cards;
using Sanguo.GameModes;

namespace Sanguo.Core
{
    /// <summary>
    /// The single authoritative game state. Exists only on the server (host). Clients hold a
    /// <see cref="ClientGameState"/> rebuilt from snapshots and incremental events.
    /// </summary>
    public sealed class GameState
    {
        private readonly List<PlayerState> _players = new List<PlayerState>();
        private readonly List<PlayerState> _seatOrder = new List<PlayerState>();
        private readonly Dictionary<int, CardInstance> _cards = new Dictionary<int, CardInstance>();

        public GameState(string roomId, GameModeConfig config)
        {
            RoomId = roomId ?? string.Empty;
            Config = config;
        }

        public string RoomId { get; }
        public GameModeConfig Config { get; }

        /// <summary>Players indexed by PlayerId (0..N-1).</summary>
        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>Players ordered by seat; turn order and "seat order" resolution follow this list.</summary>
        public IReadOnlyList<PlayerState> SeatOrder => _seatOrder;

        public CardZone DrawPile { get; } = new CardZone(ZoneType.DrawPile);
        public CardZone DiscardPile { get; } = new CardZone(ZoneType.DiscardPile);
        public CardZone Processing { get; } = new CardZone(ZoneType.Processing);

        public IReadOnlyDictionary<int, CardInstance> Cards => _cards;

        public TurnState Turn { get; } = new TurnState();
        public GameStateMachine StateMachine { get; } = new GameStateMachine();
        public GamePhase Phase => StateMachine.Phase;

        public bool IsGameOver { get; internal set; }
        public GameResult Result { get; internal set; }

        internal int NextUseId { get; set; } = 1;
        internal int NextTriggerEventId { get; set; } = 1;

        public int PlayerCount => _players.Count;

        public int AliveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _players.Count; i++)
                    if (_players[i].Alive) n++;
                return n;
            }
        }

        public PlayerState CurrentPlayer => GetPlayer(Turn.CurrentPlayerId);

        public PlayerState GetPlayer(int playerId)
        {
            return playerId >= 0 && playerId < _players.Count ? _players[playerId] : null;
        }

        internal void AddPlayer(PlayerState player)
        {
            _players.Add(player);
        }

        internal void RebuildSeatOrder()
        {
            _seatOrder.Clear();
            _seatOrder.AddRange(_players);
            _seatOrder.Sort((a, b) => a.Seat.CompareTo(b.Seat));
        }

        internal void RegisterCard(CardInstance card)
        {
            _cards.Add(card.InstanceId, card);
        }

        public CardInstance GetCard(int instanceId)
        {
            return _cards.TryGetValue(instanceId, out var c) ? c : null;
        }

        /// <summary>Index of the player in <see cref="SeatOrder"/>.</summary>
        public int SeatIndexOf(PlayerState p) => _seatOrder.IndexOf(p);

        /// <summary>Next living player clockwise from <paramref name="from"/> (excluding it), or null.</summary>
        public PlayerState NextAlive(PlayerState from)
        {
            int n = _seatOrder.Count;
            int start = SeatIndexOf(from);
            for (int step = 1; step <= n; step++)
            {
                var p = _seatOrder[(start + step) % n];
                if (p.Alive && !ReferenceEquals(p, from)) return p;
            }
            return null;
        }

        /// <summary>Living players in seat order starting at <paramref name="start"/>.</summary>
        public void GetAliveInSeatOrder(PlayerState start, bool includeStart, List<PlayerState> output)
        {
            int n = _seatOrder.Count;
            int startIdx = start != null ? SeatIndexOf(start) : 0;
            if (startIdx < 0) startIdx = 0;
            for (int step = 0; step < n; step++)
            {
                var p = _seatOrder[(startIdx + step) % n];
                if (!p.Alive) continue;
                if (!includeStart && step == 0 && start != null && ReferenceEquals(p, start)) continue;
                output.Add(p);
            }
        }

        /// <summary>
        /// Debug check that every card is in exactly one zone and its location fields match.
        /// Returns null when consistent, otherwise a description of the first problems found.
        /// </summary>
        public string ValidateInvariants()
        {
            var seen = new HashSet<int>();
            var sb = new StringBuilder();

            void Check(CardInstance c, ZoneType zone, int owner)
            {
                if (!seen.Add(c.InstanceId)) sb.Append("card ").Append(c).Append(" appears twice; ");
                if (c.Zone != zone || c.OwnerId != owner)
                    sb.Append("card ").Append(c).Append(" location mismatch (").Append(c.Zone).Append('/').Append(c.OwnerId)
                        .Append(" vs ").Append(zone).Append('/').Append(owner).Append("); ");
            }

            foreach (var c in DrawPile) Check(c, ZoneType.DrawPile, -1);
            foreach (var c in DiscardPile) Check(c, ZoneType.DiscardPile, -1);
            foreach (var c in Processing) Check(c, ZoneType.Processing, -1);
            foreach (var p in _players)
            {
                foreach (var c in p.HandCards) Check(c, ZoneType.Hand, p.PlayerId);
                foreach (var c in p.Equipment.Cards) Check(c, ZoneType.Equipment, p.PlayerId);
                foreach (var c in p.JudgeArea) Check(c, ZoneType.JudgeArea, p.PlayerId);
                if (p.Hp > p.MaxHp) sb.Append(p).Append(" hp above max; ");
            }
            if (seen.Count != _cards.Count)
                sb.Append("tracked ").Append(seen.Count).Append(" of ").Append(_cards.Count).Append(" cards; ");
            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}
