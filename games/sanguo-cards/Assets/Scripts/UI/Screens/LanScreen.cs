using System;
using System.Collections.Generic;
using System.Text;
using Sanguo.App;
using Sanguo.GameModes;
using Sanguo.Network;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>
    /// LAN lobby browser: rooms found by UDP discovery, create a room, join by IP or invite code.
    /// iOS devices usually cannot receive broadcasts without Apple's multicast entitlement, so the
    /// invite code / IP path is always offered next to the list.
    /// </summary>
    public sealed class LanScreen : UIScreen
    {
        private RectTransform _list;
        private Text _empty;
        private Text _status;
        private InputField _address;
        private InputField _invite;
        private string _signature = string.Empty;
        private float _nextRefresh;

        protected override void Build(RectTransform root)
        {
            var header = UIFactory.Text(root, "Header", "局域网对战", UITheme.FontTitle, UITheme.Gold);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(900, 100));

            // Left: discovered rooms.
            var listPanel = UIFactory.Panel(root, "RoomsPanel", UITheme.Panel);
            UIFactory.Band(listPanel.rectTransform, new Vector2(0, 0), new Vector2(0.62f, 1), 40, 16, 120, 40);
            var listTitle = UIFactory.Text(listPanel.transform, "Title", "附近的房间", UITheme.FontLarge, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Band(listTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 28, 200, 0, -70);
            var refresh = UIFactory.Button(listPanel.transform, "Refresh", "刷新", Refresh, UITheme.ButtonSecondary);
            UIFactory.Place((RectTransform)refresh.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -8), new Vector2(150, 56));
            UIFactory.ScrollView(listPanel.transform, "List", false, out _list);
            UIFactory.Band((RectTransform)_list.parent, new Vector2(0, 0), new Vector2(1, 1), 20, 20, 80, 70);
            _list.GetComponent<VerticalLayoutGroup>().spacing = 10;
            _empty = UIFactory.Text(listPanel.transform, "Empty", "正在搜索同一 WiFi 下的房间…", UITheme.FontBody, UITheme.TextDim);
            UIFactory.Band(_empty.rectTransform, new Vector2(0, 0.3f), new Vector2(1, 0.7f), 40, 40);
            _status = UIFactory.Text(listPanel.transform, "Status", string.Empty, UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Band(_status.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 28, 28, -64, 6);

            // Right: create / direct join.
            var side = UIFactory.Panel(root, "SidePanel", UITheme.Panel);
            UIFactory.Band(side.rectTransform, new Vector2(0.62f, 0), new Vector2(1, 1), 16, 40, 120, 40);
            var column = UIFactory.Rect("Column", side.transform);
            UIFactory.Stretch(column, 30, 30, 26, 26);
            UIFactory.Column(column, 14);
            UIFactory.Size(UIFactory.Button(column, "Create", "创建房间", CreateRoom, UITheme.Button, UITheme.FontLarge), -1, 96);
            Spacer(column, 10);
            Label(column, "输入房主 IP 加入（如 192.168.1.8）");
            var profile = GameManager.Instance.Profiles.Profile;
            _address = UIFactory.Input(column, "Address", "IP 地址[:端口]", profile.LastServerAddress);
            UIFactory.Size(_address, -1, 68);
            UIFactory.Size(UIFactory.Button(column, "JoinIp", "IP 加入", JoinByAddress, UITheme.ButtonSecondary), -1, 72);
            Spacer(column, 10);
            Label(column, "输入 8 位邀请码加入");
            _invite = UIFactory.Input(column, "Invite", "邀请码", profile.LastInviteCode);
            _invite.characterLimit = 9;
            _invite.characterValidation = InputField.CharacterValidation.Alphanumeric;
            UIFactory.Size(_invite, -1, 68);
            UIFactory.Size(UIFactory.Button(column, "JoinCode", "邀请码加入", JoinByInvite, UITheme.ButtonSecondary), -1, 72);
            Spacer(column, 10);
            UIFactory.Size(UIFactory.Button(column, "Back", "返回", () => UI.Show<MainMenuScreen>(), UITheme.ButtonSecondary), -1, 72);
        }

        private static void Label(RectTransform parent, string text)
        {
            var t = UIFactory.Text(parent, "Label", text, UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Size(t, -1, 36);
        }

        private static void Spacer(RectTransform parent, float height)
        {
            UIFactory.Size(UIFactory.Rect("Spacer", parent), -1, height);
        }

        public override void OnShow()
        {
            var nm = GameManager.Instance.Network;
            nm.LeaveCurrent();
            nm.StartBrowsing();
            _signature = null;
            var ip = LocalNetwork.GetLanIPv4();
            _status.text = ip != null && !System.Net.IPAddress.IsLoopback(ip) ? "本机 IP：" + ip : "未检测到局域网连接，请先连接 WiFi";
        }

        public override void OnHide() => GameManager.Instance.Network.StopBrowsing();

        public override void OnBack() => UI.Show<MainMenuScreen>();

        private void Refresh()
        {
            GameManager.Instance.Network.StopBrowsing();
            GameManager.Instance.Network.StartBrowsing();
            _signature = null;
            UI.Toast("已刷新");
        }

        public override void Tick()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            var rooms = GameManager.Instance.Network.Rooms?.Rooms;
            string error = GameManager.Instance.Network.Rooms?.DiscoveryError;
            var sb = new StringBuilder();
            if (rooms != null)
                foreach (var r in rooms)
                    sb.Append(r.RoomId).Append(r.Humans).Append(r.MaxPlayers).Append(r.InGame).Append(r.RoomName).Append('|');
            string signature = sb.ToString() + error;
            if (signature == _signature) return;
            _signature = signature;
            RebuildList(rooms, error);
        }

        private void RebuildList(IReadOnlyList<RoomInfo> rooms, string error)
        {
            UIFactory.DestroyChildren(_list);
            int count = rooms?.Count ?? 0;
            _empty.gameObject.SetActive(count == 0);
            if (count == 0)
            {
                _empty.text = string.IsNullOrEmpty(error)
                    ? "正在搜索同一 WiFi 下的房间…\n找不到时请使用右侧 IP 或邀请码加入"
                    : "房间搜索不可用：" + error + "\n请使用右侧 IP 或邀请码加入";
                return;
            }
            foreach (var room in rooms) AddRoomRow(room);
        }

        private void AddRoomRow(RoomInfo room)
        {
            var row = UIFactory.Image(_list, "Room " + room.RoomId, UITheme.PanelLight, UIAssets.RoundedSmall, true);
            UIFactory.Size(row, -1, 120);
            bool compatible = room.ProtocolVersion == ProtocolInfo.Version;
            string state = !compatible ? "<color=#FF6A5A>版本不兼容</color>" : room.InGame ? "<color=#E8C16A>游戏中</color>" : "等待中";
            var title = UIFactory.FitText(row.transform, "Title", room.RoomName + "  <size=22>" + state + "</size>", UITheme.FontLarge, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Band(title.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), 24, 330, 6, 0);
            string info = "房主 " + room.HostName + " · " + ModeOptions.ModeName(room.ModeId) + " · " + room.Humans + "/" + room.MaxPlayers + " 人 · 邀请码 "
                          + room.InviteCode;
            var details = UIFactory.Text(row.transform, "Info", info, UITheme.FontSmall, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Band(details.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), 24, 330, 0, 6);

            var buttons = UIFactory.Rect("Buttons", row.transform);
            UIFactory.Band(buttons, new Vector2(1, 0), new Vector2(1, 1), -320, 16, 18, 18);
            UIFactory.Row(buttons, 12, TextAnchor.MiddleRight);
            var target = room;
            if (compatible && !room.InGame && room.Humans < room.MaxPlayers)
                UIFactory.Size(UIFactory.Button(buttons, "Join", "加入", () => Join(target.Address, target.Port, false)), 140, -1);
            if (compatible && room.AllowSpectators)
                UIFactory.Size(UIFactory.Button(buttons, "Watch", "观战", () => Join(target.Address, target.Port, true), UITheme.ButtonSecondary), 140, -1);
        }

        private void CreateRoom()
        {
            var gm = GameManager.Instance;
            var profile = gm.Profiles.Profile;
            var settings = new RoomSettings
            {
                RoomName = profile.Nickname + " 的房间",
                Config = ConfigForm.Sanitize(profile.LastRoomConfig ?? new RoomSettings().Config)
            };
            GameClient client;
            try
            {
                client = gm.Network.HostRoom(settings, profile.Nickname, profile.AvatarId);
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException || ex is InvalidOperationException)
            {
                UI.Toast("创建房间失败：" + ex.Message);
                return;
            }
            UI.Show<RoomScreen>().Attach(client);
        }

        private void JoinByAddress()
        {
            string text = (_address.text ?? string.Empty).Trim();
            if (!TryParseAddress(text, out string host, out int port))
            {
                UI.Toast("地址格式不正确");
                return;
            }
            var gm = GameManager.Instance;
            gm.Profiles.Profile.LastServerAddress = text;
            gm.Profiles.Save();
            Join(host, port, false);
        }

        /// <summary>Accepts "host" or "host:port" (IPv4 or a host name).</summary>
        public static bool TryParseAddress(string text, out string host, out int port)
        {
            host = null;
            port = ProtocolInfo.DefaultGamePort;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            int colon = text.LastIndexOf(':');
            if (colon > 0 && text.IndexOf(':') == colon)
            {
                if (!int.TryParse(text.Substring(colon + 1), out port) || port <= 0 || port > 65535) return false;
                text = text.Substring(0, colon);
            }
            if (text.Length == 0 || text.IndexOf(' ') >= 0) return false;
            host = text;
            return true;
        }

        private void JoinByInvite()
        {
            var gm = GameManager.Instance;
            var profile = gm.Profiles.Profile;
            string code = (_invite.text ?? string.Empty).Trim();
            var client = gm.Network.JoinByInviteCode(code, profile.Nickname, profile.AvatarId);
            if (client == null)
            {
                UI.Toast("邀请码无效");
                return;
            }
            profile.LastInviteCode = code;
            gm.Profiles.Save();
            UI.Show<RoomScreen>().Attach(client);
        }

        private void Join(string host, int port, bool spectator)
        {
            var gm = GameManager.Instance;
            var profile = gm.Profiles.Profile;
            var client = gm.Network.Join(host, port, profile.Nickname, profile.AvatarId, spectator);
            UI.Show<RoomScreen>().Attach(client);
        }
    }
}
