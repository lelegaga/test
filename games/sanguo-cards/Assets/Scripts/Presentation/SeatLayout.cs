using System;
using System.Collections.Generic;
using Sanguo.Core;

namespace Sanguo.Presentation
{
    public enum SeatTier : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    public struct SeatSlot
    {
        public int PlayerId;
        /// <summary>Display order around the table, clockwise from the viewer's left neighbour.</summary>
        public int Order;
        public SeatTier Tier;
        /// <summary>Seats between the viewer and this player (shortest way round, all seats counted).</summary>
        public int SeatDistance;
    }

    /// <summary>
    /// Decides how the other players are shown on a phone. Small tables show everyone at medium size;
    /// big tables (10v10) emphasise the active player, the viewer's neighbours and the active
    /// player's neighbours, and shrink the rest into small avatars in a scrollable strip.
    /// </summary>
    public static class SeatLayout
    {
        /// <summary>Tables with at most this many other players show nobody at small size.</summary>
        public const int CompactThreshold = 7;

        public static List<SeatSlot> Arrange(ClientGameState state, int viewerId)
        {
            var result = new List<SeatSlot>();
            if (state == null || state.Players.Count == 0) return result;
            var bySeat = new List<ClientPlayerState>(state.Players);
            bySeat.Sort((a, b) => a.Seat.CompareTo(b.Seat));
            int n = bySeat.Count;
            int viewerIndex = bySeat.FindIndex(p => p.PlayerId == viewerId);
            int start = viewerIndex >= 0 ? viewerIndex + 1 : 0;
            int currentIndex = bySeat.FindIndex(p => p.PlayerId == state.CurrentPlayerId);
            int others = viewerIndex >= 0 ? n - 1 : n;

            for (int k = 0; k < others; k++)
            {
                int idx = (start + k) % n;
                var p = bySeat[idx];
                int dist = viewerIndex >= 0 ? RingDistance(idx, viewerIndex, n) : 0;
                SeatTier tier;
                if (idx == currentIndex) tier = SeatTier.Large;
                else if (others <= CompactThreshold) tier = SeatTier.Medium;
                else if ((viewerIndex >= 0 && dist <= 2) || (currentIndex >= 0 && RingDistance(idx, currentIndex, n) <= 1)) tier = SeatTier.Medium;
                else tier = SeatTier.Small;
                result.Add(new SeatSlot { PlayerId = p.PlayerId, Order = k, Tier = tier, SeatDistance = dist });
            }
            return result;
        }

        public static int RingDistance(int a, int b, int n)
        {
            int d = Math.Abs(a - b);
            return Math.Min(d, n - d);
        }

        /// <summary>Pixel width of a seat widget for a tier (reference 1920×1080 canvas).</summary>
        public static float WidthOf(SeatTier tier)
        {
            switch (tier)
            {
                case SeatTier.Large: return 210f;
                case SeatTier.Medium: return 170f;
                default: return 96f;
            }
        }
    }

    /// <summary>Bounded list of log lines rendered from events.</summary>
    public sealed class LogFeed
    {
        private readonly int _capacity;
        private readonly List<string> _lines = new List<string>();

        public LogFeed(int capacity = 300)
        {
            _capacity = Math.Max(10, capacity);
        }

        public IReadOnlyList<string> Lines => _lines;
        public int Version { get; private set; }

        public void Add(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _lines.Add(line);
            if (_lines.Count > _capacity) _lines.RemoveRange(0, _lines.Count - _capacity);
            Version++;
        }

        public void AddEvent(Events.GameEvent e, Events.IGameLogNames names, int viewerId)
        {
            Add(Events.GameLogFormatter.Format(e, names, viewerId));
        }

        public void Clear()
        {
            _lines.Clear();
            Version++;
        }
    }

    /// <summary>Display helpers for card faces.</summary>
    public static class CardText
    {
        public static string SuitSymbol(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spade: return "♠";
                case Suit.Heart: return "♥";
                case Suit.Club: return "♣";
                case Suit.Diamond: return "♦";
                default: return "";
            }
        }

        public static string NumberText(int number)
        {
            switch (number)
            {
                case 1: return "A";
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                default: return number > 0 ? number.ToString() : "";
            }
        }

        public static bool IsRed(Suit suit) => suit == Suit.Heart || suit == Suit.Diamond;

        public static string PhaseName(GamePhase phase) => Events.GameLogFormatter.PhaseName(phase);
    }
}
