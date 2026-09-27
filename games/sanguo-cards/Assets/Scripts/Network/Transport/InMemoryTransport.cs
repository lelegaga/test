using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Sanguo.Network
{
    /// <summary>
    /// In-process transport. Messages still go through frame encoding/decoding, so tests exercise
    /// the exact wire format; also used by the host device to talk to its own server.
    /// </summary>
    public sealed class InMemoryNetworkTransport : INetworkTransport
    {
        private readonly Dictionary<int, InMemoryListener> _listeners = new Dictionary<int, InMemoryListener>();
        private readonly object _lock = new object();
        private int _nextPort = 50000;

        public IConnectionListener Listen(int port)
        {
            lock (_lock)
            {
                if (port == 0) port = _nextPort++;
                var listener = new InMemoryListener(this, port);
                _listeners[port] = listener;
                return listener;
            }
        }

        internal void Remove(int port)
        {
            lock (_lock) _listeners.Remove(port);
        }

        public IConnection Connect(string host, int port, int timeoutMs, out string error)
        {
            InMemoryListener listener;
            lock (_lock) _listeners.TryGetValue(port, out listener);
            if (listener == null)
            {
                error = "No listener on port " + port + ".";
                return null;
            }
            error = null;
            var a = new InMemoryConnection("client");
            var b = new InMemoryConnection("memory-client");
            a.Peer = b;
            b.Peer = a;
            listener.Enqueue(b);
            return a;
        }

        private sealed class InMemoryListener : IConnectionListener
        {
            private readonly InMemoryNetworkTransport _owner;
            private readonly ConcurrentQueue<IConnection> _pending = new ConcurrentQueue<IConnection>();

            public InMemoryListener(InMemoryNetworkTransport owner, int port)
            {
                _owner = owner;
                Port = port;
            }

            public int Port { get; }

            public void Enqueue(IConnection c) => _pending.Enqueue(c);

            public bool TryAccept(out IConnection connection) => _pending.TryDequeue(out connection);

            public void Stop() => _owner.Remove(Port);
        }
    }

    public sealed class InMemoryConnection : IConnection
    {
        private static int _nextId;
        private readonly ConcurrentQueue<NetworkMessage> _incoming = new ConcurrentQueue<NetworkMessage>();
        private readonly FrameDecoder _decoder = new FrameDecoder();
        private readonly List<NetworkMessage> _scratch = new List<NetworkMessage>();
        private readonly object _decodeLock = new object();
        private volatile bool _connected = true;

        public InMemoryConnection(string remoteAddress)
        {
            Id = Interlocked.Increment(ref _nextId);
            RemoteAddress = remoteAddress;
        }

        internal InMemoryConnection Peer;

        public int Id { get; }
        public bool IsConnected => _connected;
        public string RemoteAddress { get; }
        public string CloseReason { get; private set; }

        /// <summary>Test hook: drops every message sent from now on without closing (simulates a dead link).</summary>
        public bool Blackhole { get; set; }

        public void Send(NetworkMessage message)
        {
            if (!_connected || Blackhole) return;
            var peer = Peer;
            if (peer == null || !peer._connected) return;
            peer.Deliver(FrameCodec.Encode(message));
        }

        private void Deliver(byte[] frame)
        {
            lock (_decodeLock)
            {
                _scratch.Clear();
                _decoder.Feed(frame, 0, frame.Length, _scratch);
                foreach (var m in _scratch) _incoming.Enqueue(m);
            }
        }

        public bool TryReceive(out NetworkMessage message) => _incoming.TryDequeue(out message);

        public void Close(string reason)
        {
            if (!_connected) return;
            _connected = false;
            CloseReason = reason;
            var peer = Peer;
            if (peer != null && peer._connected)
            {
                peer._connected = false;
                peer.CloseReason = "remote closed";
            }
        }
    }
}
