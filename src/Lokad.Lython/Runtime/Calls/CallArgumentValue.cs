namespace Lokad.Lython.Runtime;

internal enum CallArgumentPlacement
{
    Positional,
    Keyword
}

internal readonly record struct CallArgumentValue
{
    private readonly string _keywordName;

    public CallArgumentValue(string? name, object value)
    {
        Placement = name is null ? CallArgumentPlacement.Positional : CallArgumentPlacement.Keyword;
        _keywordName = name ?? string.Empty;
        Value = value;
    }

    public CallArgumentPlacement Placement { get; }

    public bool IsPositional => Placement == CallArgumentPlacement.Positional;

    public bool IsKeyword => Placement == CallArgumentPlacement.Keyword;

    public string KeywordName => IsKeyword
        ? _keywordName
        : throw new InvalidOperationException("A positional call argument has no keyword name.");

    // This projection keeps Python-facing parsers concise while Placement carries the actual discriminant.
    public string? Name => IsKeyword ? _keywordName : null;

    public object Value { get; }
}
