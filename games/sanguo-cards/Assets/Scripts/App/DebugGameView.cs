using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.GameModes;
using UnityEngine;

namespace Sanguo.App
{
    /// <summary>
    /// Temporary IMGUI view used until the mobile UI (stage 8): shows a bot game as a client would see
    /// it (snapshot + projected events for one seat), with the table state and the game log.
    /// </summary>
    public sealed class DebugGameView : MonoBehaviour
    {
        private const int MaxLines = 400;

        private readonly List<string> _log = new List<string>();
        private ClientGameState _view;
        private IGameLogNames _names;
        private GameSession _session;
        private Vector2 _scroll;
        private int _viewer;
        private int _games;

        private void Start()
        {
            NewGame();
        }

        private void NewGame()
        {
            var config = new GameModeConfig
            {
                ModeId = FreeForAllMode.Id,
                PlayerCount = 4,
                StartingHandSize = 4,
                DrawPerTurn = 2,
                CharacterSelection = CharacterSelectionMode.Random
            };
            _games++;
            _log.Clear();
            _session = GameManager.Instance.StartLocalGame(config, null, System.Environment.TickCount);
            _viewer = 0;
            _view = _session.GetSnapshot(_viewer);
            _names = GameLogNames.ForClient(GameManager.Instance.Content, _view);
            _session.AddViewer(_viewer, OnEvent);
            Add("—— 第 " + _games + " 局开始（视角：" + _view.GetPlayer(_viewer).Nickname + "）——");
        }

        private void OnEvent(GameEvent e)
        {
            if (!_view.Apply(e))
            {
                // Sequence gap or inconsistency: resynchronise from a fresh snapshot.
                _view = _session.GetSnapshot(_viewer);
                _names = GameLogNames.ForClient(GameManager.Instance.Content, _view);
            }
            string line = GameLogFormatter.Format(e, _names, _viewer);
            if (line != null) Add(line);
        }

        private void Add(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLines) _log.RemoveAt(0);
            _scroll.y = float.MaxValue;
        }

        private void OnGUI()
        {
            if (_view == null) return;
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            GUILayout.BeginArea(new Rect(10, 10, w * 0.38f, h - 20), GUI.skin.box);
            GUILayout.Label("三国身份牌 · 调试对局  回合 " + _view.TurnNumber + " / 轮 " + _view.Round + "  阶段 " + _view.Phase);
            foreach (var p in _view.Players)
            {
                string cur = p.PlayerId == _view.CurrentPlayerId ? "▶ " : "   ";
                string state = p.Alive ? p.Hp + "/" + p.MaxHp : "阵亡";
                GUILayout.Label(cur + "座" + p.Seat + " " + p.Nickname + " [" + p.CharacterId + "] 生命 " + state + " 手牌 " + p.HandCount);
            }
            GUILayout.Label("牌堆 " + _view.DrawPileCount + "  弃牌堆 " + _view.DiscardPile.Count);
            if (_view.IsGameOver && GUILayout.Button("再来一局", GUILayout.Height(40))) NewGame();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(w * 0.4f, 10, w * 0.6f - 20, h - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var line in _log) GUILayout.Label(line);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
