using System;
using System.Collections.Generic;
using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>Card face (or back) built from UGUI primitives; reused through pools.</summary>
    public sealed class CardWidget : MonoBehaviour
    {
        public static readonly Vector2 DefaultSize = new Vector2(150, 210);

        private Image _frame;
        private Image _face;
        private Text _corner;
        private Text _name;
        private Text _type;
        private Text _caption;
        private CanvasGroup _group;
        private PressHandler _press;

        public RectTransform Rect { get; private set; }
        public CanvasGroup Group => _group;
        public int InstanceId { get; private set; } = -1;
        public string CardId { get; private set; }

        public static CardWidget Create(Transform parent, Vector2 size, bool interactive)
        {
            var rt = UIFactory.Rect("Card", parent);
            rt.sizeDelta = size;
            var w = rt.gameObject.AddComponent<CardWidget>();
            w.Rect = rt;
            w._group = rt.gameObject.AddComponent<CanvasGroup>();
            w._group.blocksRaycasts = interactive;
            float k = size.x / DefaultSize.x;
            w._frame = UIFactory.Image(rt, "Frame", UITheme.Selectable, UIAssets.RoundedSmall);
            UIFactory.Stretch(w._frame.rectTransform, -6 * k, -6 * k, -6 * k, -6 * k);
            w._face = UIFactory.Image(rt, "Face", UITheme.CardFace, UIAssets.RoundedSmall, interactive);
            UIFactory.Stretch(w._face.rectTransform);
            w._corner = UIFactory.Text(w._face.transform, "Corner", string.Empty, Mathf.RoundToInt(28 * k), UITheme.SuitBlack, TextAnchor.UpperLeft);
            UIFactory.Stretch(w._corner.rectTransform, 10 * k, 10 * k, 6 * k, 0);
            w._name = UIFactory.FitText(w._face.transform, "Name", string.Empty, Mathf.RoundToInt(38 * k), UITheme.SuitBlack);
            UIFactory.Band(w._name.rectTransform, new Vector2(0, 0.3f), new Vector2(1, 0.78f), 8 * k, 8 * k);
            w._type = UIFactory.FitText(w._face.transform, "Type", string.Empty, Mathf.RoundToInt(20 * k), new Color(0.35f, 0.3f, 0.25f), TextAnchor.LowerCenter);
            UIFactory.Band(w._type.rectTransform, new Vector2(0, 0), new Vector2(1, 0.3f), 6 * k, 6 * k, 0, 8 * k);
            w._caption = UIFactory.FitText(rt, "Caption", string.Empty, Mathf.RoundToInt(22 * k), UITheme.Gold);
            UIFactory.Band(w._caption.rectTransform, new Vector2(0, 0), new Vector2(1, 0), -20, -20, -4, -34 * k);
            w._caption.gameObject.AddComponent<Outline>().effectColor = Color.black;
            if (interactive) w._press = PressHandler.On(w._face, null);
            w.SetHighlight(false, false);
            return w;
        }

        public void SetCallbacks(Action clicked, Action longPressed)
        {
            if (_press == null) return;
            _press.Clicked = clicked;
            _press.LongPressed = longPressed;
        }

        public void SetCard(CardInfo card, GameContent content)
        {
            InstanceId = card?.InstanceId ?? -1;
            CardId = card?.CardId;
            if (card == null || card.CardId == null)
            {
                SetHidden();
                return;
            }
            _face.color = UITheme.CardFace;
            bool red = CardText.IsRed(card.Suit);
            _corner.color = red ? UITheme.SuitRed : UITheme.SuitBlack;
            _corner.text = CardText.SuitSymbol(card.Suit) + CardText.NumberText(card.Number);
            _name.color = UITheme.SuitBlack;
            _name.text = content != null ? content.Cards.GetName(card.CardId) : card.CardId;
            _type.text = TypeLabel(content, card.CardId);
            _caption.text = string.Empty;
        }

        public void SetHidden()
        {
            _face.color = UITheme.CardBack;
            _corner.text = string.Empty;
            _name.color = UITheme.Gold;
            _name.text = "三国";
            _type.text = string.Empty;
            _caption.text = string.Empty;
        }

        /// <summary>Small label under the card ("刘备 使用", "当作【杀】"...).</summary>
        public void SetCaption(string caption) => _caption.text = caption ?? string.Empty;

        public void SetHighlight(bool selectable, bool selected)
        {
            _frame.enabled = selectable || selected;
            _frame.color = selected ? UITheme.Selected : UITheme.Selectable;
        }

        public void SetDimmed(bool dimmed) => _group.alpha = dimmed ? 0.5f : 1f;

        public void SetAlpha(float alpha) => _group.alpha = alpha;

        public static string TypeLabel(GameContent content, string cardId)
        {
            if (content == null || !content.Cards.TryGet(cardId, out var card)) return string.Empty;
            switch (card)
            {
                case EquipmentCard e: return "装备·" + SlotName(e.Slot);
                case TrickCard t: return t.IsDelayed ? "延时锦囊" : "锦囊";
                default: return "基本牌";
            }
        }

        public static string SlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Weapon: return "武器";
                case EquipSlot.Armor: return "防具";
                case EquipSlot.DefensiveMount: return "+1马";
                case EquipSlot.OffensiveMount: return "-1马";
                case EquipSlot.Treasure: return "宝物";
                default: return string.Empty;
            }
        }

        /// <summary>Full text for long-press help.</summary>
        public static string Describe(GameContent content, CardInfo card)
        {
            if (card == null || card.CardId == null) return "未知的牌";
            string head = CardText.SuitSymbol(card.Suit) + CardText.NumberText(card.Number) + " 【" + content.Cards.GetName(card.CardId) + "】 " + TypeLabel(content, card.CardId);
            return content.Cards.TryGet(card.CardId, out var def) && def.Description.Length > 0 ? head + "\n" + def.Description : head;
        }
    }

    /// <summary>
    /// The local player's hand. Cards keep their widget while in hand and glide to their slot
    /// (overlapping when the hand is wide); selected cards rise, unusable ones dim during a choice.
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        private const float Raise = 36f;
        private readonly Dictionary<int, CardWidget> _widgets = new Dictionary<int, CardWidget>();
        private readonly List<CardWidget> _order = new List<CardWidget>();
        private readonly List<int> _stale = new List<int>();
        private readonly HashSet<int> _raised = new HashSet<int>();
        private UIPool<CardWidget> _pool;
        private RectTransform _rect;
        private Vector2 _cardSize;

        public Action<int> CardClicked;
        public Action<CardInfo> CardLongPressed;

        public static HandView Create(RectTransform parent, Vector2 cardSize)
        {
            var hv = parent.gameObject.AddComponent<HandView>();
            hv._rect = parent;
            hv._cardSize = cardSize;
            hv._pool = new UIPool<CardWidget>(() => CardWidget.Create(parent, cardSize, true));
            return hv;
        }

        public int Count => _order.Count;

        /// <summary>Screen position of the hand centre (target of draw animations).</summary>
        public Vector3 CenterWorld => _rect.TransformPoint(_rect.rect.center);

        public Vector3? CardWorldPosition(int instanceId)
        {
            return _widgets.TryGetValue(instanceId, out var w) ? w.Rect.position : (Vector3?)null;
        }

        public void Sync(List<CardInfo> hand, GameContent content, InteractionModel model)
        {
            _stale.Clear();
            foreach (var id in _widgets.Keys) _stale.Add(id);
            _order.Clear();
            if (hand != null)
            {
                foreach (var card in hand)
                {
                    if (!_widgets.TryGetValue(card.InstanceId, out var w))
                    {
                        w = _pool.Get();
                        w.SetCard(card, content);
                        var captured = card;
                        w.SetCallbacks(() => CardClicked?.Invoke(captured.InstanceId), () => CardLongPressed?.Invoke(captured));
                        // New cards appear from below the hand.
                        w.Rect.anchorMin = w.Rect.anchorMax = new Vector2(0, 0.5f);
                        w.Rect.anchoredPosition = new Vector2(_rect.rect.width, -_cardSize.y);
                        _widgets[card.InstanceId] = w;
                    }
                    _stale.Remove(card.InstanceId);
                    _order.Add(w);
                }
            }
            foreach (var id in _stale)
            {
                _pool.Release(_widgets[id]);
                _widgets.Remove(id);
            }
            _raised.Clear();
            if (model != null)
                foreach (var id in model.SelectedCards) _raised.Add(id);
            bool choosing = model != null && model.Request != null && !model.Submitted;
            for (int i = 0; i < _order.Count; i++)
            {
                var w = _order[i];
                w.Rect.SetSiblingIndex(i);
                bool selectable = model != null && model.IsCardSelectable(w.InstanceId);
                bool selected = model != null && model.IsCardSelected(w.InstanceId);
                w.SetHighlight(selectable && !selected, selected);
                w.SetDimmed(choosing && !selectable && !selected);
            }
        }

        private void Update()
        {
            int n = _order.Count;
            if (n == 0) return;
            float width = _rect.rect.width;
            float cw = _cardSize.x;
            float spacing = n > 1 ? Mathf.Min(cw + 10f, (width - cw) / (n - 1)) : 0f;
            float t = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (int i = 0; i < n; i++)
            {
                var w = _order[i];
                var target = new Vector2(cw / 2f + i * spacing, _raised.Contains(w.InstanceId) ? Raise : 0f);
                var pos = w.Rect.anchoredPosition;
                w.Rect.anchoredPosition = (pos - target).sqrMagnitude < 0.25f ? target : Vector2.Lerp(pos, target, t);
            }
        }

    }
}
