using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CodeMarket.ModAPI.Core.Json;

public static class JsonWriter
{
    public static string Serialize(JsonValue value, bool indented = true)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        var sb = new StringBuilder();
        WriteValue(sb, value, indented, 0);
        return sb.ToString();
    }

    private static void WriteValue(StringBuilder sb, JsonValue value, bool indented, int depth)
    {
        switch (value.Kind)
        {
            case JsonKind.Null:
                sb.Append("null");
                break;

            case JsonKind.Bool:
                sb.Append(value.AsBool() ? "true" : "false");
                break;

            case JsonKind.Number:
                double num = value.AsNumber();
                if (double.IsNaN(num) || double.IsInfinity(num))
                {
                    throw new ArgumentException("Cannot serialize non-finite JSON number.", nameof(value));
                }

                sb.Append(num.ToString("R", CultureInfo.InvariantCulture));
                break;

            case JsonKind.String:
                WriteString(sb, value.AsString());
                break;

            case JsonKind.Array:
                WriteArray(sb, value.AsArray(), indented, depth);
                break;

            case JsonKind.Object:
                WriteObject(sb, value.AsObject(), indented, depth);
                break;

            default:
                throw new InvalidOperationException($"Unsupported JsonKind: {value.Kind}");
        }
    }

    private static void WriteArray(StringBuilder sb, IReadOnlyList<JsonValue> items, bool indented, int depth)
    {
        if (items.Count == 0)
        {
            sb.Append("[]");
            return;
        }

        sb.Append('[');
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            if (indented)
            {
                sb.Append('\n');
                AppendIndent(sb, depth + 1);
            }

            WriteValue(sb, items[i], indented, depth + 1);
        }

        if (indented)
        {
            sb.Append('\n');
            AppendIndent(sb, depth);
        }

        sb.Append(']');
    }

    private static void WriteObject(StringBuilder sb, IReadOnlyDictionary<string, JsonValue> obj, bool indented, int depth)
    {
        if (obj.Count == 0)
        {
            sb.Append("{}");
            return;
        }

        // Ordinal key sorting guarantees deterministic output across Mono and .NET runtimes.
        List<KeyValuePair<string, JsonValue>> sorted = obj.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();

        sb.Append('{');
        for (int i = 0; i < sorted.Count; i++)
        {
            if (i > 0) sb.Append(',');
            if (indented)
            {
                sb.Append('\n');
                AppendIndent(sb, depth + 1);
            }

            WriteString(sb, sorted[i].Key);
            sb.Append(indented ? ": " : ":");
            WriteValue(sb, sorted[i].Value, indented, depth + 1);
        }

        if (indented)
        {
            sb.Append('\n');
            AppendIndent(sb, depth);
        }

        sb.Append('}');
    }

    private static void WriteString(StringBuilder sb, string text)
    {
        sb.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }

    private static void AppendIndent(StringBuilder sb, int depth)
    {
        sb.Append(' ', depth * 2);
    }
}
