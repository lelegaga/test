using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace Sanguo.Network
{
    /// <summary>Local network helpers (LAN address for invite codes, broadcast targets).</summary>
    public static class LocalNetwork
    {
        /// <summary>Best guess of this device's LAN IPv4 address (private ranges preferred).</summary>
        public static IPAddress GetLanIPv4()
        {
            IPAddress fallback = null;
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;
                        if (IsPrivate(ua.Address)) return ua.Address;
                        fallback = fallback ?? ua.Address;
                    }
                }
            }
            catch (Exception ex) when (ex is NetworkInformationException || ex is NotSupportedException || ex is PlatformNotSupportedException)
            {
                // Some platforms restrict interface enumeration; fall through to the socket trick.
            }
            if (fallback != null) return fallback;
            try
            {
                // Connecting a UDP socket sends nothing but reveals the outgoing interface address.
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("10.255.255.255", 1);
                    return ((IPEndPoint)s.LocalEndPoint).Address;
                }
            }
            catch (SocketException)
            {
                return IPAddress.Loopback;
            }
        }

        public static bool IsPrivate(IPAddress a)
        {
            var b = a.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }

        /// <summary>Limited broadcast plus the directed broadcast address of every IPv4 interface.</summary>
        public static List<IPEndPoint> BroadcastTargets(int port)
        {
            var list = new List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, port) };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        var ip = ua.Address.GetAddressBytes();
                        var mask = ua.IPv4Mask.GetAddressBytes();
                        var bcast = new byte[4];
                        for (int i = 0; i < 4; i++) bcast[i] = (byte)(ip[i] | ~mask[i]);
                        var ep = new IPEndPoint(new IPAddress(bcast), port);
                        if (!list.Exists(e => e.Equals(ep))) list.Add(ep);
                    }
                }
            }
            catch (Exception ex) when (ex is NetworkInformationException || ex is NotSupportedException || ex is PlatformNotSupportedException)
            {
                // Limited broadcast only.
            }
            return list;
        }
    }

    internal static class DiscoveryPacket
    {
        public static readonly byte[] Magic = { (byte)'S', (byte)'G', (byte)'R', (byte)'M' };

        public static byte[] Encode(RoomInfo info)
        {
            var w = new NetWriter(128);
            w.WriteRaw(Magic, 0, Magic.Length);
            RoomCodec.WriteInfo(w, info);
            return w.ToArray();
        }

        public static RoomInfo Decode(byte[] data)
        {
            if (data.Length < 4 || data[0] != Magic[0] || data[1] != Magic[1] || data[2] != Magic[2] || data[3] != Magic[3]) return null;
            try
            {
                return RoomCodec.ReadInfo(new NetReader(data, 4, data.Length - 4));
            }
            catch (ProtocolException)
            {
                return null;
            }
        }
    }

    /// <summary>Host side: periodically broadcasts the room announcement over UDP.</summary>
    public sealed class LanRoomAnnouncer : IDisposable
    {
        private readonly UdpClient _udp;
        private readonly List<IPEndPoint> _targets;
        private long _lastSentMs;
        private bool _sentOnce;

        public LanRoomAnnouncer(int discoveryPort = ProtocolInfo.DiscoveryPort, IEnumerable<IPEndPoint> targets = null)
        {
            _udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            _targets = targets != null ? new List<IPEndPoint>(targets) : LocalNetwork.BroadcastTargets(discoveryPort);
        }

        public int IntervalMs { get; set; } = 1000;

        /// <summary>Sends the announcement when due. Errors (no network) are swallowed; discovery is best effort.</summary>
        public void Update(long nowMs, RoomInfo info)
        {
            if (_sentOnce && nowMs - _lastSentMs < IntervalMs) return;
            _sentOnce = true;
            _lastSentMs = nowMs;
            var packet = DiscoveryPacket.Encode(info);
            foreach (var target in _targets)
            {
                try
                {
                    _udp.Send(packet, packet.Length, target);
                }
                catch (SocketException)
                {
                }
            }
        }

        public void Dispose() => _udp.Close();
    }

    /// <summary>Client side: listens for room announcements and keeps a list of live rooms.</summary>
    public sealed class LanRoomBrowser : IDisposable
    {
        private readonly UdpClient _udp;
        private readonly ConcurrentQueue<RoomInfo> _received = new ConcurrentQueue<RoomInfo>();
        private readonly Dictionary<string, RoomInfo> _rooms = new Dictionary<string, RoomInfo>();
        private readonly Thread _thread;
        private volatile bool _running = true;

        public LanRoomBrowser(int discoveryPort = ProtocolInfo.DiscoveryPort)
        {
            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.EnableBroadcast = true;
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "SanguoDiscovery" };
            _thread.Start();
        }

        /// <summary>Rooms not heard from for this long disappear from the list.</summary>
        public int ExpiryMs { get; set; } = 3500;

        private void ReceiveLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    var data = _udp.Receive(ref any);
                    var info = DiscoveryPacket.Decode(data);
                    if (info == null || info.ProtocolVersion != ProtocolInfo.Version) continue;
                    // The packet source is more reliable than a self-reported address.
                    info.Address = any.Address.ToString();
                    _received.Enqueue(info);
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

        /// <summary>Merges received announcements and drops expired rooms; returns the current list.</summary>
        public List<RoomInfo> Update(long nowMs)
        {
            while (_received.TryDequeue(out var info))
            {
                info.LastSeenMs = nowMs;
                _rooms[info.RoomId + "@" + info.Address + ":" + info.Port] = info;
            }
            var stale = new List<string>();
            foreach (var kv in _rooms)
                if (nowMs - kv.Value.LastSeenMs > ExpiryMs) stale.Add(kv.Key);
            foreach (var k in stale) _rooms.Remove(k);
            var list = new List<RoomInfo>(_rooms.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.RoomName, b.RoomName));
            return list;
        }

        public void Clear() => _rooms.Clear();

        public void Dispose()
        {
            _running = false;
            _udp.Close();
        }
    }
}
