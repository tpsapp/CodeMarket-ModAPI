using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CodeMarket.ModAPI.Core.Json;

public static class JsonParser
{
    public const int MaxDepth = 64;

    public static JsonValue Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonParseException("JSON input cannot be null or empty.", 0, 1, 1);
        }

        var reader = new Reader(json);
        reader.SkipWhitespace();
        JsonValue value = reader.ReadValue(0);
        reader.SkipWhitespace();

        if (!reader.IsAtEnd)
        {
            throw reader.Error($"Unexpected trailing token '{reader.Current}'.");
        }

        return value;
    }

    private sealed class Reader
    {
        private readonly string _text;
        private int _pos;
        private int _line = 1;
        private int _col = 1;

        public Reader(string text)
        {
            _text = text;
        }

        public bool IsAtEnd => _pos >= _text.Length;
        public char Current => _text[_pos];

        public JsonParseException Error(string message) => new JsonParseException(message, _pos, _line, _col);

        private char Advance()
        {
            char c = _text[_pos++];
            if (c == '\n')
            {
                _line++;
                _col = 1;
            }
            else
            {
                _col++;
            }

            return c;
        }

        public void SkipWhitespace()
        {
            while (!IsAtEnd)
            {
                char c = Current;
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    Advance();
                }
                else
                {
                    break;
                }
            }
        }

        public JsonValue ReadValue(int depth)
        {
            if (IsAtEnd) throw Error("Unexpected end of JSON input.");

            char c = Current;
            return c switch
            {
                '{' => ReadObject(depth),
                '[' => ReadArray(depth),
                '"' => JsonValue.FromString(ReadString()),
                't' => ReadLiteral("true", JsonValue.FromBool(true)),
                'f' => ReadLiteral("false", JsonValue.FromBool(false)),
                'n' => ReadLiteral("null", JsonValue.Null),
                '-' or (>= '0' and <= '9') => ReadNumber(),
                _ => throw Error($"Unexpected character '{c}'.")
            };
        }

        private JsonValue ReadObject(int depth)
        {
            if (depth >= MaxDepth) throw Error($"Maximum nesting depth ({MaxDepth}) exceeded.");

            Advance(); // '{'
            SkipWhitespace();

            var props = new List<KeyValuePair<string, JsonValue>>();
            if (!IsAtEnd && Current == '}')
            {
                Advance();
                return JsonValue.FromObject(props);
            }

            while (true)
            {
                SkipWhitespace();
                if (IsAtEnd || Current != '"')
                {
                    throw Error("Expected string property name in JSON object.");
                }

                string key = ReadString();
                SkipWhitespace();

                if (IsAtEnd || Current != ':')
                {
                    throw Error($"Expected ':' after property name '{key}'.");
                }

                Advance(); // ':'
                SkipWhitespace();

                JsonValue val = ReadValue(depth + 1);
                props.Add(new KeyValuePair<string, JsonValue>(key, val));

                SkipWhitespace();
                if (IsAtEnd) throw Error("Unterminated JSON object.");

                if (Current == ',')
                {
                    Advance();
                    continue;
                }

                if (Current == '}')
                {
                    Advance();
                    return JsonValue.FromObject(props);
                }

                throw Error($"Expected ',' or '}}' in JSON object, got '{Current}'.");
            }
        }

        private JsonValue ReadArray(int depth)
        {
            if (depth >= MaxDepth) throw Error($"Maximum nesting depth ({MaxDepth}) exceeded.");

            Advance(); // '['
            SkipWhitespace();

            var items = new List<JsonValue>();
            if (!IsAtEnd && Current == ']')
            {
                Advance();
                return JsonValue.FromArray(items);
            }

            while (true)
            {
                SkipWhitespace();
                items.Add(ReadValue(depth + 1));
                SkipWhitespace();

                if (IsAtEnd) throw Error("Unterminated JSON array.");

                if (Current == ',')
                {
                    Advance();
                    continue;
                }

                if (Current == ']')
                {
                    Advance();
                    return JsonValue.FromArray(items);
                }

                throw Error($"Expected ',' or ']' in JSON array, got '{Current}'.");
            }
        }

        private string ReadString()
        {
            Advance(); // opening '"'
            var sb = new StringBuilder();

            while (!IsAtEnd)
            {
                char c = Advance();
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c < 0x20)
                {
                    throw Error($"Unescaped control character U+{(int)c:X4} in JSON string.");
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (IsAtEnd) throw Error("Unterminated escape sequence in JSON string.");

                char esc = Advance();
                switch (esc)
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
                        sb.Append(ReadUnicodeEscape());
                        break;
                    default:
                        throw Error($"Invalid escape character '\\{esc}' in JSON string.");
                }
            }

            throw Error("Unterminated JSON string.");
        }

        private char ReadUnicodeEscape()
        {
            if (_pos + 4 > _text.Length)
            {
                throw Error("Incomplete \\uXXXX unicode escape.");
            }

            int code = 0;
            for (int i = 0; i < 4; i++)
            {
                char h = Advance();
                int digit = h switch
                {
                    >= '0' and <= '9' => h - '0',
                    >= 'a' and <= 'f' => h - 'a' + 10,
                    >= 'A' and <= 'F' => h - 'A' + 10,
                    _ => -1
                };

                if (digit < 0)
                {
                    throw Error($"Invalid hex digit '{h}' in \\uXXXX escape.");
                }

                code = (code << 4) | digit;
            }

            return (char)code;
        }

        private JsonValue ReadLiteral(string expected, JsonValue result)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if (IsAtEnd || Current != expected[i])
                {
                    throw Error($"Expected literal '{expected}'.");
                }

                Advance();
            }

            return result;
        }

        private JsonValue ReadNumber()
        {
            int start = _pos;

            if (Current == '-') Advance();
            if (IsAtEnd) throw Error("Incomplete JSON number.");

            if (Current == '0')
            {
                Advance();
            }
            else if (Current >= '1' && Current <= '9')
            {
                while (!IsAtEnd && Current >= '0' && Current <= '9') Advance();
            }
            else
            {
                throw Error($"Invalid JSON number starting at '{Current}'.");
            }

            if (!IsAtEnd && Current == '.')
            {
                Advance();
                if (IsAtEnd || Current < '0' || Current > '9')
                {
                    throw Error("Expected digit after decimal point in JSON number.");
                }

                while (!IsAtEnd && Current >= '0' && Current <= '9') Advance();
            }

            if (!IsAtEnd && (Current == 'e' || Current == 'E'))
            {
                Advance();
                if (!IsAtEnd && (Current == '+' || Current == '-')) Advance();
                if (IsAtEnd || Current < '0' || Current > '9')
                {
                    throw Error("Expected digit in JSON number exponent.");
                }

                while (!IsAtEnd && Current >= '0' && Current <= '9') Advance();
            }

            string token = _text.Substring(start, _pos - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                double.IsNaN(value) || double.IsInfinity(value))
            {
                throw Error($"JSON number '{token}' is out of range.");
            }

            return JsonValue.FromNumber(value);
        }
    }
}
