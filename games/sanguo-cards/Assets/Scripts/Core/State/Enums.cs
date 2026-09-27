namespace Sanguo.Core
{
    public enum Suit : byte
    {
        None = 0,
        Spade = 1,
        Heart = 2,
        Club = 3,
        Diamond = 4
    }

    public enum CardColor : byte
    {
        None = 0,
        Black = 1,
        Red = 2
    }

    /// <summary>Every physical card is in exactly one zone at any time.</summary>
    public enum ZoneType : byte
    {
        None = 0,
        DrawPile = 1,
        DiscardPile = 2,
        /// <summary>Cards currently being used, responded with or judged (face up on the table).</summary>
        Processing = 3,
        Hand = 4,
        Equipment = 5,
        JudgeArea = 6,
        Removed = 7
    }

    public enum EquipSlot : byte
    {
        None = 0,
        Weapon = 1,
        Armor = 2,
        /// <summary>Other players' distance to the owner +1.</summary>
        DefensiveMount = 3,
        /// <summary>Owner's distance to other players -1.</summary>
        OffensiveMount = 4,
        Treasure = 5
    }

    public enum Gender : byte
    {
        Male = 0,
        Female = 1,
        Neutral = 2
    }

    public enum Kingdom : byte
    {
        None = 0,
        Wei = 1,
        Shu = 2,
        Wu = 3,
        Qun = 4,
        God = 5
    }

    /// <summary>Team colour used by team modes (3v3, 5v5, 10v10). Public information.</summary>
    public enum Team : byte
    {
        None = 0,
        /// <summary>Red side.</summary>
        A = 1,
        /// <summary>Blue side.</summary>
        B = 2
    }

    /// <summary>
    /// Role inside a mode. <see cref="Unknown"/> is only ever produced for client views of a hidden
    /// role; the server always knows the real value.
    /// </summary>
    public enum Role : byte
    {
        None = 0,
        Unknown = 1,
        Lord = 2,
        Loyalist = 3,
        Rebel = 4,
        Renegade = 5,
        Captain = 6,
        Member = 7
    }

    /// <summary>Win-condition grouping. Players in the same faction win together.</summary>
    public enum Faction : byte
    {
        None = 0,
        /// <summary>Free for all: each player is their own faction.</summary>
        Solo = 1,
        /// <summary>Lord and loyalists.</summary>
        LordSide = 2,
        Rebels = 3,
        Renegade = 4,
        TeamA = 5,
        TeamB = 6
    }

    public enum MoveReason : byte
    {
        None = 0,
        Deal = 1,
        Draw = 2,
        Use = 3,
        Respond = 4,
        Discard = 5,
        DiscardPhase = 6,
        UseFinished = 7,
        Equip = 8,
        Unequip = 9,
        Replace = 10,
        Dismantle = 11,
        Steal = 12,
        Give = 13,
        Judge = 14,
        JudgeFinished = 15,
        PlaceDelayedTrick = 16,
        Death = 17,
        Reshuffle = 18,
        Skill = 19
    }

    public enum HpChangeReason : byte
    {
        None = 0,
        Damage = 1,
        Heal = 2,
        LoseHp = 3,
        MaxHpChange = 4,
        Setup = 5
    }
}
