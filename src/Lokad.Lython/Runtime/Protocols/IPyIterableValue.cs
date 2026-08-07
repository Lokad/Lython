namespace Lokad.Lython.Runtime;

internal readonly record struct PyIterationResult(bool HasValue, object Value)
{
    public static implicit operator PyIterationResult((bool HasValue, object Value) result)
        => new(result.HasValue, result.Value);
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
