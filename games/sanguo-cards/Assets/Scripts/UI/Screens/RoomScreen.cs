using System;
using System.Collections.Generic;
using System.Text;
using Sanguo.App;
using Sanguo.Core;
using Sanguo.GameModes;
using Sanguo.Network;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>
    /// Room lobby: seats, ready state, host controls (kick, host transfer, settings, start), teams,
    /// and a small chat. Everything shown comes from the server's <see cref="RoomState"/>; buttons
    /// only send requests that the host server validates.
    /// </summary>
    public sealed class RoomScreen : UIScreen
    {
        private GameClient _client;
        private Text _title;
        private Text _subtitle;
        private const int SlotColumns = 4;
        private RectTransform _slots;
        private GridLayoutGroup _grid;
        private Text _settingsText;
        private Button _settingsButton;
        private Button _readyButton;
        private Button _startButton;
        private Text _connecting;
        private Text _chatText;
        private InputField _chatInput;
        private RectTransform _dialog;
        private ConfigForm _form;
        private readonly List<string> _chat = new List<string>();
        private bool _dirty;

        protected override void Build(RectTransform root)
        {
            _title = UIFactory.FitText(root, "Title", string.Empty, UITheme.FontLarge + 8, UITheme.Gold, TextAnchor.MiddleLeft);
            UIFactory.Band(_title.rectTransform, new Vector2(0, 1), new Vector2(0.65f, 1), 40, 0, 12, -72);
            _subtitle = UIFactory.Text(root, "Subtitle", string.Empty, UITheme.FontBody, UITheme.TextDim, TextAnchor.MiddleRight);
            UIFactory.Band(_subtitle.rectTransform, new Vector2(0.45f, 1), new Vector2(1, 1), 0, 40, 18, -66);

            var seatsPanel = UIFactory.Panel(root, "SeatsPanel", UITheme.Panel);
            UIFactory.Band(seatsPanel.rectTransform, new Vector2(0, 0), new Vector2(0.68f, 1), 40, 12, 96, 150);
            UIFactory.ScrollView(seatsPanel.transform, "Seats", false, out _slots, new Color(0, 0, 0, 0));
            UIFactory.Stretch((RectTransform)_slots.parent, 12, 12, 12, 12);
            // Layout groups are exclusive per object, so the list layout must be gone before the grid is added.
            UnityEngine.Object.DestroyImmediate(_slots.GetComponent<VerticalLayoutGroup>());
            _grid = _slots.gameObject.AddComponent<GridLayoutGroup>();
            _grid.cellSize = new Vector2(300, 132);
            _grid.spacing = new Vector2(12, 12);
            _grid.padding = new RectOffset(8, 8, 8, 8);
            _grid.childAlignment = TextAnchor.UpperCenter;
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = SlotColumns;

            var side = UIFactory.Panel(root, "SidePanel", UITheme.Panel);
            UIFactory.Band(side.rectTransform, new Vector2(0.68f, 0), new Vector2(1, 1), 12, 40, 96, 150);
            var settingsTitle = UIFactory.Text(side.transform, "SettingsTitle", "房间设置", UITheme.FontLarge, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Band(settingsTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 24, 190, 8, -64);
            _settingsButton = UIFactory.Button(side.transform, "Edit", "修改", OpenSettings, UITheme.ButtonSecondary);
            UIFactory.Place((RectTransform)_settingsButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -10), new Vector2(150, 56));
            _settingsText = UIFactory.Text(side.transform, "Settings", string.Empty, UITheme.FontSmall, UITheme.TextDim, TextAnchor.UpperLeft);
            UIFactory.Band(_settingsText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), 24, 24, 76, 0);

            var chatTitle = UIFactory.Text(side.transform, "ChatTitle", "聊天", UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Band(chatTitle.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), 24, 24, -4, -44);
            _chatText = UIFactory.Text(side.transform, "Chat", string.Empty, UITheme.FontSmall, UITheme.Text, TextAnchor.LowerLeft);
            UIFactory.Band(_chatText.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), 24, 24, 50, 90);
            _chatInput = UIFactory.Input(side.transform, "ChatInput", "说点什么…", string.Empty, UITheme.FontSmall);
            _chatInput.characterLimit = 100;
            UIFactory.Band((RectTransform)_chatInput.transform, new Vector2(0, 0), new Vector2(1, 0), 20, 140, -76, 16);
            var send = UIFactory.Button(side.transform, "Send", "发送", SendChat, UITheme.ButtonSecondary, UITheme.FontSmall);
            UIFactory.Place((RectTransform)send.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 16), new Vector2(110, 60));

            var buttons = UIFactory.Rect("Buttons", root);
            UIFactory.Band(buttons, new Vector2(0, 0), new Vector2(1, 0), 40, 40, -130, 30);
            UIFactory.Row(buttons, 24);
            UIFactory.Size(UIFactory.Button(buttons, "Leave", "离开房间", Leave, UITheme.ButtonSecondary), 300, 96);
            _readyButton = UIFactory.Button(buttons, "Ready", "准备", ToggleReady, UITheme.Button, UITheme.FontLarge);
            UIFactory.Size(_readyButton, 340, 96);
            _startButton = UIFactory.Button(buttons, "Start", "开始游戏", StartGame, UITheme.Button, UITheme.FontLarge);
            UIFactory.Size(_startButton, 340, 96);

            _connecting = UIFactory.Text(root, "Connecting", string.Empty, UITheme.FontLarge, UITheme.Text);
            UIFactory.Stretch(_connecting.rectTransform);
            _connecting.gameObject.AddComponent<Outline>().effectColor = Color.black;
        }

        /// <summary>Shows the lobby of a client that is connecting or connected.</summary>
        public void Attach(GameClient client)
        {
            Detach();
            _client = client;
            if (_client == null) return;
            _client.RoomChanged += OnRoomChanged;
            _client.JoinRejected += OnJoinRejected;
            _client.ServerMessage += OnServerMessage;
            _client.Disconnected += OnDisconnected;
            _client.ChatReceived += OnChat;
            _dirty = true;
        }

        private void Detach()
        {
            if (_client == null) return;
            _client.RoomChanged -= OnRoomChanged;
            _client.JoinRejected -= OnJoinRejected;
            _client.ServerMessage -= OnServerMessage;
            _client.Disconnected -= OnDisconnected;
            _client.ChatReceived -= OnChat;
            _client = null;
        }

        public override void OnShow() => _dirty = true;

        public override void OnHide()
        {
            CloseSettings();
            Detach();
        }

        public override void OnBack()
        {
            if (_dialog != null) CloseSettings();
            else Leave();
        }

        private void OnRoomChanged(RoomState room) => _dirty = true;

        private void OnJoinRejected(string code, string message)
        {
            UI.Toast("无法加入：" + message);
            UI.Show<LanScreen>();
        }

        private void OnServerMessage(string code, string message)
        {
            if (!string.IsNullOrEmpty(message)) UI.Toast(message);
        }

        private void OnDisconnected(string reason)
        {
            UI.Toast("已断开：" + (string.IsNullOrEmpty(reason) ? "连接丢失" : reason));
            UI.Show<LanScreen>();
        }

        private void OnChat(int slotId, string text)
        {
            string name = slotId >= 0 ? _client?.Room?.GetSlot(slotId)?.Nickname ?? ("座位" + (slotId + 1)) : "观众";
            _chat.Add("<color=#E8C16A>" + name + "</color>：" + text);
            if (_chat.Count > 8) _chat.RemoveAt(0);
            _chatText.text = string.Join("\n", _chat);
        }

        public override void Tick()
        {
            if (_client == null) return;
            var room = _client.Room;
            if (room != null && room.InGame && _client.Game != null && !_client.Game.IsGameOver)
            {
                EnterGame();
                return;
            }
            UpdateConnectionOverlay();
            if (!_dirty || room == null) return;
            _dirty = false;
            Refresh(room);
        }

        private void UpdateConnectionOverlay()
        {
            string text;
            switch (_client.Status)
            {
                case ClientConnectionStatus.Connecting: text = "正在连接房间…"; break;
                case ClientConnectionStatus.Reconnecting: text = "连接中断，正在重连…"; break;
                case ClientConnectionStatus.InGame when _client.Game == null: text = "正在同步游戏…"; break;
                default: text = string.Empty; break;
            }
            if (_connecting.text != text) _connecting.text = text;
        }

        private void EnterGame()
        {
            var client = _client;
            var config = client.Room.Settings.Config.Clone();
            config.PlayerCount = client.Room.Slots.Count;
            var game = UI.Show<GameScreen>();
            game.Attach(new NetworkGameView(client), config, () =>
            {
                if (client.Status == ClientConnectionStatus.Disconnected) UI.Show<LanScreen>();
                else UI.Show<RoomScreen>().Attach(client);
            }, () =>
            {
                GameManager.Instance.Network.LeaveCurrent();
                UI.Show<LanScreen>();
            });
        }

        private void Refresh(RoomState room)
        {
            bool host = _client.IsHost;
            var config = room.Settings.Config;
            bool teamMode = ModeOptions.IsTeamMode(config.ModeId);
            _title.text = room.Settings.RoomName;
            var address = LocalNetwork.GetLanIPv4();
            _subtitle.text = "邀请码 <color=#E8C16A>" + FormatInvite(room.InviteCode) + "</color>"
                             + (GameManager.Instance.Network.IsHosting && address != null && !System.Net.IPAddress.IsLoopback(address) ? "  ·  IP " + address : "")
                             + (room.SpectatorCount > 0 ? "  ·  观众 " + room.SpectatorCount : "");

            // Fit four columns into whatever width the safe area leaves.
            float width = ((RectTransform)_slots.parent).rect.width;
            if (width > 0)
            {
                float cell = Mathf.Floor((width - _grid.padding.horizontal - _grid.spacing.x * (SlotColumns - 1)) / SlotColumns);
                _grid.cellSize = new Vector2(Mathf.Max(200, cell), 132);
            }
            UIFactory.DestroyChildren(_slots);
            foreach (var slot in room.Slots) BuildSlot(room, slot, host, teamMode);

            var sb = new StringBuilder();
            sb.Append(ModeOptions.Describe(config)).Append('\n');
            if (config.ModeId == IdentityMode.Id)
            {
                var roles = config.Roles.Count > 0 ? config.Roles : IdentityMode.DefaultRoles(room.Slots.Count);
                sb.Append("身份：");
                foreach (var r in roles)
                    if (r.Count > 0) sb.Append(Events.GameLogFormatter.RoleName(r.Role)).Append('×').Append(r.Count).Append(' ');
                sb.Append('\n');
            }
            sb.Append(room.Settings.FillWithAI ? "空位由 AI 补齐" : "需坐满才能开始").Append('\n');
            sb.Append(config.AllowSpectators ? "允许观战" : "禁止观战").Append('\n');
            sb.Append("掉线 ").Append(config.DisconnectAITakeoverMs / 1000).Append(" 秒后由 AI 托管");
            _settingsText.text = sb.ToString();
            _settingsButton.gameObject.SetActive(host);

            var self = room.GetSlot(_client.SlotId);
            _readyButton.gameObject.SetActive(!host && self != null);
            if (self != null) UIFactory.SetLabel(_readyButton, self.Ready ? "取消准备" : "准备");
            _startButton.gameObject.SetActive(host);
            _startButton.interactable = host && CanStart(room);
            if (_client.IsSpectator) _title.text += "（观战）";
        }

        private static bool CanStart(RoomState room)
        {
            foreach (var s in room.Slots)
                if (s.Occupied && s.SlotId != room.HostSlotId && !s.Ready) return false;
            return room.Settings.FillWithAI || room.HumanCount == room.Slots.Count;
        }

        private static string FormatInvite(string code)
        {
            if (string.IsNullOrEmpty(code)) return "-";
            return code.Length == 8 ? code.Substring(0, 4) + "-" + code.Substring(4) : code;
        }

        private void BuildSlot(RoomState room, RoomSlot slot, bool host, bool teamMode)
        {
            bool mine = slot.SlotId == _client.SlotId;
            var bg = UIFactory.Image(_slots, "Slot " + slot.SlotId, slot.Occupied ? UITheme.PanelLight : new Color(1, 1, 1, 0.06f), UIAssets.RoundedSmall, true);
            if (mine) bg.gameObject.AddComponent<Outline>().effectColor = UITheme.Gold;
            if (teamMode && slot.Team != Team.None)
            {
                var stripe = UIFactory.Image(bg.transform, "Team", UITheme.TeamColor(slot.Team), UIAssets.RoundedSmall);
                UIFactory.Band(stripe.rectTransform, new Vector2(0, 0), new Vector2(0, 1), 0, -10, 0, 0);
            }
            var seat = UIFactory.Text(bg.transform, "Seat", (slot.SlotId + 1).ToString(), UITheme.FontLarge, UITheme.TextDim);
            UIFactory.Band(seat.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 1), 12, -60, 4, 0);

            string name;
            string detail;
            if (slot.Occupied)
            {
                name = slot.Nickname + (slot.SlotId == room.HostSlotId ? " <color=#E8C16A>[房主]</color>" : "");
                if (!slot.Connected) detail = "<color=#FF6A5A>离线</color>";
                else if (slot.SlotId == room.HostSlotId) detail = "房主";
                else detail = slot.Ready ? "<color=#6FE08A>已准备</color>" : "未准备";
                if (slot.Connected && slot.PingMs > 0) detail += "  " + slot.PingMs + "ms";
            }
            else
            {
                name = "空位";
                detail = room.Settings.FillWithAI ? "开始时由 AI 补位" : "等待玩家";
            }
            if (teamMode) detail += "  " + TeamName(slot.Team);
            var nameText = UIFactory.FitText(bg.transform, "Name", name, UITheme.FontBody, slot.Occupied ? UITheme.Text : UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Band(nameText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), 64, 10, 6, 0);
            var detailText = UIFactory.Text(bg.transform, "Detail", detail, UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Band(detailText.rectTransform, new Vector2(0, 0.25f), new Vector2(1, 0.5f), 64, 10, 0, 0);

            var actions = UIFactory.Rect("Actions", bg.transform);
            UIFactory.Band(actions, new Vector2(0, 0), new Vector2(1, 0.27f), 60, 8, 0, 6);
            UIFactory.Row(actions, 8, TextAnchor.MiddleRight);
            int id = slot.SlotId;
            var assignment = room.Settings.Config.TeamAssignment;
            bool canTeam = teamMode && slot.Occupied
                           && ((assignment == TeamAssignmentMode.Manual && (mine || host)) || (assignment == TeamAssignmentMode.HostAssigned && host));
            if (canTeam)
            {
                var other = slot.Team == Team.A ? Team.B : Team.A;
                SmallButton(actions, "换到" + TeamName(other), () => _client.ChangeTeam(id, other));
            }
            if (host && slot.Occupied && !mine)
            {
                SmallButton(actions, "转让房主", () => _client.TransferHost(id));
                SmallButton(actions, "踢出", () => _client.Kick(id));
            }
        }

        private static string TeamName(Team t) => t == Team.A ? "红队" : t == Team.B ? "蓝队" : "未分队";

        private static void SmallButton(RectTransform parent, string label, Action action)
        {
            var b = UIFactory.Button(parent, label, label, action, UITheme.ButtonSecondary, 18);
            UIFactory.Size(b, 98, -1);
        }

        private void ToggleReady()
        {
            var self = _client?.Room?.GetSlot(_client.SlotId);
            if (self != null) _client.SetReady(!self.Ready);
        }

        private void StartGame()
        {
            if (_client == null || !_client.IsHost) return;
            _client.StartGame();
        }

        private void SendChat()
        {
            string text = _chatInput.text;
            if (string.IsNullOrWhiteSpace(text) || _client == null) return;
            _client.SendChat(text.Trim());
            _chatInput.text = string.Empty;
        }

        private void Leave()
        {
            Detach();
            GameManager.Instance.Network.LeaveCurrent();
            UI.Show<LanScreen>();
        }

        // ================================================================== settings dialog

        private void OpenSettings()
        {
            if (_client?.Room == null || !_client.IsHost || _dialog != null) return;
            var room = _client.Room;
            _dialog = UIFactory.Rect("SettingsDialog", Root);
            UIFactory.Stretch(_dialog);
            var dim = UIFactory.Image(_dialog, "Dim", UITheme.Dim, null, true);
            UIFactory.Stretch(dim.rectTransform);
            var panel = UIFactory.Panel(_dialog, "Panel", UITheme.Panel);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 980));
            var header = UIFactory.Text(panel.transform, "Header", "房间设置", UITheme.FontLarge + 8, UITheme.Gold);
            UIFactory.Band(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 0, 0, 10, -80);
            var formArea = UIFactory.Rect("Form", panel.transform);
            UIFactory.Band(formArea, new Vector2(0, 0), new Vector2(1, 1), 40, 40, 90, 130);
            var config = room.Settings.Config.Clone();
            config.PlayerCount = room.Slots.Count;
            _form = new ConfigForm(formArea, config, true, 780);
            _form.SetRoomValues(room.Settings.RoomName, room.Settings.FillWithAI);

            var buttons = UIFactory.Rect("Buttons", panel.transform);
            UIFactory.Band(buttons, new Vector2(0, 0), new Vector2(1, 0), 40, 40, -110, 20);
            UIFactory.Row(buttons, 24);
            UIFactory.Size(UIFactory.Button(buttons, "Cancel", "取消", CloseSettings, UITheme.ButtonSecondary), 280, 90);
            UIFactory.Size(UIFactory.Button(buttons, "Apply", "应用", ApplySettings), 280, 90);
        }

        private void ApplySettings()
        {
            if (_form == null || _client == null) return;
            string error = _form.ValidationError();
            if (error != null)
            {
                UI.Toast(error);
                return;
            }
            var settings = new RoomSettings
            {
                RoomName = string.IsNullOrWhiteSpace(_form.RoomName) ? _client.Room.Settings.RoomName : _form.RoomName.Trim(),
                FillWithAI = _form.FillWithAI,
                Config = _form.Config.Clone()
            };
            _client.UpdateSettings(settings);
            var profile = GameManager.Instance.Profiles;
            profile.Profile.LastRoomConfig = settings.Config.Clone();
            profile.Save();
            CloseSettings();
        }

        private void CloseSettings()
        {
            if (_dialog == null) return;
            UnityEngine.Object.Destroy(_dialog.gameObject);
            _dialog = null;
            _form = null;
        }
    }
}
