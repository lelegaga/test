using System;
using Sanguo.App;
using Sanguo.GameModes;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>Title screen: 局域网对战 / 单机游戏 / 设置 / 退出.</summary>
    public sealed class MainMenuScreen : UIScreen
    {
        protected override void Build(RectTransform root)
        {
            var title = UIFactory.Text(root, "Title", "三国身份牌", UITheme.FontTitle + 24, UITheme.Gold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 160));
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            var subtitle = UIFactory.Text(root, "Subtitle", "局域网多人身份卡牌对战", UITheme.FontLarge, UITheme.TextDim);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 60));

            var column = UIFactory.Rect("Buttons", root);
            UIFactory.Place(column, new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 460));
            UIFactory.Column(column, 22, TextAnchor.MiddleCenter);
            AddButton(column, "局域网对战", () => UI.Show<LanScreen>());
            AddButton(column, "单机游戏", () => UI.Show<SinglePlayerScreen>());
            AddButton(column, "设置", () => UI.Show<SettingsScreen>(), UITheme.ButtonSecondary);
            if (Application.platform != RuntimePlatform.IPhonePlayer)
                AddButton(column, "退出", Application.Quit, UITheme.ButtonSecondary);

            var version = UIFactory.Text(root, "Version", "v" + Application.version + " · 开发版（原创占位素材）", UITheme.FontSmall, UITheme.TextDim, TextAnchor.LowerRight);
            UIFactory.Place(version.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 16), new Vector2(900, 40));
        }

        private static void AddButton(RectTransform parent, string label, Action action, Color? color = null)
        {
            var b = UIFactory.Button(parent, label, label, action, color ?? UITheme.Button, UITheme.FontLarge);
            UIFactory.Size(b, 520, 92);
        }
    }

    /// <summary>Single player setup: mode, player count, roles, character selection, timer.</summary>
    public sealed class SinglePlayerScreen : UIScreen
    {
        private ConfigForm _form;

        protected override void Build(RectTransform root)
        {
            var panel = UIFactory.Panel(root, "Panel", UITheme.Panel);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 900));
            var header = UIFactory.Text(panel.transform, "Header", "单机游戏", UITheme.FontTitle, UITheme.Gold);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(760, 100));

            var formArea = UIFactory.Rect("Options", panel.transform);
            UIFactory.Band(formArea, new Vector2(0, 0), new Vector2(1, 1), 40, 40, 124, 150);
            var saved = GameManager.Instance.Profiles.Profile.LastRoomConfig;
            _form = new ConfigForm(formArea, saved, false, 760);

            var hint = UIFactory.Text(panel.transform, "Hint", "你坐 1 号位，其余座位由 AI 担任（座位与身份随机）", UITheme.FontSmall, UITheme.TextDim);
            UIFactory.Band(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 40, 40, -144, 110);

            var buttons = UIFactory.Rect("Buttons", panel.transform);
            UIFactory.Band(buttons, new Vector2(0, 0), new Vector2(1, 0), 40, 40, -110, 20);
            UIFactory.Row(buttons, 24);
            UIFactory.Size(UIFactory.Button(buttons, "Back", "返回", () => UI.Show<MainMenuScreen>(), UITheme.ButtonSecondary), 300, 90);
            UIFactory.Size(UIFactory.Button(buttons, "Start", "开始游戏", StartGame, UITheme.Button, UITheme.FontLarge), 360, 90);
        }

        private void StartGame()
        {
            string error = _form.ValidationError();
            if (error != null)
            {
                UI.Toast(error);
                return;
            }
            var gm = GameManager.Instance;
            var config = _form.Config.Clone();
            gm.Profiles.Profile.LastRoomConfig = config.Clone();
            gm.Profiles.Save();
            var session = gm.StartLocalGame(config, gm.Profiles.Profile.Nickname, Environment.TickCount);
            var game = UI.Show<GameScreen>();
            game.Attach(new LocalGameView(session, 0), config, () =>
            {
                gm.StopLocalGame();
                UI.Show<SinglePlayerScreen>();
            });
        }

        public override void OnBack() => UI.Show<MainMenuScreen>();
    }

    /// <summary>Nickname, avatar, volumes and quality; saved to the local profile.</summary>
    public sealed class SettingsScreen : UIScreen
    {
        private InputField _nickname;
        private int _avatar;
        private float _music;
        private float _sfx;
        private int _quality;

        protected override void Build(RectTransform root)
        {
            var profile = GameManager.Instance.Profiles.Profile;
            _avatar = profile.AvatarId;
            _music = profile.MusicVolume;
            _sfx = profile.SfxVolume;
            _quality = profile.Quality;

            var panel = UIFactory.Panel(root, "Panel", UITheme.Panel);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 820));
            var header = UIFactory.Text(panel.transform, "Header", "设置", UITheme.FontTitle, UITheme.Gold);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(760, 100));

            var column = UIFactory.Rect("Options", panel.transform);
            UIFactory.Band(column, new Vector2(0, 0), new Vector2(1, 1), 40, 40, 140, 170);
            UIFactory.Column(column, 20);
            var nameLabel = UIFactory.Text(column, "NameLabel", "昵称", UITheme.FontBody, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Size(nameLabel, 740, 40);
            _nickname = UIFactory.Input(column, "Nickname", "输入昵称（最多 16 字）", profile.Nickname);
            _nickname.characterLimit = 16;
            UIFactory.Size(_nickname, 740, 72);
            var avatars = new string[8];
            for (int i = 0; i < avatars.Length; i++) avatars[i] = "头像 " + (i + 1);
            UIFactory.Stepper(column, "头像", avatars, _avatar, i => _avatar = i, 740);
            AddSlider(column, "音乐音量", _music, v => _music = v);
            AddSlider(column, "音效音量", _sfx, v => _sfx = v);
            UIFactory.Stepper(column, "画质", new[] { "流畅", "均衡", "精美" }, _quality, i => _quality = i, 740);

            var buttons = UIFactory.Rect("Buttons", panel.transform);
            UIFactory.Band(buttons, new Vector2(0, 0), new Vector2(1, 0), 40, 40, -130, 30);
            UIFactory.Row(buttons, 24);
            UIFactory.Size(UIFactory.Button(buttons, "Back", "返回", () => UI.Show<MainMenuScreen>(), UITheme.ButtonSecondary), 300, 90);
            UIFactory.Size(UIFactory.Button(buttons, "Save", "保存", Save), 300, 90);
        }

        private static void AddSlider(RectTransform parent, string label, float value, Action<float> onChange)
        {
            var row = UIFactory.Rect(label, parent);
            UIFactory.Size(row, 740, 64);
            var t = UIFactory.Text(row, "Label", label, UITheme.FontBody, UITheme.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Band(t.rectTransform, new Vector2(0, 0), new Vector2(0.3f, 1));
            var slider = UIFactory.Slider(row, "Slider", value, onChange);
            UIFactory.Band((RectTransform)slider.transform, new Vector2(0.32f, 0), new Vector2(1, 1));
        }

        private void Save()
        {
            var gm = GameManager.Instance;
            var p = gm.Profiles.Profile;
            p.Nickname = string.IsNullOrWhiteSpace(_nickname.text) ? p.Nickname : _nickname.text.Trim();
            p.AvatarId = _avatar;
            p.MusicVolume = _music;
            p.SfxVolume = _sfx;
            p.Quality = _quality;
            gm.Profiles.Save();
            gm.Audio.SfxVolume = _sfx;
            UI.Toast("设置已保存");
            UI.Show<MainMenuScreen>();
        }

        public override void OnBack() => UI.Show<MainMenuScreen>();
    }
}
