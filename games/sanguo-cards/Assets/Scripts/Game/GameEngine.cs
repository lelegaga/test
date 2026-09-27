using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Utils;

namespace Sanguo.Game
{
    /// <summary>
    /// The authoritative rules engine for one game (runs on the host / dedicated server only).
    ///
    ///   Client → Command → <see cref="Submit"/> → sequence check → GameRuleEngine validation →
    ///   resolve the open request → run the action stack until it needs input → events.
    ///
    /// The engine is single threaded and deterministic: the same seed, setup and command list
    /// always produce the same events, which enables replays and reproducible bug reports.
    /// </summary>
    public sealed class GameEngine
    {
        /// <summary>Upper bound of action steps per command; exceeding it means a rules bug (runaway loop).</summary>
        public const int MaxStepsPerRun = 200000;

        private readonly List<PlayerSetup> _setups;
        private readonly CommandSequencer _sequencer = new CommandSequencer();

        public GameEngine(GameContent content, IGameMode mode, GameModeConfig config, IReadOnlyList<PlayerSetup> players, int seed,
            string roomId = "", bool keepEventHistory = false)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (mode == null) throw new ArgumentNullException(nameof(mode));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (players == null) throw new ArgumentNullException(nameof(players));
            var v = mode.ValidateSetup(config, players.Count);
            if (!v.IsValid) throw new ArgumentException("Invalid game setup: " + v.Message);

            _setups = new List<PlayerSetup>(players);
            var state = new GameState(roomId, config);
            for (int i = 0; i < players.Count; i++)
            {
                var ps = players[i];
                var p = new PlayerState(i, ps.Nickname)
                {
                    AvatarId = ps.AvatarId,
                    IsBot = ps.IsBot,
                    AIControlled = ps.IsBot,
                    Team = ps.Team,
                    Seat = i
                };
                state.AddPlayer(p);
            }
            state.RebuildSeatOrder();
            Seed = seed;
            Context = new GameContext(state, content, mode, new XorShiftRandom(seed), keepEventHistory);
        }

        public GameContext Context { get; }
        public GameState State => Context.State;
        public int Seed { get; }
        public bool IsStarted { get; private set; }
        public bool IsGameOver => State.IsGameOver;

        /// <summary>When false, internal errors end the game as a draw instead of throwing (production hosts).</summary>
        public bool RethrowInternalErrors { get; set; } = true;

        /// <summary>Last internal error message, if any.</summary>
        public string LastError { get; private set; }

        public IReadOnlyList<PendingRequest> OpenRequests => Context.Requests.OpenRequests;

        public void Start(long nowMs)
        {
            if (IsStarted) throw new InvalidOperationException("Game already started.");
            IsStarted = true;
            Context.NowMs = nowMs;
            Context.Push(new GameFlowAction(_setups));
            Guarded(Run);
        }

        /// <summary>Last sequence number accepted from a player (clients resume from it after reconnecting).</summary>
        public int GetLastSequence(int playerId) => _sequencer.GetLast(playerId);

        public CommandResult Submit(GameCommand command, long nowMs)
        {
            if (command == null) return CommandResult.Reject(RejectReason.MalformedCommand);
            if (!IsStarted) return CommandResult.Reject(RejectReason.GameNotRunning);
            if (State.IsGameOver) return CommandResult.Reject(RejectReason.GameOver);
            if (State.GetPlayer(command.PlayerId) == null) return CommandResult.Reject(RejectReason.UnknownPlayer);

            switch (_sequencer.Check(command.PlayerId, command.SequenceNumber))
            {
                case SequenceCheck.Duplicate:
                    return CommandResult.Reject(RejectReason.DuplicateSequence, "Sequence " + command.SequenceNumber + " already used.");
                case SequenceCheck.OutOfOrder:
                    return CommandResult.Reject(RejectReason.OutOfOrderSequence,
                        "Expected sequence " + (_sequencer.GetLast(command.PlayerId) + 1) + ", got " + command.SequenceNumber + ".");
            }
            _sequencer.Consume(command.PlayerId, command.SequenceNumber);

            Context.NowMs = nowMs;
            var v = Context.Rules.ValidateCommand(command, out var request);
            if (!v.IsValid) return CommandResult.Reject(v);

            bool ok = Guarded(() =>
            {
                Context.Requests.Resolve(Context, request, command, false);
                Run();
            });
            return ok ? CommandResult.Ok() : CommandResult.Reject(RejectReason.InternalError, LastError);
        }

