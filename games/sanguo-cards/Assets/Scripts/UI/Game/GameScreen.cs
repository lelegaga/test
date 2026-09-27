using System;
using System.Collections.Generic;
using System.Text;
using Sanguo.App;
using Sanguo.Audio;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Network;
using Sanguo.Presentation;
using Sanguo.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>
    /// The table. Reads only the projected <see cref="ClientGameState"/> of an <see cref="IGameView"/>
    /// (local or LAN), lets <see cref="InteractionModel"/> decide what is selectable, and sends the
    /// resulting intent commands. Widgets refresh only when an event arrives or the selection
    /// changes, so an idle table costs almost nothing per frame.
    ///
    /// Layout (1920×1080 reference, inside the safe area): top bar, a strip with the other players,
    /// the centre table (cards in play, piles, prompt, timer, optional battle log) and the bottom
    /// row with the own panel and skills, the hand and the action buttons.
    /// </summary>
    public sealed class GameScreen : UIScreen
    {
        private const float TopBarHeight = 60f;
        private const float SeatStripHeight = 250f;
        private const float BottomHeight = 330f;
        private const float SelfPanelWidth = 400f;
        private const float ActionWidth = 250f;
        private const float LogWidth = 470f;
        private const int TableCards = 6;

        private IGameView _view;
        private GameModeConfig _config;
        private Action _onExit;
        private Action _onQuit;
        private GameContent _content;
        private GameLogNames _names;
        private InteractionModel _model;
        private readonly LogFeed _log = new LogFeed(200);
        private IDisposable _subscription;
        private bool _dirty;
        private int _modelVersion = -1;
        private int _logVersion = -1;
        private int _lastCurrent = -1;
        private int _revealPending = -1;
        private float _gameOverAt = -1f;
        private readonly Dictionary<int, float> _requestSeen = new Dictionary<int, float>();
        private readonly List<int> _requestStale = new List<int>();
        private readonly StringBuilder _sb = new StringBuilder(4096);

        // Widgets
        private Text _info;
        private Button _autoButton;
        private Button _logButton;
        private SeatStrip _seats;
        private RectTransform _center;
        private RectTransform _table;
        private readonly List<CardWidget> _tableCards = new List<CardWidget>();
        private UIPool<CardWidget> _tablePool;
        private Text _pile;
        private Text _prompt;
        private Image _timerFill;
        private RectTransform _logPanel;
        private ScrollRect _logScroll;
        private Text _logText;
        private bool _logVisible = true;
        private SelfPanel _self;
        private RectTransform _handArea;
        private HandView _hand;
        private Button _confirm;
        private Button _pass;
        private Button _cancel;
        private Button _endTurn;
        private RectTransform _dialogs;
        private RectTransform _animationLayer;
        private AnimationDirector _director;
        private readonly ChoiceDialog _choice = new ChoiceDialog();
        private readonly PickCardDialog _pick = new PickCardDialog();
        private readonly ResultPanel _result = new ResultPanel();
        private readonly InfoDialog _infoDialog = new InfoDialog();
        private readonly GameMenu _menu = new GameMenu();
        private Text _connection;
        private RectTransform _disconnected;

        protected override void Build(RectTransform root)
        {
            var table = UIFactory.Image(root, "Table", UITheme.Table, UIAssets.Rounded);
            UIFactory.Stretch(table.rectTransform, 8, 8, TopBarHeight + SeatStripHeight * 0.5f, BottomHeight * 0.6f);
            table.color = new Color(UITheme.Table.r, UITheme.Table.g, UITheme.Table.b, 0.55f);

            BuildTopBar(root);

            var seatArea = UIFactory.Rect("Seats", root);
            UIFactory.Band(seatArea, new Vector2(0, 1), new Vector2(1, 1), 0, 0, TopBarHeight, -(TopBarHeight + SeatStripHeight));
            _seats = new SeatStrip(seatArea)
            {
                SeatClicked = OnSeatClicked,
                SeatLongPressed = ShowPlayerInfo
            };

            BuildCenter(root);
            BuildBottom(root);

            _animationLayer = UIFactory.Rect("Animations", root);
            UIFactory.Stretch(_animationLayer);
            _director = AnimationDirector.Create(_animationLayer);

            _dialogs = UIFactory.Rect("Dialogs", root);
            UIFactory.Stretch(_dialogs);

            _connection = UIFactory.Text(root, "Connection", string.Empty, UITheme.FontLarge, UITheme.Text);
            UIFactory.Place(_connection.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 90));
            _connection.gameObject.AddComponent<Outline>().effectColor = Color.black;
        }

        private void BuildTopBar(RectTransform root)
        {
            var bar = UIFactory.Image(root, "TopBar", new Color(0, 0, 0, 0.35f), UIAssets.RoundedSmall);
            UIFactory.Band(bar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), 8, 8, 4, -TopBarHeight + 4);
            _info = UIFactory.FitText(bar.transform, "Info", string.Empty, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            UIFactory.Stretch(_info.rectTransform, 20, 560, 2, 2);
            var buttons = UIFactory.Rect("Buttons", bar.transform);
            UIFactory.Band(buttons, new Vector2(1, 0), new Vector2(1, 1), -560, 10, 4, 4);
            UIFactory.Row(buttons, 10, TextAnchor.MiddleRight);
            _autoButton = UIFactory.Button(buttons, "Auto", "托管中 · 点击取消", () => _view?.SetAutoPlay(false), UITheme.Selected, UITheme.FontSmall);
            UIFactory.Size(_autoButton, 230, -1);
            _logButton = UIFactory.Button(buttons, "Log", "战报", () => SetLogVisible(!_logVisible), UITheme.ButtonSecondary, UITheme.FontSmall);
            UIFactory.Size(_logButton, 120, -1);
            UIFactory.Size(UIFactory.Button(buttons, "Menu", "菜单", OpenMenu, UITheme.ButtonSecondary, UITheme.FontSmall), 120, -1);
        }

        private void BuildCenter(RectTransform root)
        {
            _center = UIFactory.Rect("Center", root);
            UIFactory.Band(_center, new Vector2(0, 0), new Vector2(1, 1), 16, 16, TopBarHeight + SeatStripHeight, BottomHeight);

            _logPanel = UIFactory.Image(_center, "Log", new Color(0, 0, 0, 0.45f), UIAssets.RoundedSmall, true).rectTransform;
            UIFactory.Band(_logPanel, new Vector2(1, 0), new Vector2(1, 1), -LogWidth, 0, 6, 70);
            _logScroll = UIFactory.ScrollView(_logPanel, "Scroll", false, out var logContent, new Color(0, 0, 0, 0));
            UIFactory.Stretch((RectTransform)_logScroll.transform, 4, 4, 4, 4);
            _logText = UIFactory.Text(logContent, "Lines", string.Empty, UITheme.FontSmall - 2, UITheme.Text, TextAnchor.UpperLeft);
            _logText.verticalOverflow = VerticalWrapMode.Overflow;

            var pileBack = UIFactory.Image(_center, "Pile", UITheme.CardBack, UIAssets.RoundedSmall);
            UIFactory.Place(pileBack.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 30), new Vector2(96, 134));
            var pileLabel = UIFactory.Text(pileBack.transform, "Label", "牌堆", UITheme.FontSmall, UITheme.Gold);
            UIFactory.Stretch(pileLabel.rectTransform);
            _pile = UIFactory.Text(_center, "PileCount", string.Empty, UITheme.FontSmall, UITheme.TextDim, TextAnchor.UpperCenter);
            UIFactory.Place(_pile.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 1), new Vector2(68, -40), new Vector2(200, 64));

            _table = UIFactory.Rect("TableCards", _center);
            UIFactory.Band(_table, new Vector2(0, 0), new Vector2(1, 1), 150, LogWidth + 20, 10, 80);
            var row = UIFactory.Row(_table, 12);
            row.childForceExpandHeight = false;
            _tablePool = new UIPool<CardWidget>(() =>
            {
                var w = CardWidget.Create(_table, new Vector2(120, 168), true);
                UIFactory.Size(w, 120, 168);
                return w;
            });

            _prompt = UIFactory.FitText(_center, "Prompt", string.Empty, UITheme.FontBody + 2, UITheme.Text);
            UIFactory.Band(_prompt.rectTransform, new Vector2(0, 0), new Vector2(1, 0), 150, LogWidth + 20, -58, 14);
            _prompt.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            var timerBg = UIFactory.Image(_center, "Timer", new Color(0, 0, 0, 0.5f), UIAssets.White);
            UIFactory.Band(timerBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), -300, -300, -10, 2);
            _timerFill = UIFactory.Image(timerBg.transform, "Fill", UITheme.Gold, UIAssets.White);
            _timerFill.type = Image.Type.Filled;
            _timerFill.fillMethod = Image.FillMethod.Horizontal;
            UIFactory.Stretch(_timerFill.rectTransform);
        }

        private void BuildBottom(RectTransform root)
        {
            var selfArea = UIFactory.Rect("Self", root);
            UIFactory.Band(selfArea, new Vector2(0, 0), new Vector2(0, 0), 12, -SelfPanelWidth, -BottomHeight + 10, 12);
            _self = new SelfPanel(selfArea)
            {
                Clicked = () => { if (_view != null) OnSeatClicked(_view.ViewerId); },
                LongPressed = () => { if (_view != null) ShowPlayerInfo(_view.ViewerId); },
                SkillClicked = OnSkillClicked,
                SkillLongPressed = ShowSkillInfo
            };

            _handArea = UIFactory.Rect("Hand", root);
            UIFactory.Band(_handArea, new Vector2(0, 0), new Vector2(1, 0), SelfPanelWidth + 30, ActionWidth + 30, -270, 10);
            _hand = HandView.Create(_handArea, CardWidget.DefaultSize);
            _hand.CardClicked = OnCardClicked;
            _hand.CardLongPressed = card => _infoDialog.Show(_dialogs, "卡牌说明", CardWidget.Describe(_content, card));

            var actions = UIFactory.Rect("Actions", root);
            UIFactory.Band(actions, new Vector2(1, 0), new Vector2(1, 0), -ActionWidth - 12, 12, -BottomHeight + 20, 16);
            UIFactory.Column(actions, 12, TextAnchor.LowerCenter);
            _confirm = UIFactory.Button(actions, "Confirm", "确定", Confirm, UITheme.Button, UITheme.FontLarge);
            UIFactory.Size(_confirm, -1, 80);
            _pass = UIFactory.Button(actions, "Pass", "不出", Pass, UITheme.ButtonSecondary, UITheme.FontBody);
            UIFactory.Size(_pass, -1, 70);
            _cancel = UIFactory.Button(actions, "Cancel", "取消", () => _model?.CancelSelection(), UITheme.ButtonSecondary, UITheme.FontBody);
            UIFactory.Size(_cancel, -1, 70);
            _endTurn = UIFactory.Button(actions, "EndTurn", "结束出牌", EndTurn, new Color32(0x6A, 0x55, 0x2A, 0xFF), UITheme.FontBody);
            UIFactory.Size(_endTurn, -1, 70);
        }

        // ================================================================== attach / detach

        /// <summary>Shows a game. <paramref name="onExit"/> closes the table after the game;
        /// <paramref name="onQuit"/> (defaults to onExit) leaves a running game from the menu.</summary>
        public void Attach(IGameView view, GameModeConfig config, Action onExit, Action onQuit = null)
        {
            Detach();
            _view = view;
            _config = config ?? new GameModeConfig();
            _onExit = onExit;
            _onQuit = onQuit ?? onExit;
            _content = GameManager.Instance.Content;
            _names = new GameLogNames(_content, id => _view?.State?.GetPlayer(id)?.Nickname);
            _model = new InteractionModel(view.ViewerId, _names);
            _log.Clear();
            _log.Add("<color=#E8C16A>" + ModeOptions.Describe(_config) + "</color>");
            _subscription = view.Events.SubscribeAll(OnEvent);
            view.StateReplaced += OnStateReplaced;
            view.CommandRejected += OnCommandRejected;
            GameManager.Instance.Audio.Attach(view.Events, view.ViewerId);
            _director.Bind(_content, _names, view.ViewerId, SeatWorld, TableWorld, PileWorld, () => _hand.CenterWorld);
            _seats.Clear();
            _requestSeen.Clear();
            _gameOverAt = -1f;
            _lastCurrent = -1;
            _modelVersion = -1;
            _logVersion = -1;
            CloseDialogs();
            _result.Close();
            Modal.Close(ref _disconnected);
            SetLogVisible(_logVisible);
            _dirty = true;
        }

        private void Detach()
        {
            if (_view == null) return;
            _subscription?.Dispose();
            _subscription = null;
            _view.StateReplaced -= OnStateReplaced;
            _view.CommandRejected -= OnCommandRejected;
            _view.Leave();
            GameManager.Instance.Audio.Attach(null, -1);
            _view = null;
        }

        public override void OnHide()
        {
            CloseDialogs();
            _result.Close();
            Modal.Close(ref _disconnected);
            Detach();
        }

        public override void OnBack()
        {
            if (_infoDialog.IsOpen) _infoDialog.Close();
            else if (_menu.IsOpen) _menu.Close();
            else if (_result.IsOpen) ExitTable();
            else OpenMenu();
        }

        private void CloseDialogs()
        {
            _choice.Close();
            _pick.Close();
            _infoDialog.Close();
            _menu.Close();
        }

        // ================================================================== events

        private void OnEvent(GameEvent e)
        {
            _log.AddEvent(e, _names, _view.ViewerId);
            _director.OnEvent(e);
            _dirty = true;
        }

        private void OnStateReplaced()
        {
            _log.Add("<color=#8FB8FF>（已与主机重新同步）</color>");
            _requestSeen.Clear();
            _dirty = true;
        }

        private void OnCommandRejected(CommandResult result)
        {
            _model?.OnCommandRejected();
            UI.Toast(RejectText(result.Reason));
        }

        public static string RejectText(RejectReason reason)
        {
            switch (reason)
            {
                case RejectReason.CardNotUsable: return "这张牌现在不能使用";
                case RejectReason.CardNotOwned: return "你没有这张牌";
                case RejectReason.UsageLimitReached: return "本阶段使用次数已达上限";
                case RejectReason.InvalidTarget: return "目标不合法";
                case RejectReason.TargetOutOfRange: return "目标不在距离内";
                case RejectReason.WrongTargetCount: return "目标数量不正确";
                case RejectReason.WrongCardCount: return "选择的牌数量不正确";
                case RejectReason.SkillUnavailable: return "技能现在不能发动";
                case RejectReason.PassNotAllowed: return "现在不能跳过";
                case RejectReason.NoPendingRequest:
                case RejectReason.RequestMismatch:
                case RejectReason.OutOfOrderSequence:
                case RejectReason.DuplicateSequence: return "操作已过期";
                case RejectReason.RateLimited: return "操作过于频繁";
                case RejectReason.PlayerDead: return "你已阵亡";
                case RejectReason.GameOver: return "游戏已结束";
                default: return "操作无效";
            }
        }

        // ================================================================== frame update

        public override void Tick()
        {
            if (_view == null) return;
            UpdateConnection();
            var state = _view.State;
            if (state == null) return;

            if (_dirty)
            {
                _model.Refresh(state);
                TrackRequests(state);
            }
            if (_dirty || _model.Version != _modelVersion)
            {
                _dirty = false;
                _modelVersion = _model.Version;
                RefreshAll(state);
            }
            if (_revealPending >= 0)
            {
                _seats.Reveal(_revealPending);
                _revealPending = -1;
            }
            UpdateTimers(state);
            if (_logVisible && _log.Version != _logVersion) RefreshLog();
            if (state.IsGameOver && !_result.IsOpen && _gameOverAt >= 0 && Time.unscaledTime >= _gameOverAt) ShowResult(state);
        }

        private void RefreshAll(ClientGameState state)
        {
            bool teamMode = ModeOptions.IsTeamMode(state.ModeId);
            int viewer = _view.ViewerId;

            var slots = SeatLayout.Arrange(state, viewer);
            _seats.Sync(slots);
            foreach (var slot in slots)
            {
                var p = state.GetPlayer(slot.PlayerId);
                var w = _seats.Get(slot.PlayerId);
                if (p == null || w == null) continue;
                w.Refresh(p, _content, Highlight(state, p.PlayerId, teamMode));
            }
            var self = state.Self;
            _self.Refresh(self, _content, _model, self != null ? Highlight(state, self.PlayerId, teamMode) : default, viewer);
            _hand.Sync(self?.HandCards, _content, _model);
            RefreshTable(state);
            RefreshActions();
            RefreshInfo(state);
            _prompt.text = state.IsGameOver ? "游戏结束" : _model.Prompt;
            _autoButton.gameObject.SetActive(self != null && self.AIControlled && self.Connected && !state.IsGameOver);
            RefreshDialogs(state);

            if (state.CurrentPlayerId != _lastCurrent)
            {
                _lastCurrent = state.CurrentPlayerId;
                _revealPending = _lastCurrent;
            }
            if (state.IsGameOver && _gameOverAt < 0) _gameOverAt = Time.unscaledTime + 1.5f;
        }

        private SeatHighlight Highlight(ClientGameState state, int playerId, bool teamMode)
        {
            return new SeatHighlight
            {
                Current = playerId == state.CurrentPlayerId && !state.IsGameOver,
                Selectable = _model.IsTargetSelectable(playerId) && !_model.IsTargetSelected(playerId),
                Selected = _model.IsTargetSelected(playerId),
                TeamMode = teamMode
            };
        }

        private void RefreshInfo(ClientGameState state)
        {
            _sb.Clear();
            _sb.Append(ModeOptions.ModeName(state.ModeId));
            if (state.Round > 0) _sb.Append(" · 第 ").Append(state.Round).Append(" 轮");
            var current = state.GetPlayer(state.CurrentPlayerId);
            if (current != null && !state.IsGameOver)
                _sb.Append(" · ").Append(current.PlayerId == _view.ViewerId ? "你" : current.Nickname).Append(" 的").Append(CardText.PhaseName(state.Phase));
            if (_view.IsNetwork && _view is NetworkGameView nv && nv.Client.PingMs > 0) _sb.Append(" · ").Append(nv.Client.PingMs).Append("ms");
            _info.text = _sb.ToString();
            _pile.text = state.DrawPileCount + " 张\n弃牌 " + state.DiscardPile.Count;
        }

        private void RefreshTable(ClientGameState state)
        {
            foreach (var w in _tableCards) _tablePool.Release(w);
            _tableCards.Clear();
            int fromDiscard = Math.Max(0, TableCards - state.Processing.Count);
            int start = Math.Max(0, state.DiscardPile.Count - fromDiscard);
            for (int i = start; i < state.DiscardPile.Count; i++) AddTableCard(state.DiscardPile[i], true);
            foreach (var card in state.Processing) AddTableCard(card, false);
        }

        private void AddTableCard(CardInfo card, bool old)
        {
            var w = _tablePool.Get();
            w.Rect.SetAsLastSibling();
            w.SetCard(card, _content);
            w.SetHighlight(false, false);
            w.SetAlpha(old ? 0.55f : 1f);
            var captured = card;
            w.SetCallbacks(null, () => _infoDialog.Show(_dialogs, "卡牌说明", CardWidget.Describe(_content, captured)));
            _tableCards.Add(w);
        }

        private void RefreshActions()
        {
            var mode = _model.Mode;
            bool confirmMode = mode == InteractionMode.PlayPhase || mode == InteractionMode.SkillSelect || mode == InteractionMode.Response
                               || mode == InteractionMode.Discard || mode == InteractionMode.ChooseTargets || mode == InteractionMode.Confirm;
            SetButton(_confirm, confirmMode && !_model.Submitted, _model.CanConfirm, _model.ConfirmLabel);
            SetButton(_pass, _model.CanPass, true, _model.PassLabel);
            SetButton(_cancel, _model.CanCancelSelection && mode != InteractionMode.Confirm, true, "取消");
            SetButton(_endTurn, _model.CanEndTurn, true, "结束出牌");
        }

        private static void SetButton(Button b, bool visible, bool interactable, string label)
        {
            if (b.gameObject.activeSelf != visible) b.gameObject.SetActive(visible);
            if (!visible) return;
            b.interactable = interactable;
            UIFactory.SetLabel(b, label);
        }

        private void RefreshDialogs(ClientGameState state)
        {
            var request = _model.Request;
            bool open = request != null && !_model.Submitted && !state.IsGameOver;
            var mode = _model.Mode;

            if (open && mode == InteractionMode.ChooseCharacter)
            {
                if (_choice.RequestId != request.RequestId)
                    _choice.ShowCharacters(_dialogs, request, _content, _model.Prompt, i => Send(_model.ChooseOption(i)));
            }
            else if (open && mode == InteractionMode.ChooseOption)
            {
                if (_choice.RequestId != request.RequestId)
                    _choice.ShowOptions(_dialogs, request, _content, _model.Prompt, i => Send(_model.ChooseOption(i)));
            }
            else if (_choice.IsOpen) _choice.Close();

            if (open && mode == InteractionMode.ChooseCardFromPlayer)
            {
                if (_pick.RequestId != request.RequestId)
                    _pick.Show(_dialogs, request, state.GetPlayer(request.TargetPlayerId), _content, _model.Prompt, (zone, index) => Send(_model.PickFromPlayer(zone, index)));
            }
            else if (_pick.IsOpen) _pick.Close();
        }

        private void ShowResult(ClientGameState state)
        {
            CloseDialogs();
            _result.Show(_dialogs, state, _content, _view.ViewerId, ExitTable, _view.IsNetwork ? "返回房间" : "返回");
        }

        private void ExitTable()
        {
            var exit = _onExit;
            _result.Close();
            Detach();
            exit?.Invoke();
        }

        private void QuitGame()
        {
            var quit = _onQuit;
            Detach();
            quit?.Invoke();
        }

        // ================================================================== timers

        private void TrackRequests(ClientGameState state)
        {
            _requestStale.Clear();
            foreach (var id in _requestSeen.Keys) _requestStale.Add(id);
            foreach (var r in state.OpenRequests)
            {
                if (!_requestSeen.ContainsKey(r.RequestId)) _requestSeen[r.RequestId] = Time.unscaledTime;
                _requestStale.Remove(r.RequestId);
            }
            foreach (var id in _requestStale) _requestSeen.Remove(id);
        }

        private float? Remaining(RequestInfo r)
        {
            if (r == null || !_requestSeen.TryGetValue(r.RequestId, out float seen)) return null;
            float duration = Mathf.Max(1f, _config.GetTimeoutMs(r.Kind) / 1000f);
            return Mathf.Clamp01(1f - (Time.unscaledTime - seen) / duration);
        }

        private void UpdateTimers(ClientGameState state)
        {
            var mine = _model.Request;
            float? left = mine != null && !_model.Submitted ? Remaining(mine) : null;
            var bar = _timerFill.transform.parent.gameObject;
            if (bar.activeSelf != left.HasValue) bar.SetActive(left.HasValue);
            if (left.HasValue)
            {
                _timerFill.fillAmount = left.Value;
                _timerFill.color = left.Value < 0.3f ? UITheme.HpLow : UITheme.Gold;
            }
            foreach (var w in _seats.Widgets)
            {
                RequestInfo asked = null;
                foreach (var r in state.OpenRequests)
                    if (r.PlayerId == w.PlayerId) asked = r;
                w.SetTimer(Remaining(asked));
            }
        }

        // ================================================================== log

        private void SetLogVisible(bool visible)
        {
            _logVisible = visible;
            _logPanel.gameObject.SetActive(visible);
            float right = visible ? LogWidth + 20 : 20;
            _table.offsetMax = new Vector2(-right, _table.offsetMax.y);
            _prompt.rectTransform.offsetMax = new Vector2(-right, _prompt.rectTransform.offsetMax.y);
            _logVersion = -1;
        }

        private void RefreshLog()
        {
            _logVersion = _log.Version;
            var lines = _log.Lines;
            _sb.Clear();
            for (int i = Math.Max(0, lines.Count - 60); i < lines.Count; i++)
            {
                if (_sb.Length > 0) _sb.Append('\n');
                _sb.Append(lines[i]);
            }
            _logText.text = _sb.ToString();
            Canvas.ForceUpdateCanvases();
            _logScroll.verticalNormalizedPosition = 0f;
        }

        // ================================================================== connection (LAN)

        private void UpdateConnection()
        {
            if (!(_view is NetworkGameView nv)) return;
            var status = nv.Client.Status;
            string text = status == ClientConnectionStatus.Reconnecting ? "连接中断，正在重连…（AI 暂时代打）" : string.Empty;
            if (_connection.text != text) _connection.text = text;
            if (status == ClientConnectionStatus.Disconnected && _disconnected == null && !(_view.State?.IsGameOver ?? false))
            {
                _disconnected = Modal.Open(_dialogs, "Disconnected", new Vector2(760, 380), null, out var panel);
                Modal.Title(panel, "与主机的连接已断开");
                var body = UIFactory.Text(panel, "Body", nv.Client.LastError ?? "无法重新连接到房间", UITheme.FontBody, UITheme.TextDim);
                UIFactory.Stretch(body.rectTransform, 30, 30, 90, 140);
                var ok = UIFactory.Button(panel, "Ok", "返回大厅", () =>
                {
                    Modal.Close(ref _disconnected);
                    QuitGame();
                });
                UIFactory.Place((RectTransform)ok.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(300, 90));
            }
        }

        // ================================================================== input

        private void Send(GameCommand command)
        {
            if (command == null || _view == null) return;
            GameManager.Instance.Audio.Play(Sfx.Click);
            _view.Send(command);
            _dirty = true;
        }

        private void Confirm() => Send(_model?.Confirm());
        private void Pass() => Send(_model?.Pass());
        private void EndTurn() => Send(_model?.EndTurn());

        private void OnCardClicked(int instanceId)
        {
            if (_model == null) return;
            if (_model.IsCardSelectable(instanceId)) _model.ClickCard(instanceId);
        }

        private void OnSeatClicked(int playerId)
        {
            if (_model == null || playerId < 0) return;
            if (_model.IsTargetSelectable(playerId) || _model.IsTargetSelected(playerId)) _model.ClickPlayer(playerId);
            else ShowPlayerInfo(playerId);
        }

        private void OnSkillClicked(string skillId)
        {
            if (_model == null) return;
            if (_model.IsSkillUsable(skillId)) _model.ClickSkill(skillId);
            else ShowSkillInfo(skillId);
        }

        private void ShowSkillInfo(string skillId)
        {
            if (!_content.Skills.TryGet(skillId, out var skill)) return;
            string kind = skill.IsLordSkill ? "主公技" : skill.IsLimited ? "限定技" : skill.IsLocked ? "锁定技" : skill.Category == SkillCategory.Active ? "主动技" : "技能";
            _infoDialog.Show(_dialogs, "【" + skill.Name + "】" + kind, skill.Description);
        }

        private void ShowPlayerInfo(int playerId)
        {
            var p = _view?.State?.GetPlayer(playerId);
            if (p == null) return;
            _infoDialog.Show(_dialogs, p.Nickname, PlayerText.Details(_content, p));
        }

        private void OpenMenu()
        {
            if (_view == null) return;
            var self = _view.State?.Self;
            bool auto = self != null && self.AIControlled;
            _menu.Show(_dialogs, auto, _logVisible, enabled => _view?.SetAutoPlay(enabled), SetLogVisible,
                _view.IsNetwork ? "离开房间" : "退出游戏", QuitGame);
        }

        // ================================================================== positions for animations

        private Vector3? SeatWorld(int playerId)
        {
            if (_view != null && playerId == _view.ViewerId) return _self.Rect.TransformPoint(_self.Rect.rect.center);
            var w = _seats.Get(playerId);
            return w != null ? w.Rect.position : (Vector3?)null;
        }

        private Vector3 TableWorld() => _table.TransformPoint(_table.rect.center);

        private Vector3 PileWorld()
        {
            var pile = (RectTransform)_center.Find("Pile");
            return pile != null ? pile.position : TableWorld();
        }
    }
}
