namespace Lokad.Lython.Runtime;

internal interface IPyContainsValue
{
    bool Contains(object candidate, LythonSourceSpan span);
}
