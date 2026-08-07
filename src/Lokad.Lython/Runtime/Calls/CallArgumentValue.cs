namespace Lokad.Lython.Runtime;

internal enum CallArgumentPlacement
{
    Positional,
    Keyword
}

internal readonly record struct CallArgumentValue
{
    private readonly string _keywordName;

    private CallArgumentValue(CallArgumentPlacement placement, string keywordName, object value)
    {
        Placement = placement;
        _keywordName = keywordName;
        Value = value;
    }

    public static CallArgumentValue Positional(object value)
        => new(CallArgumentPlacement.Positional, string.Empty, value);

    public static CallArgumentValue Keyword(string name, object value)
        => new(CallArgumentPlacement.Keyword, name, value);

    public CallArgumentPlacement Placement { get; }

    public bool IsPositional => Placement == CallArgumentPlacement.Positional;

    public bool IsKeyword => Placement == CallArgumentPlacement.Keyword;

    public string KeywordName => IsKeyword
        ? _keywordName
        : throw new InvalidOperationException("A positional call argument has no keyword name.");

    public object Value { get; }
}
