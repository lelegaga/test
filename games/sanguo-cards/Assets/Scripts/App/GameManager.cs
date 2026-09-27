using System;
using System.Collections.Generic;
using Sanguo.Data;
using Sanguo.Game;
using Sanguo.GameModes;
using UnityEngine;

namespace Sanguo.App
{
    /// <summary>
    /// Top-level coordinator of the Unity app. It only wires modules together (content, sessions,
    /// later networking, UI and saves); rules live in the engine-free Sanguo.Core assembly.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameContent Content { get; private set; }
        public GameModeRegistry Modes { get; private set; }

        /// <summary>The locally hosted session (single player now; LAN host later).</summary>
        public GameSession LocalSession { get; private set; }

        public event Action<GameSession> LocalSessionStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Content = ContentLoader.Load(new ResourcesContentSource());
            Modes = GameModeRegistry.CreateDefault();
            Debug.Log("[Sanguo] Content loaded: " + Content.Cards.Count + " cards, " + Content.Characters.Count + " characters.");
        }

        private void Start()
        {
            // Until the real UI exists (stage 8) the app boots straight into a watchable bot game.
            if (GetComponent<DebugGameView>() == null) gameObject.AddComponent<DebugGameView>();
        }

        /// <summary>Starts a local game: human seats first, then bots.</summary>
        public GameSession StartLocalGame(GameModeConfig config, IList<string> humanNames, int seed)
        {
            var setups = new List<PlayerSetup>();
            for (int i = 0; i < config.PlayerCount; i++)
            {
                bool human = humanNames != null && i < humanNames.Count;
                setups.Add(new PlayerSetup(human ? humanNames[i] : "AI " + (i + 1), !human));
            }
            var engine = new GameEngine(Content, Modes.Create(config), config, setups, seed, "local")
            {
                RethrowInternalErrors = Application.isEditor
            };
            LocalSession = new GameSession(engine, new UnityClock()) { AIThinkDelayMs = 350 };
            LocalSession.Start();
            LocalSessionStarted?.Invoke(LocalSession);
            return LocalSession;
        }

        private void Update()
        {
            LocalSession?.Update();
        }
    }
}
