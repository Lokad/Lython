using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object ValidateSetItem(object value, LythonSourceSpan span)
        => ValidateDictionaryKey(value, span);

    internal static object ValidateDictionaryKey(object value, LythonSourceSpan? span)
        => ValidateKeyShape(value, span);

    // Shared set/dict key validation: hashability is a property of the existing
    // items, so a validated tuple keeps its identity (and nested identities)
    // instead of rebuilding copies that break `is` checks downstream. Validation
    // never transforms items; anything unhashable throws UnhashableType.
    private static object ValidateKeyShape(object value, LythonSourceSpan? span)
    {
        // The None slot is established at class creation; this check never
        // invokes guest code or infers hashability from a later __eq__ edit.
        if (value is PyInstance instance && PyHashProtocols.HasDisabledHash(instance))
        {
            throw RuntimeErrors.UnhashableType(value, span);
        }

        var normalized = value switch
        {
            PyTuple tuple => ValidateTupleKey(tuple, span),
            IPyHashableValue => value,
            _ when PyNumberOps.TryAsNumber(value, out _) => value,
            _ => throw RuntimeErrors.UnhashableType(value, span)
        };

        return normalized;
    }

    private static PyTuple ValidateTupleKey(PyTuple tuple, LythonSourceSpan? span)
    {
        using (PyStructuralGuard.EnterSingle(tuple, span))
        {
            for (var i = 0; i < tuple.Count; i++)
            {
                PyStructuralGuard.NoteWork();
                ValidateKeyShape(tuple[i], span);
            }

            return tuple;
        }
    }

    private static long EstimateObjectArrayBytes(int count) => 32L + (16L * count);

    internal abstract class ControlSignal : Exception
    {
    }

    internal sealed class BreakSignal : ControlSignal
    {
    }

    internal sealed class ContinueSignal : ControlSignal
    {
    }

    internal sealed class ReturnSignal : Exception
    {
        public ReturnSignal(object value)
        {
            Value = value;
        }

        public object Value { get; }
    }

    // Abrupt outcome of a lowered statement block, traveling as a value so
    // ordinary returns never throw through block machinery. Both null means
    // the block fell through; control and return never coexist.
    internal readonly record struct LoweredBlockFlow(ControlSignal? Control, ReturnSignal? Return);
}
