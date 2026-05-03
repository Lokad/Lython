namespace Lokad.Lython.Runtime;

internal interface IPyIndexableValue
{
    int Length { get; }

    object GetIndex(int index);

    object GetSlice(IEnumerable<int> indices);
}

internal interface IMutablePyIndexableValue : IPyIndexableValue
{
    void SetIndex(int index, object value);
}
