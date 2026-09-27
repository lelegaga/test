using System;
using System.Collections.Generic;
using System.Text;

namespace Sanguo.Network
{
    /// <summary>Raised when received bytes do not form a valid message (malformed or malicious input).</summary>
    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Compact little-endian binary writer. Integers use zig-zag varints; strings are UTF-8 with a
    /// length prefix (0 = null). Hand written so it behaves identically under IL2CPP (no reflection).
    /// </summary>
    public sealed class NetWriter
    {
        private byte[] _buffer;
        private int _length;

        public NetWriter(int capacity = 256)
        {
            _buffer = new byte[Math.Max(16, capacity)];
        }

        public int Length => _length;

        public void Reset() => _length = 0;

        public byte[] ToArray()
        {
            var result = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, result, 0, _length);
            return result;
        }

        public ArraySegment<byte> AsSegment() => new ArraySegment<byte>(_buffer, 0, _length);

        private void Ensure(int extra)
        {
            int needed = _length + extra;
            if (needed <= _buffer.Length) return;
            int size = _buffer.Length * 2;
            while (size < needed) size *= 2;
            Array.Resize(ref _buffer, size);
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteVarUInt(uint value)
        {
            Ensure(5);
            while (value >= 0x80)
            {
                _buffer[_length++] = (byte)(value | 0x80);
                value >>= 7;
            }
            _buffer[_length++] = (byte)value;
        }

        public void WriteVarInt(int value) => WriteVarUInt((uint)((value << 1) ^ (value >> 31)));

        public void WriteInt64(long value)
        {
            Ensure(8);
            ulong v = (ulong)value;
            for (int i = 0; i < 8; i++)
            {
                _buffer[_length++] = (byte)v;
                v >>= 8;
            }
        }

        public void WriteUInt16(ushort value)
        {
            Ensure(2);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
        }

        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteVarUInt(0);
                return;
            }
            int byteCount = Encoding.UTF8.GetByteCount(value);
            WriteVarUInt((uint)byteCount + 1);
            Ensure(byteCount);
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _length);
            _length += byteCount;
        }

        public void WriteBytes(byte[] data)
        {
            if (data == null)
            {
                WriteVarUInt(0);
                return;
            }
            WriteVarUInt((uint)data.Length + 1);
            WriteRaw(data, 0, data.Length);
        }

        public void WriteRaw(byte[] data, int offset, int count)
        {
            Ensure(count);
            Buffer.BlockCopy(data, offset, _buffer, _length, count);
            _length += count;
        }

        /// <summary>Int array; null and empty are distinguished.</summary>
        public void WriteIntArray(int[] values)
        {
            if (values == null)
            {
                WriteVarUInt(0);
                return;
            }
            WriteVarUInt((uint)values.Length + 1);
            foreach (int v in values) WriteVarInt(v);
        }

        public void WriteIntList(List<int> values)
        {
            if (values == null)
            {
                WriteVarUInt(0);
                return;
            }
            WriteVarUInt((uint)values.Count + 1);
            foreach (int v in values) WriteVarInt(v);
        }

        public void WriteStringList(List<string> values)
        {
            if (values == null)
            {
                WriteVarUInt(0);
                return;
            }
            WriteVarUInt((uint)values.Count + 1);
            foreach (var v in values) WriteString(v);
        }

        /// <summary>Writes a count prefix for a list (0 = null) and returns whether items follow.</summary>
        public bool WriteListHeader<T>(ICollection<T> list)
        {
            if (list == null)
            {
                WriteVarUInt(0);
                return false;
            }
            WriteVarUInt((uint)list.Count + 1);
            return true;
        }
    }
}
