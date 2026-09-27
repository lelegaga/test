using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>
    /// Builds UGUI widgets in code (no prefabs or scene YAML needed), so the whole UI is versioned as
    /// C#, reviewable, and identical on every platform.
    /// </summary>
    public static class UIFactory
    {
        public const int UILayer = 5;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Fills the parent with the given margins (left, right, top, bottom).</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors to a single point of the parent with a fixed size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Anchors between two normalized points of the parent (a band), with margins.</summary>
        public static RectTransform Band(RectTransform rt, Vector2 min, Vector2 max, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color) => Image(parent, name, color, UIAssets.Rounded, true);

        public static Text Text(Transform parent, string name, string content, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UIAssets.Font;
            t.text = content ?? string.Empty;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        /// <summary>Text that shrinks to fit its box (names, card titles).</summary>
        public static Text FitText(Transform parent, string name, string content, int maxSize, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = Text(parent, name, content, maxSize, color, align);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(10, maxSize / 3);
            t.resizeTextMaxSize = maxSize;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Action onClick, Color? color = null, int fontSize = UITheme.FontBody)
        {
            var img = Image(parent, name, color ?? UITheme.Button, UIAssets.Rounded, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            btn.colors = colors;
            var t = FitText(img.transform, "Label", label, fontSize, UITheme.Text);
            Stretch(t.rectTransform, 10, 10, 4, 4);
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static void SetLabel(Button button, string label)
        {
            var t = button.GetComponentInChildren<Text>();
            if (t != null && t.text != label) t.text = label;
        }

        public static InputField Input(Transform parent, string name, string placeholder, string value, int fontSize = UITheme.FontBody)
        {
            var bg = Image(parent, name, new Color(1, 1, 1, 0.92f), UIAssets.RoundedSmall, true);
            var input = bg.gameObject.AddComponent<InputField>();
            var text = Text(bg.transform, "Text", string.Empty, fontSize, UITheme.SuitBlack, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Stretch(text.rectTransform, 16, 16, 6, 6);
            var ph = Text(bg.transform, "Placeholder", placeholder, fontSize, new Color(0.4f, 0.4f, 0.4f, 0.8f), TextAnchor.MiddleLeft);
            ph.fontStyle = FontStyle.Italic;
            Stretch(ph.rectTransform, 16, 16, 6, 6);
            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = bg;
            input.text = value ?? string.Empty;
            return input;
        }

        public static Slider Slider(Transform parent, string name, float value, Action<float> onChange)
        {
            var root = Rect(name, parent);
            var slider = root.gameObject.AddComponent<Slider>();
            var bg = Image(root, "Background", UITheme.HpEmpty, UIAssets.RoundedSmall);
            Band(bg.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 0.65f));
            var fillArea = Rect("Fill Area", root);
            Band(fillArea, new Vector2(0, 0.35f), new Vector2(1, 0.65f), 10, 10);
            var fill = Image(fillArea, "Fill", UITheme.Gold, UIAssets.RoundedSmall);
            Stretch(fill.rectTransform);
            var handleArea = Rect("Handle Slide Area", root);
            Stretch(handleArea, 16, 16);
            var handle = Image(handleArea, "Handle", UITheme.Text, UIAssets.Circle, true);
            handle.rectTransform.sizeDelta = new Vector2(40, 0);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        /// <summary>Scroll view with a masked viewport and an auto-sizing content container.</summary>
        public static ScrollRect ScrollView(Transform parent, string name, bool horizontal, out RectTransform content, Color? background = null)
        {
            var bg = Image(parent, name, background ?? new Color(0, 0, 0, 0.25f), UIAssets.RoundedSmall, true);
            bg.gameObject.AddComponent<RectMask2D>();
            var scroll = bg.gameObject.AddComponent<ScrollRect>();
            content = Rect("Content", bg.transform);
            if (horizontal)
            {
                content.anchorMin = new Vector2(0, 0);
                content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 0.5f);
                content.sizeDelta = Vector2.zero;
                var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 8;
                layout.padding = new RectOffset(8, 8, 6, 6);
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1);
                content.sizeDelta = Vector2.zero;
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 6;
                layout.padding = new RectOffset(10, 10, 8, 8);
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            scroll.content = content;
            scroll.viewport = bg.rectTransform;
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            return scroll;
        }

        public static LayoutElement Size(Component c, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1)
        {
            // Not "??": in the editor a missing component is a fake-null object.
            if (!c.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            if (preferredHeight > 0) le.minHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return h;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        /// <summary>
        /// "◀ value ▶" option picker (friendlier on touch screens than a dropdown). Returns a setter for the
        /// displayed index.
        /// </summary>
        public static Action<int> Stepper(Transform parent, string label, IList<string> options, int index, Action<int> onChange, float width = 560)
        {
            var row = Rect("Stepper " + label, parent);
            Size(row, width, 64);
            Row(row, 10);
            var title = Text(row, "Title", label, UITheme.FontBody, UITheme.TextDim, TextAnchor.MiddleLeft);
            Size(title, 200, 64);
            int current = Mathf.Clamp(index, 0, Math.Max(0, options.Count - 1));
            Text value = null;
            Action<int> set = i =>
            {
                current = options.Count == 0 ? 0 : (i % options.Count + options.Count) % options.Count;
                if (value != null) value.text = options.Count > 0 ? options[current] : "-";
            };
            var prev = Button(row, "Prev", "◀", () =>
            {
                set(current - 1);
                onChange?.Invoke(current);
            }, UITheme.ButtonSecondary);
            Size(prev, 64, 64);
            value = FitText(row, "Value", string.Empty, UITheme.FontBody, UITheme.Text);
            Size(value, width - 360, 64);
            var next = Button(row, "Next", "▶", () =>
            {
                set(current + 1);
                onChange?.Invoke(current);
            }, UITheme.ButtonSecondary);
            Size(next, 64, 64);
            set(current);
            return set;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChange, float width = 560)
        {
            var row = Rect("Toggle " + label, parent);
            Size(row, width, 64);
            var toggle = row.gameObject.AddComponent<Toggle>();
            var box = Image(row, "Box", UITheme.ButtonSecondary, UIAssets.RoundedSmall, true);
            Place(box.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(52, 52));
            var check = Image(box.transform, "Check", UITheme.Gold, UIAssets.RoundedSmall);
            Stretch(check.rectTransform, 10, 10, 10, 10);
            var text = Text(row, "Label", label, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 70);
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = value;
            if (onChange != null) toggle.onValueChanged.AddListener(v => onChange(v));
            return toggle;
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                // Destroy is deferred to the end of the frame; deactivate first so layouts ignore the old rows now.
                var child = t.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }
}