        /// <summary>Applies default answers to expired requests. Returns how many timed out.</summary>
        public int Tick(long nowMs)
        {
            if (!IsStarted || State.IsGameOver) return 0;
            Context.NowMs = nowMs;
            int count = 0;
            Guarded(() =>
            {
                for (int guard = 0; guard < 10000 && !State.IsGameOver; guard++)
                {
                    var expired = Context.Requests.FindExpired(nowMs);
                    if (expired == null) break;
                    ForceDefault(expired);
                    count++;
                    Run();
                }
            });
            return count;
        }

        private void ForceDefault(PendingRequest request)
        {
            var response = request.CreateDefaultResponse(Context);
            if (response != null)
            {
                response.PlayerId = request.PlayerId;
                response.RequestId = request.RequestId;
            }
            if (response == null || !request.Validate(Context, response).IsValid)
                throw new GameFlowException("Default response for " + request + " is invalid.");
            Context.Requests.Resolve(Context, request, response, true);
        }

        /// <summary>Resolves an open request with its default answer and continues resolution (bot fallback).</summary>
        public void ResolveWithDefault(PendingRequest request, long nowMs)
        {
            if (request == null || request.IsClosed || State.IsGameOver) return;
            Context.NowMs = nowMs;
            Guarded(() =>
            {
                ForceDefault(request);
                Run();
            });
        }

        /// <summary>Records an error raised outside the engine (e.g. by an AI) for diagnostics.</summary>
        public void RecordExternalError(string message)
        {
            LastError = message;
        }

        public void DrainEvents(List<GameEvent> output) => Context.Events.Drain(output);

        public ClientGameState CreateSnapshot(int viewerId) => SnapshotBuilder.Build(Context, viewerId);

        /// <summary>Marks a seat as connected/disconnected and AI controlled (host decides when).</summary>
        public void SetPlayerControl(int playerId, bool connected, bool aiControlled)
        {
            var p = State.GetPlayer(playerId);
            if (p == null) return;
            Context.Mutator.SetConnection(p, connected, aiControlled);
        }

        private void Run()
        {
            var ctx = Context;
            int steps = 0;
            while (!State.IsGameOver && ctx.Stack.Count > 0)
            {
                if (ctx.Requests.HasOpen) return;
                var top = ctx.Stack.Peek();
                var result = top.Step(ctx);
                if (++steps > MaxStepsPerRun)
                    throw new GameFlowException("Resolution exceeded " + MaxStepsPerRun + " steps (last action " + top + ").");
                if (result == ActionResult.Done) ctx.Stack.Remove(top);
                else if (result == ActionResult.Wait && !ctx.Requests.HasOpen)
                    throw new GameFlowException(top + " returned Wait without opening a request.");
            }
            if (State.IsGameOver)
            {
                ctx.Stack.Clear(ctx);
                ctx.Requests.AbortAll();
            }
        }

        private bool Guarded(Action body)
        {
            try
            {
                body();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.GetType().Name + ": " + ex.Message;
                if (RethrowInternalErrors) throw;
                // Keep every client consistent: stop the game instead of continuing from a broken state.
                if (!State.IsGameOver)
                {
                    try
                    {
                        Context.Mutator.EndGame(GameResult.Draw("engine_error"));
                    }
                    catch (Exception)
                    {
                        // Ending the game failed as well; leave the state as is.
                    }
                }
                Context.Stack.Clear(Context);
                Context.Requests.AbortAll();
                return false;
            }
        }
    }
}
