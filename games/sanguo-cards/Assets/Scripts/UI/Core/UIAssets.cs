using System.Collections.Generic;
using UnityEngine;

namespace Sanguo.UI
{
    /// <summary>
    /// Runtime-generated placeholder assets: a CJK-capable font taken from the operating system (no
    /// large font file in the build, no licensing issue) and procedural rounded sprites.
    /// </summary>
    public static class UIAssets
    {
        private static Font _font;
        private static Sprite _rounded;
        private static Sprite _roundedSmall;
        private static Sprite _circle;
        private static Sprite _white;

        /// <summary>OS fonts with Chinese glyphs, in preference order (Android, iOS/macOS, Windows, Linux).</summary>
        private static readonly string[] FontCandidates =
        {
            "Noto Sans CJK SC", "NotoSansCJK-Regular", "Source Han Sans SC", "Droid Sans Fallback", "DroidSansFallback",
            "PingFang SC", "Heiti SC", "STHeiti", "Hiragino Sans GB",
            "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun",
            "WenQuanYi Micro Hei", "Noto Sans SC", "Arial Unicode MS", "Roboto", "Arial"
        };

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                var chosen = new List<string>();
                foreach (var name in FontCandidates)
                    if (installed.Contains(name)) chosen.Add(name);
                if (chosen.Count == 0) chosen.AddRange(FontCandidates);
                _font = Font.CreateDynamicFontFromOSFont(chosen.ToArray(), 32);
                if (_font == null) _font = LoadBuiltinFont();
                return _font;
            }
        }

        private static Font LoadBuiltinFont()
        {
            // Unity 2022.2+ ships LegacyRuntime.ttf; older versions Arial.ttf.
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        /// <summary>9-sliced rounded rectangle (radius 24 px).</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = CreateRounded(96, 24);

        /// <summary>Tighter rounding for small widgets (radius 10 px).</summary>
        public static Sprite RoundedSmall => _roundedSmall != null ? _roundedSmall : _roundedSmall = CreateRounded(48, 10);

        public static Sprite Circle => _circle != null ? _circle : _circle = CreateCircle(128);

        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "SanguoWhite" };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
                return _white;
            }
        }

        private static Sprite CreateRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SanguoRounded" + radius, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SanguoCircle", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
