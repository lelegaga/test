using System;
using System.Collections.Generic;
using Sanguo.AI;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.Utils;

namespace Sanguo.Game
{
    /// <summary>
    /// Host-side orchestration of one game, independent of transport: owns the engine, runs AI seats,
    /// applies timeouts, handles disconnection/AI takeover and delivers per-viewer projected events.
    /// Used identically by single player (local viewer), the LAN host and a future dedicated server.
    /// </summary>
    public sealed class GameSession
    {
        private readonly IClock _clock;
        private readonly Dictionary<int, AIController> _ai = new Dictionary<int, AIController>();
        private readonly Dictionary<int, long> _disconnectedAt = new Dictionary<int, long>();
        private readonly HashSet<int> _autoPlay = new HashSet<int>();
        private readonly Dictionary<int, List<Action<GameEvent>>> _viewers = new Dictionary<int, List<Action<GameEvent>>>();
        private readonly List<GameEvent> _buffer = new List<GameEvent>(64);
        private readonly Func<int, IAIDecisionMaker> _brainFactory;
        private bool _flushing;

        public GameSession(GameEngine engine, IClock clock, Func<int, IAIDecisionMaker> brainFactory = null)
        {
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _clock = clock ?? new SystemClock();
            _brainFactory = brainFactory;
            EstimatorFactory = _ => AIRelationEstimators.CreateFor(engine.Context.Mode);
        }

        public GameEngine Engine { get; }
        public GameState State => Engine.State;

        /// <summary>Full (unprojected) server events. Host-side observers only (logs, replays, AI bookkeeping).</summary>
        public GameEventBus ServerEvents { get; } = new GameEventBus();

        /// <summary>Minimum time an AI waits before answering (UX pacing). 0 = instant.</summary>
        public int AIThinkDelayMs { get; set; }

        /// <summary>Relation estimator factory for AI seats (identity mode inference). Set before Start.</summary>
        public Func<int, IRelationEstimator> EstimatorFactory { get; set; }

        private bool _aiPrepared;

        public long Now => _clock.NowMs;

        public void Start()
        {
            EnsureAIControllers();
            Engine.Start(Now);
            Flush();
        }

        /// <summary>
        /// Creates an AI controller for every seat up front (any human may later be handed to the AI)
        /// so relation estimators observe the whole game from that seat's point of view.
        /// </summary>
        private void EnsureAIControllers()
        {
            if (_aiPrepared) return;
            _aiPrepared = true;
            foreach (var p in State.Players) GetAI(p.PlayerId);
        }

        /// <summary>
        /// Registers a receiver of projected events for a viewer (player id, or -1 for a spectator).
        /// The receiver should first get <see cref="GetSnapshot"/> and then apply events in order.
        /// </summary>
        public IDisposable AddViewer(int viewerId, Action<GameEvent> sink)
        {
            if (!_viewers.TryGetValue(viewerId, out var list))
            {
                list = new List<Action<GameEvent>>();
                _viewers[viewerId] = list;
            }
            list.Add(sink);
            return new Unsubscriber(() => list.Remove(sink));
        }

        public ClientGameState GetSnapshot(int viewerId) => Engine.CreateSnapshot(viewerId);

        /// <summary>Command from a human client (already authenticated as <see cref="GameCommand.PlayerId"/> by the transport).</summary>
        public CommandResult Submit(GameCommand command)
        {
            var result = Engine.Submit(command, Now);
            Flush();
            return result;
        }

        /// <summary>Advance time: timeouts, disconnection takeover and AI moves. Call regularly (e.g. every frame).</summary>
        public void Update()
        {
            if (!Engine.IsStarted || Engine.IsGameOver)
            {
                Flush();
                return;
            }
            EnsureAIControllers();
            CheckDisconnections();
            Engine.Tick(Now);
            Flush();
            RunAI(false);
        }

        /// <summary>
        /// Plays AI seats immediately (ignoring think delay) until a human must act or the game ends.
        /// Returns true if the game is over. Used by simulations and tests.
        /// </summary>
        public bool RunUntilHumanInputOrEnd(int maxCommands = 100000)
        {
            EnsureAIControllers();
            for (int i = 0; i < maxCommands && !Engine.IsGameOver; i++)
            {
                if (!RunAI(true)) break;
            }
            Flush();
            return Engine.IsGameOver;
        }

