namespace Lokad.Lython.Runtime;

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
    ValueTask<(bool HasValue, object Value)> TryMoveNextAsync();
}
