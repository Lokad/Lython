namespace Lokad.Lython.Runtime;

internal readonly struct PyIterationResult
{
    private readonly object _value;

    private PyIterationResult(object value)
    {
        _value = value;
        HasValue = true;
    }

    public static PyIterationResult End => default;

    public static PyIterationResult Yield(object value) => new(value);

    public bool HasValue { get; }

    public object Value => HasValue
        ? _value
        : throw new InvalidOperationException("An exhausted iterator has no value.");

    public void Deconstruct(out bool hasValue, out object value)
    {
        hasValue = HasValue;
        value = HasValue ? _value : PyNone.Instance;
    }
}

internal interface IPyIterableValue
{
    IEnumerable<object> Iterate();
}

internal interface IPyAsyncIterableValue : IPyIterableValue
{
    IAsyncEnumerable<object> IterateAsync();
}

internal interface IPyIteratorValue : IPyIterableValue
{
    bool TryMoveNext([MaybeNullWhen(false)] out object value);
}

internal interface IPyAsyncIteratorValue : IPyIteratorValue, IPyAsyncIterableValue
{
    ValueTask<PyIterationResult> TryMoveNextAsync();
}
