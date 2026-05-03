namespace Lokad.Lython.Runtime;

internal interface IPyIterableValue
{
    IEnumerable<object> Iterate();
}

internal interface IPyIteratorValue : IPyIterableValue
{
    bool TryMoveNext(out object value);
}
