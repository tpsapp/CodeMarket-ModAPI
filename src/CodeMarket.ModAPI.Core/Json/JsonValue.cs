using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CodeMarket.ModAPI.Core.Json;

public enum JsonKind
{
    Null,
    Bool,
    Number,
    String,
    Array,
    Object
}

public sealed class JsonValue
{
    private readonly bool _boolValue;
    private readonly double _numberValue;
    private readonly string? _stringValue;
    private readonly IReadOnlyList<JsonValue>? _arrayValue;
    private readonly IReadOnlyDictionary<string, JsonValue>? _objectValue;

    public static JsonValue Null { get; } = new JsonValue(JsonKind.Null);

    public JsonKind Kind { get; }

    private JsonValue(JsonKind kind)
    {
        Kind = kind;
    }

    private JsonValue(bool value)
    {
        Kind = JsonKind.Bool;
        _boolValue = value;
    }

    private JsonValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentException("JSON numbers must be finite.", nameof(value));
        }

        Kind = JsonKind.Number;
        _numberValue = value;
    }

    private JsonValue(string value)
    {
        Kind = JsonKind.String;
        _stringValue = value ?? throw new ArgumentNullException(nameof(value));
    }

    private JsonValue(IReadOnlyList<JsonValue> items)
    {
        Kind = JsonKind.Array;
        _arrayValue = items;
    }

    private JsonValue(IReadOnlyDictionary<string, JsonValue> properties)
    {
        Kind = JsonKind.Object;
        _objectValue = properties;
    }

    public static JsonValue FromBool(bool value) => new JsonValue(value);

    public static JsonValue FromNumber(double value) => new JsonValue(value);

    public static JsonValue FromString(string value) => new JsonValue(value);

    public static JsonValue FromArray(IEnumerable<JsonValue> items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        var list = new List<JsonValue>();
        foreach (JsonValue item in items)
        {
            list.Add(item ?? Null);
        }

        return new JsonValue(new ReadOnlyCollection<JsonValue>(list));
    }

    public static JsonValue FromObject(IEnumerable<KeyValuePair<string, JsonValue>> properties)
    {
        if (properties is null) throw new ArgumentNullException(nameof(properties));
        var dict = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonValue> kv in properties)
        {
            if (kv.Key is null) throw new ArgumentException("JSON object keys cannot be null.", nameof(properties));
            dict[kv.Key] = kv.Value ?? Null;
        }

        return new JsonValue(new ReadOnlyDictionary<string, JsonValue>(dict));
    }

    public bool AsBool()
    {
        if (Kind != JsonKind.Bool) throw new InvalidOperationException($"Expected Bool, got {Kind}.");
        return _boolValue;
    }

    public double AsNumber()
    {
        if (Kind != JsonKind.Number) throw new InvalidOperationException($"Expected Number, got {Kind}.");
        return _numberValue;
    }

    public int AsInt32()
    {
        double num = AsNumber();
        if (num < int.MinValue || num > int.MaxValue || Math.Truncate(num) != num)
        {
            throw new InvalidOperationException($"JSON number {num} is not a valid 32-bit integer.");
        }

        return (int)num;
    }

    public string AsString()
    {
        if (Kind != JsonKind.String || _stringValue is null)
        {
            throw new InvalidOperationException($"Expected String, got {Kind}.");
        }

        return _stringValue;
    }

    public IReadOnlyList<JsonValue> AsArray()
    {
        if (Kind != JsonKind.Array || _arrayValue is null)
        {
            throw new InvalidOperationException($"Expected Array, got {Kind}.");
        }

        return _arrayValue;
    }

    public IReadOnlyDictionary<string, JsonValue> AsObject()
    {
        if (Kind != JsonKind.Object || _objectValue is null)
        {
            throw new InvalidOperationException($"Expected Object, got {Kind}.");
        }

        return _objectValue;
    }

    public bool TryGetProperty(string key, out JsonValue? value)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (Kind != JsonKind.Object || _objectValue is null)
        {
            value = null;
            return false;
        }

        return _objectValue.TryGetValue(key, out value);
    }
}
