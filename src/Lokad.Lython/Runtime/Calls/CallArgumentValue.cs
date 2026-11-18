using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal readonly record struct CallArgumentValue
{
    private readonly object? _keyword;
    private readonly PyString? _originalKeyword;

    private CallArgumentValue(object? keyword, object value, PyString? originalKeyword)
    {
        _keyword = keyword;
        Value = value;
        _originalKeyword = originalKeyword;
    }

    public static CallArgumentValue Positional(object value) => new(null, value, null);
    public static CallArgumentValue Keyword(string name, object value, PyString? originalKeyword = null)
        => new(name, value, originalKeyword);
    public static CallArgumentValue Keyword(PyString key, object value) => new(key, value, null);

    public PyString? NativeKeyword => _keyword as PyString;
    public PyString? OriginalKeyword => _originalKeyword ?? NativeKeyword;
    public bool IsPositional => _keyword is null;
    public bool IsKeyword => _keyword is not null;

    public PyString GetKeywordKey(LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
        => OriginalKeyword ?? PyString.FromString(KeywordName, context.MemoryGovernor, span);

    public CallArgumentValue WithValue(object value) => new(_keyword, value, _originalKeyword);

    public string GetKeywordName(LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        if (NativeKeyword is not { } key) return KeywordName;
        context.CheckExecution(span);
        if (key.Utf8Span.IsEmpty) return string.Empty;
        var bytes = checked(32L + 2L * key.GetUtf16CodeUnitCount(context.State.CancellationToken, span));
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var name = key.AsString();
        storage.Commit(bytes);
        return name;
    }

    public string KeywordName => _keyword as string ??
        throw new InvalidOperationException("This call argument does not carry a CLR keyword name.");

    public object Value { get; }
}
