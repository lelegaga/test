using System;
using System.Collections.Generic;
using System.Text;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Events;
using Sanguo.GameModes;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>Dimmed full-screen layer with a centred panel.</summary>
    public static class Modal
    {
        public static RectTransform Open(RectTransform parent, string name, Vector2 size, Action onBackgroundClick, out RectTransform panel)
        {
            var root = UIFactory.Rect(name, parent);
            UIFactory.Stretch(root);
            var dim = UIFactory.Image(root, "Dim", UITheme.Dim, null, true);
            UIFactory.Stretch(dim.rectTransform);
            if (onBackgroundClick != null) PressHandler.On(dim, onBackgroundClick);
            var p = UIFactory.Panel(root, "Panel", UITheme.Panel);
            UIFactory.Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            panel = p.rectTransform;
            return root;
        }

        public static Text Title(RectTransform panel, string text)
        {
            var t = UIFactory.FitText(panel, "Title", text, UITheme.FontLarge, UITheme.Gold);
            UIFactory.Band(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 30, 30, 12, -72);
            return t;
        }

        public static void Close(ref RectTransform root)
        {
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
        }
    }

    /// <summary>Character selection and generic option choices (both answered with an option index).</summary>
    public sealed class ChoiceDialog
    {
        private RectTransform _root;
        private int _requestId = -1;

        public bool IsOpen => _root != null;
        public int RequestId => _requestId;

        public void ShowCharacters(RectTransform parent, RequestInfo request, GameContent content, string prompt, Action<int> choose)
        {
            Close();
            _requestId = request.RequestId;
            int count = request.Options?.Count ?? 0;
            float cardWidth = 300f;
            float width = Mathf.Min(1800f, Mathf.Max(760f, count * (cardWidth + 20f) + 60f));
            _root = Modal.Open(parent, "CharacterChoice", new Vector2(width, 800), null, out var panel);
            Modal.Title(panel, prompt);
            UIFactory.ScrollView(panel, "Characters", true, out var list, new Color(0, 0, 0, 0));
            UIFactory.Band((RectTransform)list.parent, new Vector2(0, 0), new Vector2(1, 1), 20, 20, 86, 20);
            var layout = list.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.padding = new RectOffset(10, 10, 10, 10);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                BuildCharacterCard(list, content, request.Options[i], cardWidth, () => choose(index));
            }
        }

        private static void BuildCharacterCard(RectTransform parent, GameContent content, string characterId, float width, Action onClick)
        {
            content.Characters.TryGet(characterId, out var data);
            var card = UIFactory.Image(parent, "Character " + characterId, UITheme.PanelLight, UIAssets.RoundedSmall, true);
            UIFactory.Size(card, width, -1);
            PressHandler.On(card, onClick);
            var kingdom = data?.Kingdom ?? Kingdom.None;
            var bar = UIFactory.Image(card.transform, "Kingdom", UITheme.KingdomColor(kingdom), UIAssets.RoundedSmall);
            UIFactory.Band(bar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 0, 0, 0, -64);
            var name = UIFactory.FitText(bar.transform, "Name", data?.Name ?? characterId, UITheme.FontLarge, UITheme.Text);
            UIFactory.Stretch(name.rectTransform, 10, 10, 4, 4);
            var sb = new StringBuilder();
            if (data != null)
            {
                if (data.Title.Length > 0) sb.Append("<i>").Append(data.Title).Append("</i>\n");
                sb.Append(KingdomName(kingdom)).Append("  体力 ").Append(data.MaxHp).Append('\n');
                foreach (var skillId in data.SkillIds)
                {
                    sb.Append("\n<color=#E8C16A>【").Append(content.Skills.GetName(skillId)).Append("】</color>");
                    if (content.Skills.TryGet(skillId, out var skill)) sb.Append(skill.Description);
                }
            }
            var body = UIFactory.Text(card.transform, "Body", sb.ToString(), UITheme.FontSmall, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Stretch(body.rectTransform, 16, 16, 76, 90);
            var pick = UIFactory.Button(card.transform, "Pick", "选择", onClick, UITheme.Button, UITheme.FontBody);
            UIFactory.Band((RectTransform)pick.transform, new Vector2(0, 0), new Vector2(1, 0), 30, 30, -74, 14);
        }

        public static string KingdomName(Kingdom k)
        {
            switch (k)
            {
                case Kingdom.Wei: return "魏";
                case Kingdom.Shu: return "蜀";
                case Kingdom.Wu: return "吴";
                case Kingdom.Qun: return "群";
                case Kingdom.God: return "神";
                default: return "无";
            }
        }

        public void ShowOptions(RectTransform parent, RequestInfo request, GameContent content, string prompt, Action<int> choose)
        {
            Close();
            _requestId = request.RequestId;
            int count = request.Options?.Count ?? 0;
            _root = Modal.Open(parent, "OptionChoice", new Vector2(760, Mathf.Min(900, 170 + count * 96)), null, out var panel);
            Modal.Title(panel, prompt);
            var column = UIFactory.Rect("Options", panel);
            UIFactory.Stretch(column, 40, 40, 90, 30);
            UIFactory.Column(column, 14);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                UIFactory.Size(UIFactory.Button(column, "Option " + i, OptionLabel(content, request.Options[i]), () => choose(index)), -1, 82);
            }
        }

        /// <summary>Options may be card, skill or character ids; show their names.</summary>
        public static string OptionLabel(GameContent content, string option)
        {
            if (content == null || option == null) return option ?? string.Empty;
            if (content.Cards.TryGet(option, out var card)) return "【" + card.CardName + "】";
            if (content.Skills.TryGet(option, out var skill)) return "【" + skill.Name + "】";
            if (content.Characters.TryGet(option, out var ch)) return ch.Name;
            return option;
        }

        public void Close()
        {
            Modal.Close(ref _root);
            _requestId = -1;
        }
    }

    /// <summary>
    /// Pick one card from another player's areas. Hand cards are shown face down and chosen by
    /// position only (the server maps positions onto a shuffled order), so nothing leaks.
    /// </summary>
    public sealed class PickCardDialog
    {
        private RectTransform _root;
        private int _requestId = -1;

        public bool IsOpen => _root != null;
        public int RequestId => _requestId;

        public void Show(RectTransform parent, RequestInfo request, ClientPlayerState target, GameContent content, string prompt, Action<ZoneType, int> pick)
        {
            Close();
            _requestId = request.RequestId;
            _root = Modal.Open(parent, "PickCard", new Vector2(1500, 760), null, out var panel);
            Modal.Title(panel, prompt);
            var column = UIFactory.Rect("Zones", panel);
            UIFactory.Stretch(column, 30, 30, 90, 30);
            UIFactory.Column(column, 12);
            int zones = request.Zones == 0 ? (int)ZoneMask.All : request.Zones;
            var size = new Vector2(120, 168);
            if (target == null) return;

            if ((zones & (int)ZoneMask.Hand) != 0 && target.HandCount > 0)
            {
                var row = Section(column, "手牌（" + target.HandCount + "）", size.y);
                for (int i = 0; i < target.HandCount; i++)
                {
                    int index = i;
                    var w = CardWidget.Create(row, size, true);
                    UIFactory.Size(w, size.x, size.y);
                    w.SetHidden();
                    w.SetCallbacks(() => pick(ZoneType.Hand, index), null);
                }
            }
            if ((zones & (int)ZoneMask.Equipment) != 0)
            {
                RectTransform row = null;
                for (int slot = 1; slot < target.Equipment.Length; slot++)
                {
                    var card = target.Equipment[slot];
                    if (card == null) continue;
                    if (row == null) row = Section(column, "装备区", size.y);
                    int index = slot;
                    var w = CardWidget.Create(row, size, true);
                    UIFactory.Size(w, size.x, size.y);
                    w.SetCard(card, content);
                    w.SetCallbacks(() => pick(ZoneType.Equipment, index), null);
                }
            }
            if ((zones & (int)ZoneMask.Judge) != 0 && target.JudgeArea.Count > 0)
            {
                var row = Section(column, "判定区", size.y);
                for (int i = 0; i < target.JudgeArea.Count; i++)
                {
                    int index = i;
                    var w = CardWidget.Create(row, size, true);
                    UIFactory.Size(w, size.x, size.y);
                    w.SetCard(target.JudgeArea[i], content);
                    w.SetCallbacks(() => pick(ZoneType.JudgeArea, index), null);
                }
            }
        }

        private static RectTransform Section(RectTransform column, string title, float height)
        {
            var label = UIFactory.Text(column, "Label", title, UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Size(label, -1, 32);
            UIFactory.ScrollView(column, title, true, out var row, new Color(0, 0, 0, 0.2f));
            UIFactory.Size(row.parent.GetComponent<Image>(), -1, height + 16);
            return row;
        }

        public void Close()
        {
            Modal.Close(ref _root);
            _requestId = -1;
        }
    }

    /// <summary>End of game: result for the viewer and every player's revealed role.</summary>
    public sealed class ResultPanel
    {
        private RectTransform _root;

        public bool IsOpen => _root != null;

        public void Show(RectTransform parent, ClientGameState state, GameContent content, int viewerId, Action exit, string exitLabel)
        {
            Close();
            var result = state.Result;
            bool won = result != null && !result.IsDraw && result.IsWinner(viewerId);
            string title = result == null || result.IsDraw ? "平局" : viewerId < 0 ? "游戏结束" : won ? "胜利" : "失败";
            _root = Modal.Open(parent, "Result", new Vector2(1100, 860), null, out var panel);
            var t = UIFactory.Text(panel, "Title", title, UITheme.FontTitle, won ? UITheme.Gold : UITheme.Text);
            UIFactory.Band(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 0, 0, 10, -110);
            t.gameObject.AddComponent<Outline>().effectColor = Color.black;
            var reason = UIFactory.Text(panel, "Reason", Describe(result, state), UITheme.FontBody, UITheme.TextDim);
            UIFactory.Band(reason.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 30, 30, 112, -160);

            UIFactory.ScrollView(panel, "Players", false, out var list);
            UIFactory.Band((RectTransform)list.parent, new Vector2(0, 0), new Vector2(1, 1), 30, 30, 170, 130);
            var players = new List<ClientPlayerState>(state.Players);
            players.Sort((a, b) => a.Seat.CompareTo(b.Seat));
            foreach (var p in players)
            {
                bool winner = result != null && result.IsWinner(p.PlayerId);
                var sb = new StringBuilder();
                sb.Append(p.Seat + 1).Append("号  ").Append(p.Nickname).Append("  ").Append(PlayerText.CharacterName(content, p));
                if (p.Role != Role.None) sb.Append("  ").Append(GameLogFormatter.RoleName(p.Role));
                if (p.Team != Team.None) sb.Append(p.Team == Team.A ? "  红队" : "  蓝队");
                sb.Append(p.Alive ? "" : "  (阵亡)");
                var row = UIFactory.Text(list, "Player", (winner ? "<color=#E8C16A>★ " : "<color=#B9A88C>   ") + sb + "</color>", UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
                UIFactory.Size(row, -1, 46);
            }

            var buttons = UIFactory.Rect("Buttons", panel);
            UIFactory.Band(buttons, new Vector2(0, 0), new Vector2(1, 0), 30, 30, -110, 20);
            UIFactory.Row(buttons, 24);
            UIFactory.Size(UIFactory.Button(buttons, "Exit", exitLabel, exit, UITheme.Button, UITheme.FontLarge), 360, 90);
        }

        private static string Describe(GameResult r, ClientGameState state)
        {
            if (r == null || r.IsDraw) return "达到回合上限，双方握手言和";
            switch (r.WinningFaction)
            {
                case Faction.LordSide: return "主公与忠臣获胜";
                case Faction.Rebels: return "反贼获胜";
                case Faction.Renegade: return "内奸获胜";
                case Faction.TeamA: return "红队获胜";
                case Faction.TeamB: return "蓝队获胜";
                default:
                    var winner = r.WinnerIds.Count > 0 ? state.GetPlayer(r.WinnerIds[0]) : null;
                    return winner != null ? winner.Nickname + " 获胜" : "游戏结束";
            }
        }

        public void Close() => Modal.Close(ref _root);
    }

    /// <summary>Read-only text popup (long-press help); tap anywhere to close.</summary>
    public sealed class InfoDialog
    {
        private RectTransform _root;

        public bool IsOpen => _root != null;

        public void Show(RectTransform parent, string title, string body)
        {
            Close();
            _root = Modal.Open(parent, "Info", new Vector2(900, 620), Close, out var panel);
            PressHandler.On(panel.GetComponent<Image>(), Close);
            Modal.Title(panel, title);
            var text = UIFactory.Text(panel, "Body", body, UITheme.FontBody, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Stretch(text.rectTransform, 36, 36, 90, 60);
            var hint = UIFactory.Text(panel, "Hint", "点击任意处关闭", UITheme.FontSmall, UITheme.TextDim);
            UIFactory.Band(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 0, 0, -50, 10);
        }

        public void Close() => Modal.Close(ref _root);
    }

    /// <summary>In-game menu: auto-play toggle, log toggle, leave (with confirmation).</summary>
    public sealed class GameMenu
    {
        private RectTransform _root;

        public bool IsOpen => _root != null;

        public void Show(RectTransform parent, bool autoPlay, bool logVisible, Action<bool> setAutoPlay, Action<bool> setLog, string quitLabel, Action quit)
        {
            Close();
            _root = Modal.Open(parent, "Menu", new Vector2(620, 620), Close, out var panel);
            Modal.Title(panel, "菜单");
            var column = UIFactory.Rect("Column", panel);
            UIFactory.Stretch(column, 50, 50, 100, 40);
            UIFactory.Column(column, 18);
            UIFactory.Size(UIFactory.Button(column, "Resume", "继续游戏", Close), -1, 84);
            UIFactory.Size(UIFactory.Button(column, "Auto", autoPlay ? "取消托管" : "托管（AI 代打）", () =>
            {
                setAutoPlay(!autoPlay);
                Close();
            }, UITheme.ButtonSecondary), -1, 84);
            UIFactory.Size(UIFactory.Button(column, "Log", logVisible ? "隐藏战报" : "显示战报", () =>
            {
                setLog(!logVisible);
                Close();
            }, UITheme.ButtonSecondary), -1, 84);
            UIFactory.Size(UIFactory.Button(column, "Quit", quitLabel, () =>
            {
                Close();
                ConfirmQuit(parent, quitLabel, quit);
            }, UITheme.ButtonSecondary), -1, 84);
        }

        private void ConfirmQuit(RectTransform parent, string quitLabel, Action quit)
        {
            _root = Modal.Open(parent, "ConfirmQuit", new Vector2(760, 380), Close, out var panel);
            Modal.Title(panel, quitLabel + "？");
            var text = UIFactory.Text(panel, "Body", "离开后你的座位将由 AI 接管。", UITheme.FontBody, UITheme.TextDim);
            UIFactory.Stretch(text.rectTransform, 30, 30, 90, 150);
            var buttons = UIFactory.Rect("Buttons", panel);
            UIFactory.Band(buttons, new Vector2(0, 0), new Vector2(1, 0), 30, 30, -120, 24);
            UIFactory.Row(buttons, 24);
            UIFactory.Size(UIFactory.Button(buttons, "No", "取消", Close, UITheme.ButtonSecondary), 260, 90);
            UIFactory.Size(UIFactory.Button(buttons, "Yes", "确定离开", () =>
            {
                Close();
                quit();
            }), 260, 90);
        }

        public void Close() => Modal.Close(ref _root);
    }
}
