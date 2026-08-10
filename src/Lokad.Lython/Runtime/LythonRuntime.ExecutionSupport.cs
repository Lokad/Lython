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
        => ValidateSetItem(value, span, null);

    private static object ValidateSetItem(object value, LythonSourceSpan span, MemoryGovernor? governor)
    {
        var normalized = value switch
        {
            PyTuple tuple => NormalizeValidatedTuple(tuple, item => ValidateSetItem(item, span, governor), governor, span),
            IPyHashableValue or bool or BigInteger or double => value,
            _ => throw RuntimeErrors.SetElementsMustBeHashable(span)
        };

        return EnsureHashableValue(normalized, span, "set elements must be hashable.");
    }

    internal static object ValidateDictionaryKey(object value, LythonSourceSpan? span)
        => ValidateDictionaryKey(value, span, null);

    internal static object ValidateDictionaryKey(object value, LythonSourceSpan? span, MemoryGovernor? governor)
    {
        var normalized = value switch
        {
            PyTuple tuple => NormalizeValidatedTuple(tuple, item => ValidateDictionaryKey(item, span, governor), governor, span),
            IPyHashableValue or bool or BigInteger or double => value,
            _ => throw new LythonRuntimeException("TypeError", "dictionary keys must be hashable.", span)
        };

        return EnsureHashableValue(normalized, span, "dictionary keys must be hashable.");
    }

    private static object EnsureHashableValue(object value, LythonSourceSpan? span, string message)
    {
        try
        {
            _ = PyValueComparer.Instance.GetHashCode(value);
            return value;
        }
        catch (InvalidOperationException)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }
    }

    private static PyTuple NormalizeValidatedTuple(PyTuple tuple, Func<object, object> normalize, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var items = new object[tuple.Count];
        for (var i = 0; i < tuple.Count; i++)
        {
            items[i] = normalize(tuple[i]);
        }

        return governor is null ? new PyTuple(items) : new PyTuple(items, governor, span);
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

        public const int MaxInterpreterDepth = 512;

        public int ExecutionStepCount { get; set; }

        public int CurrentRecursionDepth { get; set; }

        public int CurrentInterpreterDepth { get; set; }

        public int HostCallCount { get; set; }

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

            if (limit.Value.Bytes > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(parameterName, limit.Value.Bytes, "This byte limit cannot exceed Int32.MaxValue.");
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
}
