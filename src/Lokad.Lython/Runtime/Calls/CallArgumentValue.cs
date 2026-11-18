using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal readonly record struct CallArgumentValue
{
    private readonly string? _keywordName;

    private CallArgumentValue(string? keywordName, object value, PyString? originalKeyword)
    {
        _keywordName = keywordName;
        Value = value;
        OriginalKeyword = originalKeyword;
    }

    public static CallArgumentValue Positional(object value)
        => new(null, value, null);

    public static CallArgumentValue Keyword(string name, object value, PyString? originalKeyword = null)
        => new(name, value, originalKeyword);

    public PyString? OriginalKeyword { get; }

    public bool IsPositional => _keywordName is null;

    public bool IsKeyword => _keywordName is not null;

    public string KeywordName => _keywordName ??
        throw new InvalidOperationException("A positional call argument has no keyword name.");

    public object Value { get; }
}
