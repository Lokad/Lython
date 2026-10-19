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
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object ValidateSetItem(object value, LythonSourceSpan span)
        => ValidateHashableKey(value, span);

    internal static object ValidateDictionaryKey(object value, LythonSourceSpan? span)
        => ValidateHashableKey(value, span);

    // Shared set/dict key validation: hashability is a property of the existing
    // items, so a validated tuple keeps its identity (and nested identities)
    // instead of rebuilding copies that break `is` checks downstream. Validation
    // never transforms items; anything unhashable throws UnhashableType.
    private static object ValidateHashableKey(object value, LythonSourceSpan? span)
    {
        // R13b: customizing __eq__ without __hash__ is unhashable like CPython.
        // The slot-presence check is a pure MRO walk, so validation stays
        // context-free across all key entry points.
        if (value is PyInstance instance && PyHashProtocols.IsEqWithoutHash(instance))
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

        return EnsureHashableValue(normalized, span);
    }

    private static PyTuple ValidateTupleKey(PyTuple tuple, LythonSourceSpan? span)
    {
        using (PyStructuralGuard.EnterSingle(tuple, span))
        {
            for (var i = 0; i < tuple.Count; i++)
            {
                PyStructuralGuard.NoteWork();
                ValidateHashableKey(tuple[i], span);
            }

            return tuple;
        }
    }

    private static object EnsureHashableValue(object value, LythonSourceSpan? span)
    {
        try
        {
            _ = PyValueComparer.Instance.GetHashCode(value);
            return value;
        }
        catch (InvalidOperationException)
        {
            throw RuntimeErrors.UnhashableType(value, span);
        }
    }

    private static long EstimateObjectArrayBytes(int count) => 32L + (16L * count);

    internal sealed class ExecutionLimits
    {
        private ExecutionLimits()
        {
        }

        public CancellationToken CancellationToken { get; init; }

        public int? MaxRecursionDepth { get; init; }

        // Bounds interpreter nesting (and, through it, every execution path that
        // nests Python calls) at a fixed count. Depth is tracked once per Python
        // nesting level (calls, bodies, compound statements), not per syntax
        // node, so funded depths fit with margin on every path. Synchronous roots
        // run on a dedicated large stack. Deep-copy shares this bound
        // (MG20 pin: 512-deep copies succeed, 600-deep fail).
        public const int MaxInterpreterDepth = 512;

        public long CurrentRecursionDepth { get; set; }

        public long CurrentInterpreterDepth { get; set; }

        public static ExecutionLimits FromOptions(LythonRunOptions? options)
        {
            return new ExecutionLimits
            {
                CancellationToken = options?.CancellationToken ?? CancellationToken.None,
                MaxRecursionDepth = NonNegativeOrDefault(options?.MaxRecursionDepth?.Count, LythonRunOptions.DefaultMaxRecursionDepth, nameof(LythonRunOptions.MaxRecursionDepth)),
            };
        }

        internal static int? NonNegativeOrDefault(int? value, int? defaultValue, string parameterName)
        {
            if (value is < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Execution limits cannot be negative.");
            }

            return value ?? defaultValue;
        }

    }

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
