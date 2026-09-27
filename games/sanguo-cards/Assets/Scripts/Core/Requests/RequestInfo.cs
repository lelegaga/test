using System.Collections.Generic;

namespace Sanguo.Core
{
    public enum RequestKind : byte
    {
        /// <summary>Main play-phase decision: play a card, use a skill or end the phase.</summary>
        PlayAction = 0,
        /// <summary>Respond with a card (dodge a strike, strike back in a duel, rescue with a heal...).</summary>
        CardResponse = 1,
        /// <summary>Discard a number of own cards.</summary>
        Discard = 2,
        /// <summary>Pick one card from another player's areas (dismantle, steal).</summary>
        ChooseCardFromPlayer = 3,
        ChooseTargets = 4,
        /// <summary>Yes/no, typically "activate this optional skill?".</summary>
        Confirm = 5,
        ChooseOption = 6,
        ChooseCharacter = 7
    }

    /// <summary>
    /// Client-facing description of an open request. Everyone learns that a player is being asked
    /// something (for timers and table feedback); the private details (<see cref="Options"/>,
    /// <see cref="Candidates"/>) go only to the asked player.
    /// </summary>
    public sealed class RequestInfo
    {
        public int RequestId;
        public int PlayerId;
        public RequestKind Kind;
        public long DeadlineMs;
        public bool IsResponseWindow;
        public bool AllowPass;
        public string RequiredCardId;
        public int Count;
        public int MinCount;
        public int SourcePlayerId = -1;
        public int TargetPlayerId = -1;
        public string ContextCardId;
        public string SkillId;
        public string Purpose;
        /// <summary>ZoneMask for ChooseCardFromPlayer requests.</summary>
        public int Zones;

        /// <summary>Private: whether the details below are present.</summary>
        public bool HasPrivateDetails;
        public List<int> Candidates;
        public List<string> Options;
        /// <summary>Private: usable cards (play phase) or cards that may answer (responses).</summary>
        public List<PlayHint> PlayHints;
        /// <summary>Private: active skills usable now.</summary>
        public List<SkillHint> SkillHints;

        public PlayHint FindHint(int cardInstanceId, string skillId = null)
        {
            if (PlayHints == null) return null;
            foreach (var h in PlayHints)
                if (h.CardInstanceId == cardInstanceId && h.SkillId == skillId) return h;
            return null;
        }

        public RequestInfo PublicView()
        {
            return new RequestInfo
            {
                RequestId = RequestId,
                PlayerId = PlayerId,
                Kind = Kind,
                DeadlineMs = DeadlineMs,
                IsResponseWindow = IsResponseWindow,
                AllowPass = AllowPass,
                RequiredCardId = RequiredCardId,
                Count = Count,
                MinCount = MinCount,
                SourcePlayerId = SourcePlayerId,
                TargetPlayerId = TargetPlayerId,
                ContextCardId = ContextCardId,
                SkillId = SkillId,
                Purpose = Purpose,
                Zones = Zones,
                HasPrivateDetails = false
            };
        }

        public string Describe()
        {
            return Kind + "#" + RequestId + "(p" + PlayerId + (RequiredCardId != null ? "," + RequiredCardId : "") + ")";
        }
    }

    /// <summary>A card the player may use/play now, as <see cref="AsCardId"/> (through <see cref="SkillId"/> if converted).</summary>
    public sealed class PlayHint
    {
        public int CardInstanceId;
        public string AsCardId;
        public string SkillId;
        /// <summary>True when targets must be chosen (Min/Max/LegalTargets apply).</summary>
        public bool NeedsTargets;
        public int MinTargets;
        public int MaxTargets;
        public List<int> LegalTargets = new List<int>();
    }

    /// <summary>An active skill usable now.</summary>
    public sealed class SkillHint
    {
        public string SkillId;
        public int MinCards;
        public int MaxCards;
        public int MinTargets;
        public int MaxTargets;
        public List<int> LegalTargets = new List<int>();
    }
}
