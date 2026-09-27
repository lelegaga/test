using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.GameModes;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>
    /// Editable game setup (mode, player count, character selection, timer, identity role counts,
    /// team options, and for LAN rooms: room name, AI fill, spectators). Used by the single-player
    /// setup and the room settings dialog. The form rebuilds itself when the mode or player count
    /// changes because the available options depend on them.
    /// </summary>
    public sealed class ConfigForm
    {
        private readonly RectTransform _parent;
        private readonly bool _roomOptions;
        private readonly float _width;
        private RectTransform _content;
        private Text _summary;
        private InputField _roomName;

        public ConfigForm(RectTransform parent, GameModeConfig config, bool roomOptions, float width = 740)
        {
            _parent = parent;
            _roomOptions = roomOptions;
            _width = width;
            Config = Sanitize(config);
            Rebuild();
        }

        public GameModeConfig Config { get; private set; }
        public string RoomName { get; set; } = "三国战场";
        public bool FillWithAI { get; set; } = true;

        /// <summary>Raised after any change (the summary line is refreshed first).</summary>
        public event Action Changed;

        public void SetRoomValues(string roomName, bool fillWithAI)
        {
            RoomName = roomName;
            FillWithAI = fillWithAI;
            Rebuild();
        }

        public void Load(GameModeConfig config)
        {
            Config = Sanitize(config);
            Rebuild();
        }

        /// <summary>Normalises a stored/received config onto the options this form offers.</summary>
        public static GameModeConfig Sanitize(GameModeConfig source)
        {
            var c = source != null ? source.Clone() : ModeOptions.Build(ModeOptions.ModeIds[0], 5, CharacterSelectionMode.Choose, 30);
            c.ModeId = ModeOptions.ModeIds[ModeOptions.ModeIndex(c.ModeId)];
            var counts = ModeOptions.PlayerCounts(c.ModeId);
            if (!counts.Contains(c.PlayerCount)) c.PlayerCount = ModeOptions.DefaultPlayers(c.ModeId);
            c.TeamSize = ModeOptions.IsTeamMode(c.ModeId) ? c.PlayerCount / 2 : 0;
            if (c.ModeId != IdentityMode.Id) c.Roles.Clear();
            if (!c.UseCaptain) c.TeamVictory = TeamVictoryRule.EliminateAll;
            return c;
        }

        private void Rebuild()
        {
            if (_content != null)
            {
                if (_roomName != null) RoomName = _roomName.text;
                _content.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_content.gameObject);
            }
            _roomName = null;
            _content = UIFactory.Rect("Form", _parent);
            UIFactory.Stretch(_content);
            UIFactory.ScrollView(_content, "Scroll", false, out var column, new Color(0, 0, 0, 0));
            UIFactory.Stretch((RectTransform)column.parent);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;

            var c = Config;
            if (_roomOptions)
            {
                var label = UIFactory.Text(column, "RoomNameLabel", "房间名", UITheme.FontBody, UITheme.TextDim, TextAnchor.MiddleLeft);
                UIFactory.Size(label, _width, 40);
                _roomName = UIFactory.Input(column, "RoomName", "房间名（最多 24 字）", RoomName);
                _roomName.characterLimit = 24;
                _roomName.onEndEdit.AddListener(v =>
                {
                    RoomName = v;
                    Notify();
                });
                UIFactory.Size(_roomName, _width, 68);
            }

            UIFactory.Stepper(column, "模式", ModeOptions.ModeNames, ModeOptions.ModeIndex(c.ModeId), i =>
            {
                c.ModeId = ModeOptions.ModeIds[i];
                c.PlayerCount = ModeOptions.DefaultPlayers(c.ModeId);
                c.Roles.Clear();
                Config = Sanitize(c);
                Rebuild();
                Notify();
            }, _width);

            var counts = ModeOptions.PlayerCounts(c.ModeId);
            UIFactory.Stepper(column, "人数", counts.ConvertAll(n => n + " 人"), Math.Max(0, counts.IndexOf(c.PlayerCount)), i =>
            {
                c.PlayerCount = counts[i];
                c.Roles.Clear();
                Config = Sanitize(c);
                Rebuild();
                Notify();
            }, _width);

            UIFactory.Stepper(column, "选将", ModeOptions.SelectionNames, Math.Max(0, Array.IndexOf(ModeOptions.Selections, c.CharacterSelection)), i =>
            {
                c.CharacterSelection = ModeOptions.Selections[i];
                Notify();
            }, _width);

            int timeoutIndex = Array.IndexOf(ModeOptions.TimeoutSeconds, c.PlayTimeoutMs / 1000);
            UIFactory.Stepper(column, "出牌时间", ModeOptions.TimeoutNames, timeoutIndex < 0 ? 1 : timeoutIndex, i =>
            {
                var t = ModeOptions.Build(c.ModeId, c.PlayerCount, c.CharacterSelection, ModeOptions.TimeoutSeconds[i]);
                c.PlayTimeoutMs = t.PlayTimeoutMs;
                c.ResponseTimeoutMs = t.ResponseTimeoutMs;
                c.DiscardTimeoutMs = t.DiscardTimeoutMs;
                Notify();
            }, _width);

            if (c.ModeId == IdentityMode.Id) BuildRoleEditor(column, c);
            if (ModeOptions.IsTeamMode(c.ModeId)) BuildTeamOptions(column, c);

            if (_roomOptions)
            {
                UIFactory.Toggle(column, "空位由 AI 补齐", FillWithAI, v =>
                {
                    FillWithAI = v;
                    Notify();
                }, _width);
                UIFactory.Toggle(column, "允许观战", c.AllowSpectators, v =>
                {
                    c.AllowSpectators = v;
                    Notify();
                }, _width);
            }

            _summary = UIFactory.Text(column, "Summary", string.Empty, UITheme.FontSmall, UITheme.TextDim);
            UIFactory.Size(_summary, _width, 80);
            RefreshSummary();
        }

        private void BuildRoleEditor(RectTransform column, GameModeConfig c)
        {
            bool custom = c.Roles.Count > 0;
            UIFactory.Stepper(column, "身份分配", new[] { "标准配置", "自定义" }, custom ? 1 : 0, i =>
            {
                c.Roles.Clear();
                if (i == 1) c.Roles.AddRange(CloneRoles(IdentityMode.DefaultRoles(c.PlayerCount)));
                Rebuild();
                Notify();
            }, _width);
            if (!custom) return;

            var values = new List<string>();
            for (int n = 0; n < c.PlayerCount; n++) values.Add(n.ToString());
            AddRoleStepper(column, c, Role.Loyalist, "忠臣", values);
            AddRoleStepper(column, c, Role.Rebel, "反贼", values);
            AddRoleStepper(column, c, Role.Renegade, "内奸", values);
        }

        private void AddRoleStepper(RectTransform column, GameModeConfig c, Role role, string label, List<string> values)
        {
            UIFactory.Stepper(column, label, values, c.GetRoleCount(role), i =>
            {
                SetRoleCount(c, role, i);
                Notify();
            }, _width);
        }

        private static void SetRoleCount(GameModeConfig c, Role role, int count)
        {
            bool hasLord = false;
            for (int i = c.Roles.Count - 1; i >= 0; i--)
            {
                if (c.Roles[i].Role == Role.Lord) hasLord = true;
                if (c.Roles[i].Role == role) c.Roles.RemoveAt(i);
            }
            if (!hasLord) c.Roles.Insert(0, new RoleCount(Role.Lord, 1));
            if (count > 0) c.Roles.Add(new RoleCount(role, count));
        }

        private static List<RoleCount> CloneRoles(List<RoleCount> roles)
        {
            var list = new List<RoleCount>();
            foreach (var r in roles) list.Add(new RoleCount(r.Role, r.Count));
            return list;
        }

        private void BuildTeamOptions(RectTransform column, GameModeConfig c)
        {
            UIFactory.Stepper(column, "分队方式", ModeOptions.TeamAssignmentNames, (int)c.TeamAssignment, i =>
            {
                c.TeamAssignment = (TeamAssignmentMode)i;
                Notify();
            }, _width);
            UIFactory.Toggle(column, "队长制（队长 +" + c.CaptainExtraHp + " 体力上限）", c.UseCaptain, v =>
            {
                c.UseCaptain = v;
                if (!v) c.TeamVictory = TeamVictoryRule.EliminateAll;
                Rebuild();
                Notify();
            }, _width);
            if (c.UseCaptain)
            {
                UIFactory.Stepper(column, "胜利条件", ModeOptions.TeamVictoryNames, (int)c.TeamVictory, i =>
                {
                    c.TeamVictory = (TeamVictoryRule)i;
                    Notify();
                }, _width);
            }
        }

        /// <summary>Human readable check of the current setup; null when valid.</summary>
        public string ValidationError()
        {
            var c = Config;
            if (c.ModeId != IdentityMode.Id || c.Roles.Count == 0) return null;
            int total = 0;
            foreach (var r in c.Roles) total += r.Count;
            return total == c.PlayerCount ? null : "身份总数 " + total + " 与人数 " + c.PlayerCount + " 不一致";
        }

        private void RefreshSummary()
        {
            if (_summary == null) return;
            string error = ValidationError();
            string roles = string.Empty;
            if (Config.ModeId == IdentityMode.Id)
            {
                var list = Config.Roles.Count > 0 ? Config.Roles : IdentityMode.DefaultRoles(Config.PlayerCount);
                foreach (var r in list)
                    if (r.Count > 0) roles += (roles.Length > 0 ? " " : "") + Events.GameLogFormatter.RoleName(r.Role) + "×" + r.Count;
                roles = "\n" + roles;
            }
            _summary.text = error != null
                ? "<color=#FF6A5A>" + error + "</color>"
                : ModeOptions.Describe(Config) + roles;
        }

        private void Notify()
        {
            RefreshSummary();
            Changed?.Invoke();
        }
    }
}
