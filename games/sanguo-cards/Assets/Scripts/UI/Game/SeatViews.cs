using System;
using System.Collections.Generic;
using System.Text;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Events;
using Sanguo.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>How a seat should be highlighted this frame.</summary>
    public struct SeatHighlight
    {
        public bool Current;
        public bool Selectable;
        public bool Selected;
        public bool TeamMode;
    }

    /// <summary>Shared display text for players (character, role, equipment, judge area).</summary>
    public static class PlayerText
    {
        public static string CharacterName(GameContent content, ClientPlayerState p)
        {
            if (string.IsNullOrEmpty(p.CharacterId)) return "未选将";
            return content != null && content.Characters.TryGet(p.CharacterId, out var c) ? c.Name : p.CharacterId;
        }

        public static Kingdom KingdomOf(GameContent content, ClientPlayerState p)
        {
            return content != null && p.CharacterId != null && content.Characters.TryGet(p.CharacterId, out var c) ? c.Kingdom : Kingdom.None;
        }

        public static string RoleShort(Role role)
        {
            switch (role)
            {
                case Role.Lord: return "主";
                case Role.Loyalist: return "忠";
                case Role.Rebel: return "反";
                case Role.Renegade: return "内";
                case Role.Captain: return "帅";
                case Role.Member: return "兵";
                case Role.Unknown: return "?";
                default: return string.Empty;
            }
        }

        public static string Equipment(GameContent content, ClientPlayerState p, bool longNames)
        {
            var sb = new StringBuilder();
            for (int slot = 1; slot < p.Equipment.Length; slot++)
            {
                var card = p.Equipment[slot];
                if (card == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                string name = content != null ? content.Cards.GetName(card.CardId) : card.CardId;
                if (longNames) sb.Append(CardWidget.SlotName((EquipSlot)slot)).Append(' ');
                sb.Append(name);
            }
            return sb.ToString();
        }

        public static string Judge(GameContent content, ClientPlayerState p)
        {
            if (p.JudgeArea.Count == 0) return string.Empty;
            var sb = new StringBuilder("判:");
            foreach (var c in p.JudgeArea)
            {
                string name = content != null ? content.Cards.GetName(c.CardId) : c.CardId;
                sb.Append(name.Length > 0 ? name.Substring(0, 1) : "?");
            }
            return sb.ToString();
        }

        public static string Tags(ClientPlayerState p)
        {
            if (!p.Connected) return "<color=#FF6A5A>离线</color>";
            return p.AIControlled ? "<color=#8FB8FF>托管</color>" : string.Empty;
        }

        /// <summary>Long-press details: character, skills with descriptions, equipment and judge area.</summary>
        public static string Details(GameContent content, ClientPlayerState p)
        {
            var sb = new StringBuilder();
            sb.Append("<b>").Append(p.Nickname).Append("</b>  ").Append(CharacterName(content, p))
                .Append("  体力 ").Append(p.Hp).Append('/').Append(p.MaxHp)
                .Append("  手牌 ").Append(p.HandCount);
            if (p.Role != Role.None && p.Role != Role.Unknown) sb.Append("  身份 ").Append(GameLogFormatter.RoleName(p.Role));
            if (content != null && p.CharacterId != null && content.Characters.TryGet(p.CharacterId, out var ch) && ch.Title.Length > 0)
                sb.Append("\n").Append(ch.Title);
            foreach (var s in p.Skills)
            {
                sb.Append("\n<color=#E8C16A>【").Append(content?.Skills.GetName(s.SkillId) ?? s.SkillId).Append("】</color>");
                if (s.UsedUp) sb.Append("（已使用）");
                if (s.Disabled) sb.Append("（失效）");
                if (content != null && content.Skills.TryGet(s.SkillId, out var def)) sb.Append(def.Description);
            }
            string equip = Equipment(content, p, true);
            if (equip.Length > 0) sb.Append("\n装备：").Append(equip.Replace('\n', '，'));
            if (p.JudgeArea.Count > 0)
            {
                sb.Append("\n判定区：");
                foreach (var c in p.JudgeArea) sb.Append('【').Append(content?.Cards.GetName(c.CardId) ?? c.CardId).Append('】');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Another player's seat. Medium/large tiers show the full card (character, role, HP pips, hand
    /// count, equipment, judge area); the small tier used on big tables is a compact avatar.
    /// </summary>
    public sealed class SeatWidget : MonoBehaviour
    {
        private const int MaxPips = 10;

        private Image _frame;
        private Image _panel;
        private Image _kingdomBar;
        private Image _teamStripe;
        private Text _character;
        private Text _nickname;
        private Text _tags;
        private Image _avatar;
        private Text _avatarText;
        private Image _roleBadge;
        private Text _roleText;
        private readonly List<Image> _pips = new List<Image>();
        private Text _hpText;
        private Text _handCount;
        private Text _equipment;
        private Text _judge;
        private Image _deadOverlay;
        private Text _deadText;
        private Image _timerFill;

        public int PlayerId { get; private set; }
        public SeatTier Tier { get; private set; }
        public RectTransform Rect { get; private set; }
        public LayoutElement Layout { get; private set; }

        public static SeatWidget Create(Transform parent, SeatTier tier, int playerId)
        {
            var rt = UIFactory.Rect("Seat " + playerId, parent);
            var w = rt.gameObject.AddComponent<SeatWidget>();
            w.Rect = rt;
            w.PlayerId = playerId;
            w.Tier = tier;
            w.Layout = UIFactory.Size(rt, SeatLayout.WidthOf(tier), -1);
            if (tier == SeatTier.Small) w.BuildCompact();
            else w.BuildFull();
            return w;
        }

        public void SetCallbacks(Action clicked, Action longPressed) => PressHandler.On(_panel, clicked, longPressed);

        private void BuildCommon()
        {
            _frame = UIFactory.Image(Rect, "Frame", UITheme.Selectable, UIAssets.RoundedSmall);
            UIFactory.Stretch(_frame.rectTransform, -6, -6, -6, -6);
            _panel = UIFactory.Image(Rect, "Panel", UITheme.PanelLight, UIAssets.RoundedSmall, true);
            UIFactory.Stretch(_panel.rectTransform);
            _teamStripe = UIFactory.Image(_panel.transform, "Team", UITheme.TeamA, UIAssets.RoundedSmall);
            UIFactory.Band(_teamStripe.rectTransform, new Vector2(0, 0), new Vector2(0, 1), 0, -8);
        }

        private void BuildFull()
        {
            BuildCommon();
            var p = _panel.transform;
            _kingdomBar = UIFactory.Image(p, "Kingdom", UITheme.KingdomColor(Kingdom.None), UIAssets.RoundedSmall);
            UIFactory.Band(_kingdomBar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 0, 0, 0, -38);
            _character = UIFactory.FitText(_kingdomBar.transform, "Character", string.Empty, UITheme.FontBody, UITheme.Text);
            UIFactory.Stretch(_character.rectTransform, 8, 40, 2, 2);
            _nickname = UIFactory.FitText(p, "Nickname", string.Empty, UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Band(_nickname.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 12, 60, 40, -68);
            _tags = UIFactory.Text(p, "Tags", string.Empty, 18, UITheme.Text, TextAnchor.MiddleRight);
            UIFactory.Band(_tags.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 60, 8, 40, -68);

            _avatar = UIFactory.Image(p, "Avatar", UITheme.KingdomColor(Kingdom.None), UIAssets.Circle);
            UIFactory.Place(_avatar.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 4), new Vector2(64, 64));
            _avatarText = UIFactory.Text(_avatar.transform, "Initial", string.Empty, 34, UITheme.Text);
            UIFactory.Stretch(_avatarText.rectTransform);
            _equipment = UIFactory.Text(p, "Equipment", string.Empty, 17, UITheme.Text, TextAnchor.UpperLeft);
            _equipment.verticalOverflow = VerticalWrapMode.Truncate;
            _equipment.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Band(_equipment.rectTransform, new Vector2(0, 0), new Vector2(1, 1), 12, 80, 72, 70);

            var pips = UIFactory.Rect("Pips", p);
            UIFactory.Band(pips, new Vector2(0, 0), new Vector2(1, 0), 10, 10, -64, 44);
            var row = UIFactory.Row(pips, 3, TextAnchor.MiddleLeft);
            row.childForceExpandHeight = false;
            for (int i = 0; i < MaxPips; i++)
            {
                var pip = UIFactory.Image(pips, "Pip", UITheme.HpHigh, UIAssets.Circle);
                UIFactory.Size(pip, 14, 14);
                _pips.Add(pip);
            }
            _hpText = UIFactory.Text(p, "Hp", string.Empty, UITheme.FontSmall, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Band(_hpText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 10, 10, -66, 42);

            _judge = UIFactory.Text(p, "Judge", string.Empty, 18, UITheme.Gold, TextAnchor.MiddleLeft);
            UIFactory.Band(_judge.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 10, 60, -40, 6);
            var hand = UIFactory.Image(p, "HandBadge", UITheme.CardBack, UIAssets.RoundedSmall);
            UIFactory.Place(hand.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-8, 8), new Vector2(40, 34));
            _handCount = UIFactory.Text(hand.transform, "Count", "0", UITheme.FontSmall, UITheme.Text);
            UIFactory.Stretch(_handCount.rectTransform);

            BuildRoleBadge(p, 38, new Vector2(-2, -2));
            BuildOverlays(p);
        }

        private void BuildCompact()
        {
            BuildCommon();
            var p = _panel.transform;
            _kingdomBar = UIFactory.Image(p, "Kingdom", UITheme.KingdomColor(Kingdom.None), UIAssets.RoundedSmall);
            UIFactory.Band(_kingdomBar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 0, 0, 0, -10);
            _avatar = UIFactory.Image(p, "Avatar", UITheme.KingdomColor(Kingdom.None), UIAssets.Circle);
            UIFactory.Place(_avatar.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(60, 60));
            _avatarText = UIFactory.Text(_avatar.transform, "Initial", string.Empty, 30, UITheme.Text);
            UIFactory.Stretch(_avatarText.rectTransform);
            _nickname = UIFactory.FitText(p, "Nickname", string.Empty, 18, UITheme.Text);
            UIFactory.Band(_nickname.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 4, 4, 82, -110);
            _hpText = UIFactory.Text(p, "Hp", string.Empty, UITheme.FontSmall, UITheme.Text);
            UIFactory.Band(_hpText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 4, 4, 112, -142);
            _judge = UIFactory.Text(p, "Judge", string.Empty, 16, UITheme.Gold);
            UIFactory.Band(_judge.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 4, 4, -64, 40);
            var hand = UIFactory.Image(p, "HandBadge", UITheme.CardBack, UIAssets.RoundedSmall);
            UIFactory.Place(hand.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(40, 30));
            _handCount = UIFactory.Text(hand.transform, "Count", "0", 20, UITheme.Text);
            UIFactory.Stretch(_handCount.rectTransform);
            BuildRoleBadge(p, 30, new Vector2(0, -2));
            BuildOverlays(p);
        }

        private void BuildRoleBadge(Transform parent, float size, Vector2 offset)
        {
            _roleBadge = UIFactory.Image(parent, "Role", UITheme.TextDim, UIAssets.Circle);
            UIFactory.Place(_roleBadge.rectTransform, new Vector2(1, 1), new Vector2(1, 1), offset, new Vector2(size, size));
            _roleText = UIFactory.Text(_roleBadge.transform, "Text", string.Empty, Mathf.RoundToInt(size * 0.6f), UITheme.Text);
            UIFactory.Stretch(_roleText.rectTransform);
        }

        private void BuildOverlays(Transform parent)
        {
            var timerBg = UIFactory.Image(parent, "Timer", new Color(0, 0, 0, 0.5f), UIAssets.White);
            UIFactory.Band(timerBg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 6, 6, -6, 0);
            _timerFill = UIFactory.Image(timerBg.transform, "Fill", UITheme.Gold, UIAssets.White);
            _timerFill.type = Image.Type.Filled;
            _timerFill.fillMethod = Image.FillMethod.Horizontal;
            UIFactory.Stretch(_timerFill.rectTransform);
            timerBg.gameObject.SetActive(false);

            _deadOverlay = UIFactory.Image(parent, "Dead", new Color(0.05f, 0.05f, 0.05f, 0.72f), UIAssets.RoundedSmall);
            UIFactory.Stretch(_deadOverlay.rectTransform);
            _deadText = UIFactory.Text(_deadOverlay.transform, "Text", "阵亡", UITheme.FontLarge, UITheme.TextDim);
            UIFactory.Stretch(_deadText.rectTransform);
            _deadOverlay.gameObject.SetActive(false);
        }

        public void Refresh(ClientPlayerState p, GameContent content, SeatHighlight h)
        {
            var kingdom = PlayerText.KingdomOf(content, p);
            var kColor = UITheme.KingdomColor(kingdom);
            string charName = PlayerText.CharacterName(content, p);
            _kingdomBar.color = kColor;
            _avatar.color = Color.Lerp(kColor, Color.black, 0.25f);
            _avatarText.text = string.IsNullOrEmpty(p.CharacterId) || charName.Length == 0 ? "?" : charName.Substring(0, 1);
            if (_character != null) _character.text = charName;
            _nickname.text = Tier == SeatTier.Small ? p.Nickname : (p.Seat + 1) + "号 " + p.Nickname;
            if (_tags != null) _tags.text = PlayerText.Tags(p);

            _teamStripe.enabled = h.TeamMode && p.Team != Team.None;
            _teamStripe.color = UITheme.TeamColor(p.Team);

            bool knownRole = p.Role != Role.None;
            _roleBadge.enabled = knownRole;
            _roleBadge.color = p.Role == Role.Unknown ? new Color(0.3f, 0.3f, 0.3f, 0.9f) : UITheme.RoleColor(p.Role);
            _roleText.text = PlayerText.RoleShort(p.Role);

            if (_pips.Count > 0 && p.MaxHp <= MaxPips)
            {
                var color = UITheme.HpColor(p.Hp, p.MaxHp);
                for (int i = 0; i < _pips.Count; i++)
                {
                    _pips[i].gameObject.SetActive(i < p.MaxHp);
                    _pips[i].color = i < p.Hp ? color : UITheme.HpEmpty;
                }
                _hpText.text = string.Empty;
            }
            else
            {
                foreach (var pip in _pips) pip.gameObject.SetActive(false);
                _hpText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(UITheme.HpColor(p.Hp, p.MaxHp)) + ">♥</color>" + p.Hp + "/" + p.MaxHp;
            }
            _handCount.text = p.HandCount.ToString();
            if (_equipment != null) _equipment.text = PlayerText.Equipment(content, p, false);
            _judge.text = PlayerText.Judge(content, p);

            _deadOverlay.gameObject.SetActive(!p.Alive);
            if (!p.Alive) _deadText.text = p.RoleRevealed && p.Role != Role.None ? "阵亡\n<size=22>" + GameLogFormatter.RoleName(p.Role) + "</size>" : "阵亡";
            _panel.color = p.IsDying ? new Color(0.55f, 0.15f, 0.12f, 0.95f) : UITheme.PanelLight;

            _frame.enabled = h.Selected || h.Selectable || h.Current;
            _frame.color = h.Selected ? UITheme.Selected : h.Selectable ? UITheme.Selectable : UITheme.CurrentTurn;
            float scale = Tier == SeatTier.Large ? 1.04f : 1f;
            Rect.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Remaining time of a request this player is answering (null hides the bar).</summary>
        public void SetTimer(float? remaining01)
        {
            var bg = _timerFill.transform.parent.gameObject;
            bool show = remaining01.HasValue;
            if (bg.activeSelf != show) bg.SetActive(show);
            if (show)
            {
                _timerFill.fillAmount = remaining01.Value;
                _timerFill.color = remaining01.Value < 0.3f ? UITheme.HpLow : UITheme.Gold;
            }
        }
    }

    /// <summary>
    /// The other players in a strip across the top of the table, ordered like a real table read
    /// from the viewer's seat: the previous player on the left, the next player on the right.
    /// Scrolls horizontally when a big table does not fit.
    /// </summary>
    public sealed class SeatStrip
    {
        private readonly ScrollRect _scroll;
        private readonly RectTransform _content;
        private readonly HorizontalLayoutGroup _layout;
        private readonly Dictionary<int, SeatWidget> _widgets = new Dictionary<int, SeatWidget>();
        private readonly List<int> _stale = new List<int>();

        public Action<int> SeatClicked;
        public Action<int> SeatLongPressed;

        public SeatStrip(RectTransform parent)
        {
            _scroll = UIFactory.ScrollView(parent, "SeatStrip", true, out _content, new Color(0, 0, 0, 0));
            UIFactory.Stretch((RectTransform)_scroll.transform);
            _layout = _content.GetComponent<HorizontalLayoutGroup>();
            _layout.spacing = 14;
            _layout.padding = new RectOffset(16, 16, 10, 10);
            _layout.childAlignment = TextAnchor.MiddleCenter;
        }

        /// <summary>Concrete collection type so per-frame iteration does not box an enumerator.</summary>
        public Dictionary<int, SeatWidget>.ValueCollection Widgets => _widgets.Values;

        public SeatWidget Get(int playerId) => _widgets.TryGetValue(playerId, out var w) ? w : null;

        public void Sync(List<SeatSlot> slots)
        {
            _stale.Clear();
            foreach (var id in _widgets.Keys) _stale.Add(id);
            float total = _layout.padding.horizontal;
            for (int i = slots.Count - 1, index = 0; i >= 0; i--, index++)
            {
                var slot = slots[i];
                if (_widgets.TryGetValue(slot.PlayerId, out var w) && w.Tier != slot.Tier)
                {
                    w.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(w.gameObject);
                    _widgets.Remove(slot.PlayerId);
                    w = null;
                }
                if (w == null)
                {
                    int id = slot.PlayerId;
                    w = SeatWidget.Create(_content, slot.Tier, id);
                    w.SetCallbacks(() => SeatClicked?.Invoke(id), () => SeatLongPressed?.Invoke(id));
                    _widgets[id] = w;
                }
                _stale.Remove(slot.PlayerId);
                w.Rect.SetSiblingIndex(index);
                total += SeatLayout.WidthOf(slot.Tier) + (index > 0 ? _layout.spacing : 0);
            }
            foreach (var id in _stale)
            {
                _widgets[id].gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_widgets[id].gameObject);
                _widgets.Remove(id);
            }

            // Centre the strip when it fits; otherwise anchor left and allow scrolling.
            float viewport = ((RectTransform)_scroll.transform).rect.width;
            bool fits = total <= viewport || viewport <= 0;
            _scroll.horizontal = !fits;
            _content.anchorMin = new Vector2(fits ? 0.5f : 0f, 0f);
            _content.anchorMax = new Vector2(fits ? 0.5f : 0f, 1f);
            _content.pivot = new Vector2(fits ? 0.5f : 0f, 0.5f);
            if (fits) _content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Scrolls so that a seat (e.g. the current player) is visible.</summary>
        public void Reveal(int playerId)
        {
            if (!_scroll.horizontal || !_widgets.TryGetValue(playerId, out var w)) return;
            var viewport = (RectTransform)_scroll.transform;
            float contentWidth = _content.rect.width - viewport.rect.width;
            if (contentWidth <= 0) return;
            float x = w.Rect.anchoredPosition.x - viewport.rect.width / 2f;
            _scroll.horizontalNormalizedPosition = Mathf.Clamp01(x / contentWidth);
        }

        public void Clear()
        {
            foreach (var w in _widgets.Values) UnityEngine.Object.Destroy(w.gameObject);
            _widgets.Clear();
        }
    }

    /// <summary>The local player's own area: character, role, HP, equipment, judge area and skills.</summary>
    public sealed class SelfPanel
    {
        private readonly RectTransform _root;
        private readonly Image _frame;
        private readonly Image _panel;
        private readonly Image _kingdomBar;
        private readonly Text _character;
        private readonly Text _role;
        private readonly Text _nickname;
        private readonly Text _hp;
        private readonly Text _equipment;
        private readonly Text _judge;
        private readonly RectTransform _skills;
        private readonly List<SkillButton> _skillButtons = new List<SkillButton>();
        private string _skillSignature = string.Empty;

        public Action Clicked;
        public Action LongPressed;
        public Action<string> SkillClicked;
        public Action<string> SkillLongPressed;

        private sealed class SkillButton
        {
            public string SkillId;
            public Image Background;
            public Text Label;
        }

        public SelfPanel(RectTransform parent)
        {
            _root = parent;
            _frame = UIFactory.Image(parent, "Frame", UITheme.Selectable, UIAssets.RoundedSmall);
            UIFactory.Stretch(_frame.rectTransform, -6, -6, -6, -6);
            _panel = UIFactory.Image(parent, "Panel", UITheme.Panel, UIAssets.RoundedSmall, true);
            UIFactory.Stretch(_panel.rectTransform);
            PressHandler.On(_panel, () => Clicked?.Invoke(), () => LongPressed?.Invoke());
            var p = _panel.transform;
            _kingdomBar = UIFactory.Image(p, "Kingdom", UITheme.KingdomColor(Kingdom.None), UIAssets.RoundedSmall);
            UIFactory.Band(_kingdomBar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 0, 0, 0, -44);
            _character = UIFactory.FitText(_kingdomBar.transform, "Character", string.Empty, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Stretch(_character.rectTransform, 14, 120, 2, 2);
            _role = UIFactory.FitText(_kingdomBar.transform, "Role", string.Empty, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleRight);
            UIFactory.Stretch(_role.rectTransform, 150, 14, 2, 2);
            _nickname = UIFactory.FitText(p, "Nickname", string.Empty, UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Band(_nickname.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 14, 14, 48, -80);
            _hp = UIFactory.Text(p, "Hp", string.Empty, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Band(_hp.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 14, 14, 82, -118);
            _equipment = UIFactory.Text(p, "Equipment", string.Empty, UITheme.FontSmall - 2, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Band(_equipment.rectTransform, new Vector2(0, 0), new Vector2(1, 1), 14, 14, 122, 104);
            _judge = UIFactory.Text(p, "Judge", string.Empty, UITheme.FontSmall, UITheme.Gold, TextAnchor.MiddleLeft);
            UIFactory.Band(_judge.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 14, 14, -104, 72);
            _skills = UIFactory.Rect("Skills", p);
            UIFactory.Band(_skills, new Vector2(0, 0), new Vector2(1, 0), 10, 10, -66, 8);
            var row = UIFactory.Row(_skills, 8, TextAnchor.MiddleLeft);
            row.childForceExpandWidth = true;
        }

        public RectTransform Rect => _root;

        public void Refresh(ClientPlayerState p, GameContent content, InteractionModel model, SeatHighlight h, int viewerId)
        {
            if (p == null)
            {
                _character.text = viewerId < 0 ? "观战中" : string.Empty;
                _role.text = _nickname.text = _hp.text = _equipment.text = _judge.text = string.Empty;
                _frame.enabled = false;
                SyncSkills(null, content, model);
                return;
            }
            _kingdomBar.color = UITheme.KingdomColor(PlayerText.KingdomOf(content, p));
            _character.text = PlayerText.CharacterName(content, p);
            _role.text = p.Role != Role.None && p.Role != Role.Unknown
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(UITheme.RoleColor(p.Role)) + ">" + GameLogFormatter.RoleName(p.Role) + "</color>"
                : string.Empty;
            _nickname.text = (p.Seat + 1) + "号 " + p.Nickname + (h.TeamMode && p.Team != Team.None ? (p.Team == Team.A ? "  红队" : "  蓝队") : "")
                             + "  " + PlayerText.Tags(p);
            var hpColor = ColorUtility.ToHtmlStringRGB(UITheme.HpColor(p.Hp, p.MaxHp));
            var sb = new StringBuilder("<color=#").Append(hpColor).Append('>');
            if (p.MaxHp <= 10)
            {
                for (int i = 0; i < p.MaxHp; i++)
                {
                    if (i == p.Hp) sb.Append("</color><color=#3A332E>");
                    sb.Append('●');
                }
            }
            sb.Append("</color> ").Append(p.Hp).Append('/').Append(p.MaxHp);
            if (!p.Alive) sb.Append("  <color=#FF6A5A>阵亡</color>");
            else if (p.IsDying) sb.Append("  <color=#FF6A5A>濒死</color>");
            _hp.text = sb.ToString();
            string equip = PlayerText.Equipment(content, p, true);
            _equipment.text = equip.Length > 0 ? equip : "<color=#7A6A58>（无装备）</color>";
            _judge.text = PlayerText.Judge(content, p);
            _panel.color = p.IsDying ? new Color(0.45f, 0.12f, 0.1f, 0.95f) : UITheme.Panel;
            _frame.enabled = h.Selected || h.Selectable || h.Current;
            _frame.color = h.Selected ? UITheme.Selected : h.Selectable ? UITheme.Selectable : UITheme.CurrentTurn;
            SyncSkills(p, content, model);
        }

        private void SyncSkills(ClientPlayerState p, GameContent content, InteractionModel model)
        {
            string signature = string.Empty;
            if (p != null)
                foreach (var s in p.Skills) signature += s.SkillId + ";";
            if (signature != _skillSignature)
            {
                _skillSignature = signature;
                UIFactory.DestroyChildren(_skills);
                _skillButtons.Clear();
                if (p != null)
                {
                    foreach (var s in p.Skills)
                    {
                        string id = s.SkillId;
                        var bg = UIFactory.Image(_skills, "Skill " + id, UITheme.ButtonSecondary, UIAssets.RoundedSmall, true);
                        UIFactory.Size(bg, 100, -1, 1);
                        var label = UIFactory.FitText(bg.transform, "Label", string.Empty, UITheme.FontSmall, UITheme.Text);
                        UIFactory.Stretch(label.rectTransform, 4, 4, 2, 2);
                        PressHandler.On(bg, () => SkillClicked?.Invoke(id), () => SkillLongPressed?.Invoke(id));
                        _skillButtons.Add(new SkillButton { SkillId = id, Background = bg, Label = label });
                    }
                }
            }
            if (p == null) return;
            foreach (var b in _skillButtons)
            {
                var state = p.FindSkill(b.SkillId);
                string name = content?.Skills.GetName(b.SkillId) ?? b.SkillId;
                string tag = string.Empty;
                if (content != null && content.Skills.TryGet(b.SkillId, out var def))
                {
                    if (def.IsLordSkill) tag = "主";
                    else if (def.IsLimited) tag = "限";
                    else if (def.IsLocked) tag = "锁";
                }
                b.Label.text = tag.Length > 0 ? name + "<size=16>·" + tag + "</size>" : name;
                bool active = model != null && (model.ActiveSkillId == b.SkillId || model.ConversionSkillId == b.SkillId);
                bool usable = model != null && model.IsSkillUsable(b.SkillId);
                bool spent = state != null && (state.UsedUp || state.Disabled);
                b.Background.color = active ? UITheme.Selected : usable ? UITheme.Button : spent ? UITheme.ButtonDisabled : UITheme.ButtonSecondary;
                b.Label.color = spent ? UITheme.TextDim : UITheme.Text;
            }
        }
    }
}
