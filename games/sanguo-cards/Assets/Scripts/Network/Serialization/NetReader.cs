using System;
using System.Collections.Generic;
using System.Text;

namespace Sanguo.Network
{
    /// <summary>
    /// Bounds-checked reader matching <see cref="NetWriter"/>. Every length is validated before
    /// allocation, so malformed or hostile packets raise <see cref="ProtocolException"/> instead of
    /// crashing or exhausting memory.
    /// </summary>
    public sealed class NetReader
    {
        /// <summary>Upper bound for any collection or string read from the wire.</summary>
        public const int MaxCollectionLength = 1 << 16;

        private readonly byte[] _data;
        private readonly int _end;
        private int _pos;

        public NetReader(byte[] data) : this(data, 0, data?.Length ?? 0)
        {
        }

        public NetReader(byte[] data, int offset, int count)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || count < 0 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));
            _data = data;
            _pos = offset;
            _end = offset + count;
        }

        public int Remaining => _end - _pos;
        public bool AtEnd => _pos >= _end;

        private void Need(int n)
        {
            if (n < 0 || _pos + n > _end) throw new ProtocolException("Unexpected end of message.");
        }

        public byte ReadByte()
        {
            Need(1);
            return _data[_pos++];
        }

        public bool ReadBool()
        {
            byte b = ReadByte();
            if (b > 1) throw new ProtocolException("Invalid boolean.");
            return b == 1;
        }

        public uint ReadVarUInt()
        {
            uint result = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte b = ReadByte();
                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
            }
            throw new ProtocolException("Varint too long.");
        }

        public int ReadVarInt()
        {
            uint v = ReadVarUInt();
            return (int)(v >> 1) ^ -(int)(v & 1);
        }

        public long ReadInt64()
        {
            Need(8);
            ulong v = 0;
            for (int i = 0; i < 8; i++) v |= (ulong)_data[_pos++] << (8 * i);
            return (long)v;
        }

        public ushort ReadUInt16()
        {
            Need(2);
            ushort v = (ushort)(_data[_pos] | (_data[_pos + 1] << 8));
            _pos += 2;
            return v;
        }

        /// <summary>Returns -1 for null, otherwise the count.</summary>
        private int ReadCount()
        {
            uint raw = ReadVarUInt();
            if (raw == 0) return -1;
            if (raw - 1 > MaxCollectionLength) throw new ProtocolException("Collection too large.");
            return (int)(raw - 1);
        }

        public string ReadString()
        {
            int n = ReadCount();
            if (n < 0) return null;
            Need(n);
            string s = Encoding.UTF8.GetString(_data, _pos, n);
            _pos += n;
            return s;
        }

        public byte[] ReadBytes()
        {
            uint raw = ReadVarUInt();
            if (raw == 0) return null;
            int n = (int)Math.Min(raw - 1, int.MaxValue);
            Need(n);
            var result = new byte[n];
            Buffer.BlockCopy(_data, _pos, result, 0, n);
            _pos += n;
            return result;
        }

        public int[] ReadIntArray()
        {
            int n = ReadCount();
            if (n < 0) return null;
            var result = new int[n];
            for (int i = 0; i < n; i++) result[i] = ReadVarInt();
            return result;
        }

        public List<int> ReadIntList()
        {
            int n = ReadCount();
            if (n < 0) return null;
            var result = new List<int>(n);
            for (int i = 0; i < n; i++) result.Add(ReadVarInt());
            return result;
        }

        public List<string> ReadStringList()
        {
            int n = ReadCount();
            if (n < 0) return null;
            var result = new List<string>(n);
            for (int i = 0; i < n; i++) result.Add(ReadString());
            return result;
        }

        /// <summary>Reads a list header written by <see cref="NetWriter.WriteListHeader{T}"/>; -1 = null.</summary>
        public int ReadListHeader() => ReadCount();

        /// <summary>Reads an enum stored as a byte and checks it is defined.</summary>
        public T ReadEnum<T>() where T : struct, Enum
        {
            byte b = ReadByte();
            var value = (T)Enum.ToObject(typeof(T), b);
            if (!Enum.IsDefined(typeof(T), value)) throw new ProtocolException("Invalid " + typeof(T).Name + " value " + b + ".");
            return value;
        }
    }
}
