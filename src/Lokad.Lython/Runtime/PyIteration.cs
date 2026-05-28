using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyIteration
{
    public static IEnumerable<object> ToSequence(object value, LythonSourceSpan span)
    {
        return value switch
        {
            PyNone => throw RuntimeErrors.Type("Object is not iterable.", span),
            PyDict dict => dict.Keys,
            IPyIteratorValue iterator => EnumerateIterator(iterator),
            IPyIterableValue iterable => iterable.Iterate(),
            IEnumerable<object> typed => typed,
            System.Collections.IEnumerable untyped => EnumerateUntyped(untyped),
            _ => throw RuntimeErrors.Type("Object is not iterable.", span)
        };
    }

    private static IEnumerable<object> EnumerateIterator(IPyIteratorValue iterator)
    {
        while (iterator.TryMoveNext(out var value))
        {
            yield return value;
        }
    }

    private static IEnumerable<object> EnumerateUntyped(System.Collections.IEnumerable sequence)
    {
        foreach (var value in sequence)
        {
            yield return LythonRuntime.RuntimeValue(value);
        }
    }
}
