using Sanguo.Core;
using UnityEngine;

namespace Sanguo.UI
{
    /// <summary>Colours and sizes of the placeholder art style (swap for licensed art later).</summary>
    public static class UITheme
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        public static readonly Color Background = new Color32(0x2B, 0x1D, 0x16, 0xFF);
        public static readonly Color Table = new Color32(0x3E, 0x5B, 0x3A, 0xFF);
        public static readonly Color Panel = new Color32(0x1E, 0x16, 0x12, 0xE6);
        public static readonly Color PanelLight = new Color32(0x4A, 0x36, 0x2A, 0xF0);
        public static readonly Color Gold = new Color32(0xE8, 0xC1, 0x6A, 0xFF);
        public static readonly Color Text = new Color32(0xF4, 0xEA, 0xD5, 0xFF);
        public static readonly Color TextDim = new Color32(0xB9, 0xA8, 0x8C, 0xFF);
        public static readonly Color CardFace = new Color32(0xF6, 0xEE, 0xDC, 0xFF);
        public static readonly Color CardBack = new Color32(0x7A, 0x2E, 0x22, 0xFF);
        public static readonly Color SuitRed = new Color32(0xC0, 0x22, 0x1E, 0xFF);
        public static readonly Color SuitBlack = new Color32(0x1E, 0x1E, 0x1E, 0xFF);
        public static readonly Color Button = new Color32(0x8C, 0x2F, 0x22, 0xFF);
        public static readonly Color ButtonSecondary = new Color32(0x4D, 0x4A, 0x44, 0xFF);
        public static readonly Color ButtonDisabled = new Color32(0x44, 0x3C, 0x36, 0xB0);
        public static readonly Color Selectable = new Color32(0xFF, 0xD5, 0x4A, 0xFF);
        public static readonly Color Selected = new Color32(0xFF, 0x4A, 0x3A, 0xFF);
        public static readonly Color CurrentTurn = new Color32(0x6F, 0xE0, 0x8A, 0xFF);
        public static readonly Color Dim = new Color(0, 0, 0, 0.55f);
        public static readonly Color HpHigh = new Color32(0x4C, 0xC2, 0x5A, 0xFF);
        public static readonly Color HpMid = new Color32(0xE6, 0xB8, 0x2E, 0xFF);
        public static readonly Color HpLow = new Color32(0xD8, 0x3A, 0x2E, 0xFF);
        public static readonly Color HpEmpty = new Color32(0x3A, 0x33, 0x2E, 0xFF);
        public static readonly Color TeamA = new Color32(0xD9, 0x4A, 0x3A, 0xFF);
        public static readonly Color TeamB = new Color32(0x3A, 0x7A, 0xD9, 0xFF);

        public const int FontSmall = 22;
        public const int FontBody = 28;
        public const int FontLarge = 36;
        public const int FontTitle = 72;

        public static Color KingdomColor(Kingdom k)
        {
            switch (k)
            {
                case Kingdom.Wei: return new Color32(0x3C, 0x5A, 0x9E, 0xFF);
                case Kingdom.Shu: return new Color32(0xB0, 0x3A, 0x2E, 0xFF);
                case Kingdom.Wu: return new Color32(0x3E, 0x8E, 0x4E, 0xFF);
                case Kingdom.Qun: return new Color32(0x8A, 0x80, 0x70, 0xFF);
                default: return new Color32(0x6A, 0x5A, 0x8A, 0xFF);
            }
        }

        public static Color TeamColor(Team t) => t == Team.A ? TeamA : t == Team.B ? TeamB : Gold;

        public static Color HpColor(int hp, int max)
        {
            if (max <= 0) return HpEmpty;
            float r = hp / (float)max;
            return r > 0.6f ? HpHigh : r > 0.3f ? HpMid : HpLow;
        }

        public static Color RoleColor(Role role)
        {
            switch (role)
            {
                case Role.Lord: return new Color32(0xE8, 0xB0, 0x2E, 0xFF);
                case Role.Loyalist: return new Color32(0x3E, 0x8E, 0xD6, 0xFF);
                case Role.Rebel: return new Color32(0x3E, 0xA8, 0x4E, 0xFF);
                case Role.Renegade: return new Color32(0x8A, 0x4E, 0xC0, 0xFF);
                case Role.Captain: return Gold;
                default: return TextDim;
            }
        }
    }
}
