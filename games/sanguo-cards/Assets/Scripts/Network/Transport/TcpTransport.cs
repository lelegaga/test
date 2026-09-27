using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Sanguo.Network
{
    /// <summary>TCP transport (reliable, ordered): length-prefixed frames, one reader and one writer thread per connection.</summary>
    public sealed class TcpNetworkTransport : INetworkTransport
    {
        public IConnectionListener Listen(int port) => new TcpConnectionListener(port);

        public IConnection Connect(string host, int port, int timeoutMs, out string error)
        {
            error = null;
            var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
            try
            {
                var result = client.BeginConnect(host, port, null, null);
                if (!result.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    client.Close();
                    error = "Connection timed out.";
                    return null;
                }
                client.EndConnect(result);
                return new TcpConnection(client);
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException || ex is ArgumentException)
            {
                client.Close();
                error = ex.Message;
                return null;
            }
        }
    }

    public sealed class TcpConnectionListener : IConnectionListener
    {
        private readonly TcpListener _listener;
        private readonly ConcurrentQueue<IConnection> _accepted = new ConcurrentQueue<IConnection>();
        private readonly Thread _thread;
        private volatile bool _running = true;

        public TcpConnectionListener(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "SanguoAccept" };
            _thread.Start();
        }

        public int Port { get; }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    client.NoDelay = true;
                    _accepted.Enqueue(new TcpConnection(client));
                }
                catch (SocketException)
                {
                    if (!_running) return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        public bool TryAccept(out IConnection connection) => _accepted.TryDequeue(out connection);

        public void Stop()
        {
            _running = false;
            try
            {
                _listener.Stop();
            }
            catch (SocketException)
            {
            }
            while (_accepted.TryDequeue(out var c)) c.Close("server stopped");
        }
    }

    public sealed class TcpConnection : IConnection
    {
        /// <summary>Pending outgoing frames beyond which the peer is considered stuck and dropped.</summary>
        public const int MaxSendQueue = 2048;

        private static int _nextId;

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly ConcurrentQueue<NetworkMessage> _incoming = new ConcurrentQueue<NetworkMessage>();
        private readonly BlockingCollection<byte[]> _outgoing = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        private readonly Thread _reader;
        private readonly Thread _writer;
        private volatile bool _connected = true;
        private volatile string _closeReason;
        private int _closed;

        public TcpConnection(TcpClient client)
        {
            Id = Interlocked.Increment(ref _nextId);
            _client = client;
            _stream = client.GetStream();
            RemoteAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";
            _reader = new Thread(ReadLoop) { IsBackground = true, Name = "SanguoRead" + Id };
            _writer = new Thread(WriteLoop) { IsBackground = true, Name = "SanguoWrite" + Id };
            _reader.Start();
            _writer.Start();
        }

        public int Id { get; }
        public bool IsConnected => _connected;
        public string RemoteAddress { get; }
        public string CloseReason => _closeReason;

        public void Send(NetworkMessage message)
        {
            if (!_connected) return;
            if (_outgoing.Count >= MaxSendQueue)
            {
                Close("send queue overflow");
                return;
            }
            try
            {
                _outgoing.Add(FrameCodec.Encode(message));
            }
            catch (InvalidOperationException)
            {
                // Completed concurrently by Close.
            }
        }

        public bool TryReceive(out NetworkMessage message) => _incoming.TryDequeue(out message);

        private void ReadLoop()
        {
            var buffer = new byte[16 * 1024];
            var decoder = new FrameDecoder();
            var decoded = new List<NetworkMessage>();
            try
            {
                while (_connected)
                {
                    int n = _stream.Read(buffer, 0, buffer.Length);
                    if (n <= 0)
                    {
                        Close("remote closed");
                        return;
                    }
                    decoded.Clear();
                    decoder.Feed(buffer, 0, n, decoded);
                    foreach (var m in decoded) _incoming.Enqueue(m);
                }
            }
            catch (ProtocolException ex)
            {
                Close("protocol error: " + ex.Message);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                Close("connection lost");
            }
        }

        private void WriteLoop()
        {
            try
            {
                foreach (var frame in _outgoing.GetConsumingEnumerable())
                {
                    _stream.Write(frame, 0, frame.Length);
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                Close("connection lost");
            }
        }

        public void Close(string reason)
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1) return;
            _closeReason = reason;
            _connected = false;
            try
            {
                _outgoing.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
            }
            // Give the writer a moment to flush a final message (e.g. a Disconnect notice).
            if (Thread.CurrentThread != _writer) _writer.Join(200);
            try
            {
                _client.Close();
            }
            catch (Exception)
            {
                // Closing a broken socket may throw; the connection is gone either way.
            }
        }
    }
}
