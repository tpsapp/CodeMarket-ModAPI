using System;
using System.Collections.Generic;
using CodeMarket.ModAPI.Core.Json;

namespace CodeMarket.ModAPI.Core.Tests;

internal static class JsonTests
{
    public static void Register()
    {
        TestHarness.Register("json", "JSON-01_HappyPathRoundTrip", () =>
        {
            const string input = """
            {
              "name": "CodeMarket",
              "version": 1,
              "ratio": 1.25,
              "enabled": true,
              "empty": null,
              "items": [1, "two", false, { "nested": "ok" }]
            }
            """;

            JsonValue parsed = JsonParser.Parse(input);
            TestHarness.CheckEq(JsonKind.Object, parsed.Kind, "Root must be object");
            TestHarness.CheckTrue(parsed.TryGetProperty("name", out JsonValue? name), "name missing");
            TestHarness.CheckEq("CodeMarket", name!.AsString(), "name value");
            TestHarness.CheckTrue(parsed.TryGetProperty("version", out JsonValue? ver), "version missing");
            TestHarness.CheckEq(1, ver!.AsInt32(), "version int32");
            TestHarness.CheckTrue(parsed.TryGetProperty("ratio", out JsonValue? ratio), "ratio missing");
            TestHarness.CheckEq(1.25, ratio!.AsNumber(), "ratio number");
            TestHarness.CheckTrue(parsed.TryGetProperty("enabled", out JsonValue? enabled), "enabled missing");
            TestHarness.CheckEq(true, enabled!.AsBool(), "enabled bool");
            TestHarness.CheckTrue(parsed.TryGetProperty("empty", out JsonValue? empty), "empty missing");
            TestHarness.CheckEq(JsonKind.Null, empty!.Kind, "empty null");
            TestHarness.CheckTrue(parsed.TryGetProperty("items", out JsonValue? items), "items missing");
            TestHarness.CheckEq(4, items!.AsArray().Count, "items count");

            string serialized = JsonWriter.Serialize(parsed, indented: true);
            JsonValue reparsed = JsonParser.Parse(serialized);
            TestHarness.CheckEq(serialized, JsonWriter.Serialize(reparsed, indented: true), "Round-trip must be deterministic");
        });

        TestHarness.Register("json", "JSON-02_UnicodeAndEscapes", () =>
        {
            const string input = "{\"text\":\"Line1\\nLine2\\t\\\"quoted\\\"\\\\\\u0041\\uD83D\\uDE00\"}";
            JsonValue parsed = JsonParser.Parse(input);
            TestHarness.CheckTrue(parsed.TryGetProperty("text", out JsonValue? val), "text missing");
            string s = val!.AsString();
            TestHarness.CheckEq("Line1\nLine2\t\"quoted\"\\A😀", s, "Decoded string mismatch");

            string roundTrip = JsonWriter.Serialize(parsed, indented: false);
            JsonValue reparsed = JsonParser.Parse(roundTrip);
            TestHarness.CheckTrue(reparsed.TryGetProperty("text", out JsonValue? val2), "text2 missing");
            TestHarness.CheckEq(s, val2!.AsString(), "Escaped string round-trip mismatch");
        });

        TestHarness.Register("json", "JSON-03_EmptyAndWhitespace", () =>
        {
            TestHarness.CheckThrows<JsonParseException>(() => JsonParser.Parse(""), "Empty string must fail");
            TestHarness.CheckThrows<JsonParseException>(() => JsonParser.Parse("   \r\n\t  "), "Whitespace must fail");
        });

        TestHarness.Register("json", "JSON-04_EmptyStructs", () =>
        {
            JsonValue emptyObj = JsonParser.Parse("{}");
            TestHarness.CheckEq(0, emptyObj.AsObject().Count, "Empty object count");
            TestHarness.CheckEq("{}", JsonWriter.Serialize(emptyObj), "Empty object serialization");

            JsonValue emptyArr = JsonParser.Parse("[]");
            TestHarness.CheckEq(0, emptyArr.AsArray().Count, "Empty array count");
            TestHarness.CheckEq("[]", JsonWriter.Serialize(emptyArr), "Empty array serialization");
        });

        TestHarness.Register("json", "JSON-05_MaxDepthGuard", () =>
        {
            string tooDeep = new string('[', JsonParser.MaxDepth + 1) + new string(']', JsonParser.MaxDepth + 1);
            TestHarness.CheckThrows<JsonParseException>(() => JsonParser.Parse(tooDeep), "Depth > 64 must throw JsonParseException");
        });

        TestHarness.Register("json", "JSON-06_MalformedSyntax", () =>
        {
            TestHarness.CheckThrows<JsonParseException>(() => JsonParser.Parse("{\"a\":1,}"), "Trailing comma in object");
            TestHarness.CheckThrows<JsonParseException>(() => JsonParser.Parse("[1,2,]"), "Trailing comma in array");
            TestHarness.CheckThrows<JsonParseException>(() => JsonParser.Parse("\"unterminated"), "Unterminated string");
            TestHarness.CheckThrows<JsonParseException>(() => JsonParser.Parse("{} 123"), "Trailing token");
            TestHarness.CheckThrows<JsonParseException>(() => JsonParser.Parse("{\"a\": 01}"), "Leading zero number");
        });

        TestHarness.Register("json", "JSON-07_NumericBounds", () =>
        {
            JsonValue nums = JsonParser.Parse($"[0, -1, {int.MaxValue}, {int.MinValue}]");
            IReadOnlyList<JsonValue> arr = nums.AsArray();
            TestHarness.CheckEq(0, arr[0].AsInt32(), "zero");
            TestHarness.CheckEq(-1, arr[1].AsInt32(), "negative one");
            TestHarness.CheckEq(int.MaxValue, arr[2].AsInt32(), "int.MaxValue");
            TestHarness.CheckEq(int.MinValue, arr[3].AsInt32(), "int.MinValue");

            TestHarness.CheckThrows<ArgumentException>(() => JsonValue.FromNumber(double.NaN), "NaN rejected");
            TestHarness.CheckThrows<ArgumentException>(() => JsonValue.FromNumber(double.PositiveInfinity), "Infinity rejected");
        });
    }
}
