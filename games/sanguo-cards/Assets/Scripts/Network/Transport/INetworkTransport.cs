namespace Sanguo.Network
{
    /// <summary>
    /// A message pipe to one peer. Receiving is polled from the game thread (<see cref="TryReceive"/>),
    /// so game logic stays single threaded no matter how the transport does I/O.
    /// </summary>
    public interface IConnection
    {
        int Id { get; }
        bool IsConnected { get; }
        string RemoteAddress { get; }

        /// <summary>Why the connection closed (null while open).</summary>
        string CloseReason { get; }

        void Send(NetworkMessage message);
        bool TryReceive(out NetworkMessage message);
        void Close(string reason);
    }

    /// <summary>Accepts incoming connections (the host side).</summary>
    public interface IConnectionListener
    {
        int Port { get; }
        bool TryAccept(out IConnection connection);
        void Stop();
    }

    /// <summary>
    /// Transport abstraction: TCP for LAN, in-memory for tests and single-device play; a future
    /// internet/relay transport implements the same interface without touching game code.
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>Starts listening. Port 0 picks a free port.</summary>
        IConnectionListener Listen(int port);

        /// <summary>Connects to a host; returns null (and sets <paramref name="error"/>) on failure.</summary>
        IConnection Connect(string host, int port, int timeoutMs, out string error);
    }
}
