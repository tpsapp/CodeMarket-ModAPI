using System;

namespace CodeMarket.ModAPI.Core.Json;

public sealed class JsonParseException : Exception
{
    public int Position { get; }
    public int Line { get; }
    public int Column { get; }

    public JsonParseException(string message, int position, int line, int column)
        : base($"{message} (line {line}, col {column}, pos {position})")
    {
        Position = position;
        Line = line;
        Column = column;
    }
}
