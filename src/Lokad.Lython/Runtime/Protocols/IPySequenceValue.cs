namespace Lokad.Lython.Runtime;

internal interface IPySequenceValue : IReadOnlyList<object>
{
    object GetItem(int index);

    object CreateSlice(IEnumerable<object> items);
}

internal interface IMutablePySequenceValue : IPySequenceValue
{
    void SetItem(int index, object value);

    void RemoveAt(int index);
}
