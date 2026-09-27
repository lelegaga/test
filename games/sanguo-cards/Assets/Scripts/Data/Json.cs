using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sanguo.Data
{
    public enum JsonKind
    {
        Null,
        Boolean,
        Number,
        String,
        Array,
        Object
    }

    /// <summary>
    /// Minimal JSON DOM used for content files and local saves. Hand written instead of relying on
    /// reflection based serializers so it works identically under Mono, IL2CPP (no stripping issues)
    /// and plain .NET.
    /// </summary>
    public sealed class JsonValue
    {
        public static readonly JsonValue Null = new JsonValue(JsonKind.Null);

        private readonly bool _bool;
        private readonly double _number;
        private readonly string _string;
        private readonly List<JsonValue> _items;
        private readonly List<KeyValuePair<string, JsonValue>> _members;
        private readonly Dictionary<string, JsonValue> _lookup;

        public JsonKind Kind { get; }

        private JsonValue(JsonKind kind)
        {
            Kind = kind;
            if (kind == JsonKind.Array) _items = new List<JsonValue>();
            if (kind == JsonKind.Object)
            {
                _members = new List<KeyValuePair<string, JsonValue>>();
                _lookup = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            }
        }

        private JsonValue(bool value) : this(JsonKind.Boolean) { _bool = value; }
        private JsonValue(double value) : this(JsonKind.Number) { _number = value; }
        private JsonValue(string value) : this(JsonKind.String) { _string = value ?? string.Empty; }

        public static JsonValue From(bool value) => new JsonValue(value);
        public static JsonValue From(double value) => new JsonValue(value);
        public static JsonValue From(int value) => new JsonValue((double)value);
        public static JsonValue From(long value) => new JsonValue((double)value);
        public static JsonValue From(string value) => value == null ? Null : new JsonValue(value);
        public static JsonValue NewArray() => new JsonValue(JsonKind.Array);
        public static JsonValue NewObject() => new JsonValue(JsonKind.Object);

        public bool IsNull => Kind == JsonKind.Null;
        public bool IsObject => Kind == JsonKind.Object;
        public bool IsArray => Kind == JsonKind.Array;

        public int Count => Kind == JsonKind.Array ? _items.Count : Kind == JsonKind.Object ? _members.Count : 0;

        public IReadOnlyList<JsonValue> Items => _items ?? (IReadOnlyList<JsonValue>)Array.Empty<JsonValue>();

        public IReadOnlyList<KeyValuePair<string, JsonValue>> Members =>
            _members ?? (IReadOnlyList<KeyValuePair<string, JsonValue>>)Array.Empty<KeyValuePair<string, JsonValue>>();

        public JsonValue this[string key]
        {
            get
            {
                if (Kind != JsonKind.Object || key == null) return Null;
                return _lookup.TryGetValue(key, out var v) ? v : Null;
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (Kind != JsonKind.Array || index < 0 || index >= _items.Count) return Null;
                return _items[index];
            }
        }

        public bool Has(string key) => Kind == JsonKind.Object && _lookup.ContainsKey(key);

        public string AsString(string fallback = null) => Kind == JsonKind.String ? _string : fallback;
        public double AsDouble(double fallback = 0) => Kind == JsonKind.Number ? _number : fallback;
        public int AsInt(int fallback = 0) => Kind == JsonKind.Number ? (int)Math.Round(_number) : fallback;
        public long AsLong(long fallback = 0) => Kind == JsonKind.Number ? (long)Math.Round(_number) : fallback;
        public bool AsBool(bool fallback = false) => Kind == JsonKind.Boolean ? _bool : fallback;

        public string GetString(string key, string fallback = null) => this[key].AsString(fallback);
        public int GetInt(string key, int fallback = 0) => this[key].AsInt(fallback);
        public long GetLong(string key, long fallback = 0) => this[key].AsLong(fallback);
        public double GetDouble(string key, double fallback = 0) => this[key].AsDouble(fallback);
        public bool GetBool(string key, bool fallback = false) => this[key].AsBool(fallback);

        /// <summary>Reads a string array; a single string is accepted as a one element array.</summary>
        public List<string> GetStringList(string key)
        {
            var result = new List<string>();
            var node = this[key];
            if (node.Kind == JsonKind.String) result.Add(node._string);
            foreach (var item in node.Items)
            {
                var s = item.AsString();
                if (s != null) result.Add(s);
            }
            return result;
        }

        public JsonValue Add(JsonValue value)
        {
            if (Kind != JsonKind.Array) throw new InvalidOperationException("Add is only valid on arrays.");
            _items.Add(value ?? Null);
            return this;
        }

        public JsonValue Set(string key, JsonValue value)
        {
            if (Kind != JsonKind.Object) throw new InvalidOperationException("Set is only valid on objects.");
            value = value ?? Null;
            if (_lookup.ContainsKey(key))
            {
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].Key == key)
                    {
                        _members[i] = new KeyValuePair<string, JsonValue>(key, value);
                        break;
                    }
                }
            }
            else
            {
                _members.Add(new KeyValuePair<string, JsonValue>(key, value));
            }
            _lookup[key] = value;
            return this;
        }

        public JsonValue Set(string key, string value) => Set(key, From(value));
        public JsonValue Set(string key, int value) => Set(key, From(value));
        public JsonValue Set(string key, long value) => Set(key, From(value));
        public JsonValue Set(string key, double value) => Set(key, From(value));
        public JsonValue Set(string key, bool value) => Set(key, From(value));

        public static JsonValue Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return new Parser(text).ParseDocument();
        }

        public string ToJson(bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, pretty, 0);
            return sb.ToString();
        }

        public override string ToString() => ToJson();

        private void Write(StringBuilder sb, bool pretty, int indent)
        {
            switch (Kind)
            {
                case JsonKind.Null:
                    sb.Append("null");
                    break;
                case JsonKind.Boolean:
                    sb.Append(_bool ? "true" : "false");
                    break;
                case JsonKind.Number:
                    if (Math.Abs(_number % 1) < double.Epsilon && Math.Abs(_number) < 1e15)
                        sb.Append(((long)_number).ToString(CultureInfo.InvariantCulture));
                    else
                        sb.Append(_number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonKind.String:
                    WriteString(sb, _string);
                    break;
                case JsonKind.Array:
                    sb.Append('[');
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        NewLine(sb, pretty, indent + 1);
                        _items[i].Write(sb, pretty, indent + 1);
                    }
                    if (_items.Count > 0) NewLine(sb, pretty, indent);
                    sb.Append(']');
                    break;
                case JsonKind.Object:
                    sb.Append('{');
                    for (int i = 0; i < _members.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        NewLine(sb, pretty, indent + 1);
                        WriteString(sb, _members[i].Key);
                        sb.Append(pretty ? ": " : ":");
                        _members[i].Value.Write(sb, pretty, indent + 1);
                    }
                    if (_members.Count > 0) NewLine(sb, pretty, indent);
                    sb.Append('}');
                    break;
            }
        }

        private static void NewLine(StringBuilder sb, bool pretty, int indent)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _pos;

            public Parser(string text)
            {
                _text = text;
            }

            public JsonValue ParseDocument()
            {
                SkipWhitespace();
                var value = ParseValue();
                SkipWhitespace();
                if (_pos != _text.Length) throw Error("Unexpected trailing characters");
                return value;
            }

            private JsonValue ParseValue()
            {
                SkipWhitespace();
                if (_pos >= _text.Length) throw Error("Unexpected end of input");
                char c = _text[_pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return new JsonValue(ParseString());
                    case 't': Expect("true"); return new JsonValue(true);
                    case 'f': Expect("false"); return new JsonValue(false);
                    case 'n': Expect("null"); return Null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private JsonValue ParseObject()
            {
                var obj = NewObject();
                _pos++; // {
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _pos++;
                    return obj;
                }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw Error("Expected property name");
                    string key = ParseString();
                    SkipWhitespace();
                    if (Peek() != ':') throw Error("Expected ':'");
                    _pos++;
                    obj.Set(key, ParseValue());
                    SkipWhitespace();
                    char c = Peek();
                    _pos++;
                    if (c == ',') continue;
                    if (c == '}') return obj;
                    throw Error("Expected ',' or '}'");
                }
            }

            private JsonValue ParseArray()
            {
                var arr = NewArray();
                _pos++; // [
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _pos++;
                    return arr;
                }
                while (true)
                {
                    arr.Add(ParseValue());
                    SkipWhitespace();
                    char c = Peek();
                    _pos++;
                    if (c == ',') continue;
                    if (c == ']') return arr;
                    throw Error("Expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                _pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (_pos >= _text.Length) throw Error("Unterminated string");
                    char c = _text[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (_pos >= _text.Length) throw Error("Unterminated escape");
                    char e = _text[_pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _pos += 4;
                            break;
                        default: throw Error("Bad escape '\\" + e + "'");
                    }
                }
            }

            private JsonValue ParseNumber()
            {
                int start = _pos;
                if (Peek() == '-') _pos++;
                while (_pos < _text.Length)
                {
                    char c = _text[_pos];
                    if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') _pos++;
                    else break;
                }
                string token = _text.Substring(start, _pos - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Error("Bad number '" + token + "'");
                return new JsonValue(value);
            }

            private void Expect(string literal)
            {
                if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0) throw Error("Expected '" + literal + "'");
                _pos += literal.Length;
            }

            private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

            private void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
            }

            private FormatException Error(string message)
            {
                return new FormatException(message + " at position " + _pos + ".");
            }
        }
    }
}
