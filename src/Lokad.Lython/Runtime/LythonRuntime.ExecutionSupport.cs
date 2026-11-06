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
    {
        // Coarse shape rejection can precede construction, but nested elements
        // must be visited by the actual hash walk in Python order. In particular,
        // an earlier __hash__ callback precedes a later unhashable tuple element.
        // The None slot is established at class creation; this check never
        // invokes guest code or infers hashability from a later __eq__ edit.
        if (value is PyInstance instance && PyHashProtocols.HasDisabledHash(instance))
        {
            throw RuntimeErrors.UnhashableType(value, span);
        }

        var normalized = value switch
        {
            IPyHashableValue => value,
            _ when PyNumberOps.TryAsNumber(value, out _) => value,
            _ => throw RuntimeErrors.UnhashableType(value, span)
        };

        return normalized;
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
