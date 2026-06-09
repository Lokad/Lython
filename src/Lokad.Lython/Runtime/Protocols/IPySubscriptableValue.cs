namespace Lokad.Lython.Runtime;

internal interface IPySubscriptableValue
{
    object GetSubscript(object index, LythonSourceSpan span);
}

internal interface IMutablePySubscriptableValue : IPySubscriptableValue
{
    void SetSubscript(object index, object value, LythonSourceSpan span);
}

internal interface IDeletablePySubscriptableValue : IPySubscriptableValue
{
    void DeleteSubscript(object index, LythonSourceSpan span);
}

internal interface IPySliceableValue
{
    object GetSlice(object? start, object? end, object? step, LythonSourceSpan span);
}
