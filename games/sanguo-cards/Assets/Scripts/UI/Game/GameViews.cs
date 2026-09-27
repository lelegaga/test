using System;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.Network;

namespace Sanguo.UI
{
    /// <summary>
    /// What the table screen needs from a game, whether it runs on this device (single player) or on
    /// a LAN host. In both cases the UI only reads a projected <see cref="ClientGameState"/> and sends
    /// intents; it never touches the authoritative state.
    /// </summary>
    public interface IGameView
    {
        ClientGameState State { get; }
        int ViewerId { get; }
        GameEventBus Events { get; }
        bool IsNetwork { get; }

        /// <summary>The replica was replaced by a fresh snapshot (reconnect/resync): rebuild everything.</summary>
        event Action StateReplaced;

        event Action<CommandResult> CommandRejected;

        void Send(GameCommand command);
        void SetAutoPlay(bool enabled);
        void Leave();
    }

    /// <summary>Single player: the session runs in-process; this is the human seat's client view.</summary>
    public sealed class LocalGameView : IGameView
    {
        private readonly GameSession _session;
        private readonly IDisposable _subscription;

        public LocalGameView(GameSession session, int viewerId)
        {
            _session = session;
            ViewerId = viewerId;
            State = session.GetSnapshot(viewerId);
            _subscription = session.AddViewer(viewerId, OnEvent);
        }

        public ClientGameState State { get; private set; }
        public int ViewerId { get; }
        public GameEventBus Events { get; } = new GameEventBus();
        public bool IsNetwork => false;

        public event Action StateReplaced;
        public event Action<CommandResult> CommandRejected;

        private void OnEvent(GameEvent e)
        {
            if (State.Apply(e))
            {
                Events.Publish(e);
                return;
            }
            State = _session.GetSnapshot(ViewerId);
            StateReplaced?.Invoke();
        }

        public void Send(GameCommand command)
        {
            command.PlayerId = ViewerId;
            command.SequenceNumber = _session.Engine.GetLastSequence(ViewerId) + 1;
            var result = _session.Submit(command);
            if (!result.Accepted) CommandRejected?.Invoke(result);
        }

        public void SetAutoPlay(bool enabled) => _session.SetAutoPlay(ViewerId, enabled);

        public void Leave() => _subscription.Dispose();
    }

    /// <summary>LAN play: the replica and event stream come from the <see cref="GameClient"/>.</summary>
    public sealed class NetworkGameView : IGameView
    {
        private readonly GameClient _client;

        public NetworkGameView(GameClient client)
        {
            _client = client;
            _client.GameStateReplaced += OnReplaced;
            _client.CommandRejected += OnRejected;
        }

        public ClientGameState State => _client.Game;
        public int ViewerId => _client.SlotId;
        public GameEventBus Events => _client.Events;
        public bool IsNetwork => true;
        public GameClient Client => _client;

        public event Action StateReplaced;
        public event Action<CommandResult> CommandRejected;

        private void OnReplaced(ClientGameState s) => StateReplaced?.Invoke();

        private void OnRejected(int sequence, CommandResult result) => CommandRejected?.Invoke(result);

        public void Send(GameCommand command) => _client.Send(command);

        public void SetAutoPlay(bool enabled) => _client.SetAutoPlay(enabled);

        public void Leave()
        {
            _client.GameStateReplaced -= OnReplaced;
            _client.CommandRejected -= OnRejected;
        }
    }
}
