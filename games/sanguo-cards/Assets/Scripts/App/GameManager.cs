using System;
using System.Collections.Generic;
using Sanguo.Audio;
using Sanguo.Data;
using Sanguo.Game;
using Sanguo.GameModes;
using Sanguo.UI;
using UnityEngine;

namespace Sanguo.App
{
    /// <summary>
    /// Top-level coordinator of the Unity app. It only wires modules together (content, profile,
    /// networking, audio, UI, local sessions); rules live in the engine-free Sanguo.Core assembly and
    /// networking in Sanguo.Network.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameContent Content { get; private set; }
        public GameModeRegistry Modes { get; private set; }
        public ProfileService Profiles { get; private set; }
        public NetworkManager Network { get; private set; }
        public AudioManager Audio { get; private set; }
        public UIRoot UI { get; private set; }

        /// <summary>The single-player session running on this device, if any.</summary>
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
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Content = ContentLoader.Load(new ResourcesContentSource());
            Modes = GameModeRegistry.CreateDefault();
            Profiles = new ProfileService();
            Profiles.Load();
            Network = gameObject.AddComponent<NetworkManager>();
            Audio = gameObject.AddComponent<AudioManager>();
            Audio.SfxVolume = Profiles.Profile.SfxVolume;
            EnsureCamera();
            UI = UIRoot.Create();
            Debug.Log("[Sanguo] Content loaded: " + Content.Cards.Count + " cards, " + Content.Characters.Count + " characters.");
        }

        private void Start()
        {
            UI.Show<MainMenuScreen>();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            DontDestroyOnLoad(go);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UITheme.Background;
            cam.cullingMask = 0;
            cam.orthographic = true;
        }

        /// <summary>Starts a single-player game: the human takes seat 0, bots fill the rest.</summary>
        public GameSession StartLocalGame(GameModeConfig config, string humanName, int seed)
        {
            StopLocalGame();
            var setups = new List<PlayerSetup>();
            for (int i = 0; i < config.PlayerCount; i++)
                setups.Add(i == 0 ? new PlayerSetup(humanName) { AvatarId = Profiles.Profile.AvatarId } : new PlayerSetup("AI-" + i, true) { AvatarId = -1 });
            var engine = new GameEngine(Content, Modes.Create(config), config, setups, seed, "local")
            {
                RethrowInternalErrors = Application.isEditor
            };
            LocalSession = new GameSession(engine, new UnityClock()) { AIThinkDelayMs = 700 };
            LocalSession.Start();
            LocalSessionStarted?.Invoke(LocalSession);
            return LocalSession;
        }

        public void StopLocalGame()
        {
            LocalSession = null;
        }

        private void Update()
        {
            LocalSession?.Update();
        }
    }
}
