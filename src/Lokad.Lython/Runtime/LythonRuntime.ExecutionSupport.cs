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
            IPyHashableValue or bool or BigInteger or double => value,
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

        public int? MaxExecutionSteps { get; init; }

        public int? MaxRecursionDepth { get; init; }

        public int? MaxHostCalls { get; init; }

        public int? MaxCollectionSize { get; init; }

        public int? MaxStringLength { get; init; }

        public int? MaxHostReadBytes { get; init; }

        public int? MaxStandardOutputBytes { get; init; }

        public int? MaxStandardErrorBytes { get; init; }

        public long? MaxExecutionMemoryBytes { get; init; }

        // Counters stay 64-bit wide: reaching 2^63 increments is infeasible, so
    // enforcement can never wrap around to negative and switch itself off.
    // Bounds interpreter nesting (and, through it, every execution path that
    // nests Python calls) at a fixed count. Depth is tracked once per Python
    // nesting level (calls, bodies, compound statements), not per syntax
    // node, so funded depths fit with margin on every path. Synchronous roots
    // run on a dedicated large stack. Deep-copy shares this bound
    // (MG20 pin: 512-deep copies succeed, 600-deep fail).
    public const int MaxInterpreterDepth = 512;

    // Stack-probe backstop (MG25): the counters above cannot trip first when
    // the CLR stack is nearly exhausted, so nesting past this depth also
    // proves real stack headroom before deepening. The trip fires while fewer
    // than the headroom bytes remain (see StackRuler); it is a last-resort
    // backstop for stacks the counters cannot see (exotic hosts, pool-thread
    // continuations), not the primary limiter, so it stays small enough to
    // never disturb funded depths. The threshold keeps shallow programs
    // (and synthetic counter drills, which nest no CLR frames) on the
    // counter-only fast path.
    public const int StackProbeDepthThreshold = 32;
    public const long StackProbeHeadroomBytes = 96 * 1024;

        public long ExecutionStepCount { get; set; }

        public long CurrentRecursionDepth { get; set; }

        public long CurrentInterpreterDepth { get; set; }

        public long HostCallCount { get; set; }

        public static ExecutionLimits FromOptions(LythonRunOptions? options)
        {
            var useDefaultLimits = options?.DisableDefaultLimits != true;
            return new ExecutionLimits
            {
                CancellationToken = options?.CancellationToken ?? CancellationToken.None,
                MaxExecutionSteps = NonNegativeOrDefault(options?.MaxExecutionSteps?.Count, useDefaultLimits ? LythonRunOptions.DefaultMaxExecutionSteps : null, nameof(LythonRunOptions.MaxExecutionSteps)),
                MaxRecursionDepth = NonNegativeOrDefault(options?.MaxRecursionDepth?.Count, useDefaultLimits ? LythonRunOptions.DefaultMaxRecursionDepth : null, nameof(LythonRunOptions.MaxRecursionDepth)),
                MaxHostCalls = NonNegativeOrDefault(options?.MaxHostCalls?.Count, useDefaultLimits ? LythonRunOptions.DefaultMaxHostCalls : null, nameof(LythonRunOptions.MaxHostCalls)),
                MaxCollectionSize = NonNegativeOrDefault(options?.MaxCollectionSize?.Count, useDefaultLimits ? LythonRunOptions.DefaultMaxCollectionSize : null, nameof(LythonRunOptions.MaxCollectionSize)),
                MaxStringLength = NonNegativeOrDefault(options?.MaxStringLength?.Count, useDefaultLimits ? LythonRunOptions.DefaultMaxStringLength : null, nameof(LythonRunOptions.MaxStringLength)),
                MaxHostReadBytes = NonNegativeOrDefault(ToInt32Bytes(options?.MaxHostReadBytes, nameof(LythonRunOptions.MaxHostReadBytes)), useDefaultLimits ? LythonRunOptions.DefaultMaxHostReadBytes : null, nameof(LythonRunOptions.MaxHostReadBytes)),
                MaxStandardOutputBytes = NonNegativeOrDefault(ToInt32Bytes(options?.MaxStandardOutputBytes, nameof(LythonRunOptions.MaxStandardOutputBytes)), useDefaultLimits ? LythonRunOptions.DefaultMaxStandardOutputBytes : null, nameof(LythonRunOptions.MaxStandardOutputBytes)),
                MaxStandardErrorBytes = NonNegativeOrDefault(ToInt32Bytes(options?.MaxStandardErrorBytes, nameof(LythonRunOptions.MaxStandardErrorBytes)), useDefaultLimits ? LythonRunOptions.DefaultMaxStandardErrorBytes : null, nameof(LythonRunOptions.MaxStandardErrorBytes)),
                MaxExecutionMemoryBytes = NonNegativeOrDefault(options?.MaxExecutionMemoryBytes?.Bytes, useDefaultLimits ? LythonRunOptions.DefaultMaxExecutionMemoryBytes : null, nameof(LythonRunOptions.MaxExecutionMemoryBytes)),
            };
        }

        private static int? ToInt32Bytes(LythonByteLimit? limit, string parameterName)
        {
            if (limit is null)
            {
                return null;
            }

            if (limit.Value.Bytes < 0 || limit.Value.Bytes > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(parameterName, limit.Value.Bytes, "This byte limit cannot be negative and cannot exceed Int32.MaxValue.");
            }

            return (int)limit.Value.Bytes;
        }

        internal static int? NonNegativeOrDefault(int? value, int? defaultValue, string parameterName)
        {
            if (value is < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Execution limits cannot be negative.");
            }

            return value ?? defaultValue;
        }

        internal static long? NonNegativeOrDefault(long? value, long? defaultValue, string parameterName)
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
