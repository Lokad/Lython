using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyContainment
{
    public static bool Contains(object container, object candidate, LythonSourceSpan span)
    {
        return container switch
        {
            PyString text when PyStringOps.TryAsString(candidate, out var part) => text.Contains(part),
            PyDict dict => dict.ContainsKey(LythonRuntime.ValidateDictionaryKey(candidate, span)),
            PySet set => set.Contains(candidate),
            IEnumerable<object> sequence => ContainsInTypedSequence(sequence, candidate),
            System.Collections.IEnumerable sequence => ContainsInUntypedSequence(sequence, candidate),
            _ => throw new LythonRuntimeException("TypeError", "Right operand does not support membership testing.", span),
        };
    }

    private static bool ContainsInTypedSequence(IEnumerable<object> sequence, object candidate)
    {
        foreach (var item in sequence)
        {
            if (PyEquality.AreEqual(item, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInUntypedSequence(System.Collections.IEnumerable sequence, object candidate)
    {
        foreach (var item in sequence)
        {
            if (PyEquality.AreEqual(LythonRuntime.RuntimeValue(item), candidate))
            {
                return true;
            }
        }

        return false;
    }
}