        /// <summary>Answers at most one AI request. Returns true if an AI acted.</summary>
        private bool RunAI(bool ignoreDelay)
        {
            bool acted = false;
            for (int guard = 0; guard < 1000 && !Engine.IsGameOver; guard++)
            {
                PendingRequest ready = null;
                foreach (var r in Engine.OpenRequests)
                {
                    var p = State.GetPlayer(r.PlayerId);
                    if (p == null || !p.AIControlled) continue;
                    if (!ignoreDelay && Now - r.OpenedAtMs < AIThinkDelayMs) continue;
                    ready = r;
                    break;
                }
                if (ready == null) break;
                ActFor(ready);
                acted = true;
                if (ignoreDelay) break;
            }
            return acted;
        }

        private void ActFor(PendingRequest request)
        {
            var ai = GetAI(request.PlayerId);
            GameCommand cmd = null;
            try
            {
                cmd = ai.Decide(Engine.Context, request);
            }
            catch (Exception ex)
            {
                if (Engine.RethrowInternalErrors) throw;
                Engine.RecordExternalError("AI failure: " + ex.Message);
            }
            if (cmd != null)
            {
                var result = Engine.SubmitFromHost(cmd, Now);
                if (result.Accepted)
                {
                    Flush();
                    return;
                }
                if (Engine.RethrowInternalErrors)
                    throw new InvalidOperationException("AI produced an illegal command " + cmd + " for " + request + ": " + result);
            }
            // Fallback: never let a bot stall the table.
            Engine.ResolveWithDefault(request, Now);
            Flush();
        }

        private AIController GetAI(int playerId)
        {
            if (!_ai.TryGetValue(playerId, out var ai))
            {
                var brain = _brainFactory?.Invoke(playerId) ?? new HeuristicAI();
                var estimator = EstimatorFactory?.Invoke(playerId);
                ai = new AIController(playerId, brain, Engine.Seed, estimator);
                _ai[playerId] = ai;
                if (estimator is IGameEventObserver observer) AddViewer(playerId, observer.Observe);
            }
            return ai;
        }

        // ------------------------------------------------------------------ connection / takeover

        public void SetConnected(int playerId, bool connected)
        {
            var p = State.GetPlayer(playerId);
            if (p == null) return;
            if (connected)
            {
                _disconnectedAt.Remove(playerId);
                bool ai = p.IsBot || _autoPlay.Contains(playerId);
                Engine.SetPlayerControl(playerId, true, ai);
            }
            else
            {
                if (!_disconnectedAt.ContainsKey(playerId)) _disconnectedAt[playerId] = Now;
                Engine.SetPlayerControl(playerId, false, p.AIControlled);
            }
            Flush();
        }

        /// <summary>Player-requested auto play (托管). Remains until turned off.</summary>
        public void SetAutoPlay(int playerId, bool enabled)
        {
            var p = State.GetPlayer(playerId);
            if (p == null) return;
            if (enabled) _autoPlay.Add(playerId);
            else _autoPlay.Remove(playerId);
            Engine.SetPlayerControl(playerId, p.Connected, enabled || p.IsBot || IsTakeoverDue(playerId));
            Flush();
        }

        private bool IsTakeoverDue(int playerId)
        {
            return _disconnectedAt.TryGetValue(playerId, out long at) && Now - at >= State.Config.DisconnectAITakeoverMs;
        }

        private void CheckDisconnections()
        {
            if (_disconnectedAt.Count == 0) return;
            foreach (var kv in _disconnectedAt)
            {
                var p = State.GetPlayer(kv.Key);
                if (p != null && !p.AIControlled && IsTakeoverDue(kv.Key)) Engine.SetPlayerControl(kv.Key, false, true);
            }
        }

        // ------------------------------------------------------------------ event delivery

        private void Flush()
        {
            if (_flushing) return;
            _flushing = true;
            try
            {
                while (true)
                {
                    _buffer.Clear();
                    Engine.DrainEvents(_buffer);
                    if (_buffer.Count == 0) break;
                    foreach (var e in _buffer)
                    {
                        ServerEvents.Publish(e);
                        foreach (var kv in _viewers)
                        {
                            if (kv.Value.Count == 0) continue;
                            var projected = e.ProjectFor(kv.Key);
                            var sinks = kv.Value.ToArray();
                            foreach (var sink in sinks) sink(projected);
                        }
                    }
                }
            }
            finally
            {
                _flushing = false;
            }
        }

        private sealed class Unsubscriber : IDisposable
        {
            private Action _action;

            public Unsubscriber(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action?.Invoke();
                _action = null;
            }
        }
    }
}
