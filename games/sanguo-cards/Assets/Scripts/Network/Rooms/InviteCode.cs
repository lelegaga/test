using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sanguo.Network
{
    /// <summary>
    /// Room invite codes: the host's IPv4 address and port offset packed into 8 Crockford base32
    /// characters (e.g. "C1M0-G1A5"). Lets players join by typing a short code when broadcast
    /// discovery is blocked (guest Wi-Fi, iOS without the multicast entitlement...).
    /// </summary>
    public static class InviteCode
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Encode(IPAddress address, int port)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("An IPv4 address is required.", nameof(address));
            int offset = port - ProtocolInfo.DefaultGamePort;
            if (offset < 0 || offset > 255) throw new ArgumentOutOfRangeException(nameof(port), "Port must be within 256 of the default game port.");
            var ip = address.GetAddressBytes();
            ulong value = 0;
            for (int i = 0; i < 4; i++) value = (value << 8) | ip[i];
            value = (value << 8) | (uint)offset;
            var sb = new StringBuilder(9);
            for (int i = 7; i >= 0; i--)
            {
                sb.Append(Alphabet[(int)((value >> (i * 5)) & 31)]);
                if (i == 4) sb.Append('-');
            }
            return sb.ToString();
        }

        public static bool TryDecode(string code, out IPAddress address, out int port)
        {
            address = null;
            port = 0;
            if (string.IsNullOrWhiteSpace(code)) return false;
            ulong value = 0;
            int digits = 0;
            foreach (char raw in code.Trim().ToUpperInvariant())
            {
                if (raw == '-' || raw == ' ') continue;
                char c = raw == 'O' ? '0' : raw == 'I' || raw == 'L' ? '1' : raw;
                int v = Alphabet.IndexOf(c);
                if (v < 0) return false;
                value = (value << 5) | (uint)v;
                digits++;
            }
            if (digits != 8) return false;
            int offset = (int)(value & 0xFF);
            value >>= 8;
            var ip = new byte[4];
            for (int i = 3; i >= 0; i--)
            {
                ip[i] = (byte)(value & 0xFF);
                value >>= 8;
            }
            address = new IPAddress(ip);
            port = ProtocolInfo.DefaultGamePort + offset;
            return true;
        }
    }
}
