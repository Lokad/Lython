using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class ExecutionValueObservation
{
    public ExecutionValueObservation(ExecutionState state)
    {
        State = state;
    }

    public ExecutionState State { get; }

    public LythonRuntime.ExecutionLimits Limits => State.Limits;

    public LegacyApproximateMemoryDiagnostics LegacyApproximateMemoryDiagnostics => State.LegacyApproximateMemoryDiagnostics;

    public void ObserveString(PyString text, LythonSourceSpan? span)
    {
        EnforceStringLengthLimit(text, span);

        if (text.OwnerMemoryGovernor is null)
        {
            TrackLegacyApproximateBytes(32L + text.Utf8Bytes.Length, span);
        }
    }

    public void ObserveCollectionCount(int count, LythonSourceSpan? span)
    {
        EnforceCollectionCountLimit(count, span);
    }

    public void ObserveValue(object value, LythonSourceSpan? span)
    {
        EnforceValueLimits(value, span);

        if (ShouldTrackApproximateValue(value))
        {
            TrackLegacyApproximateBytes(EstimateApproximateValueBytes(value), span);
        }
    }

    private void TrackLegacyApproximateBytes(long bytes, LythonSourceSpan? span)
    {
        if (bytes <= 0)
        {
            return;
        }

        LegacyApproximateMemoryDiagnostics.TrackBytes(bytes, span);
    }

    private void EnforceStringLengthLimit(PyString text, LythonSourceSpan? span)
    {
        if (Limits.MaxStringLength is { } maxStringLength && text.Length > maxStringLength)
        {
            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
        }
    }

    private void EnforceCollectionCountLimit(int count, LythonSourceSpan? span)
    {
        if (Limits.MaxCollectionSize is { } maxCollectionSize && count > maxCollectionSize)
        {
            throw RuntimeErrors.Runtime($"maximum collection size exceeded ({maxCollectionSize})", span);
        }
    }

    private void EnforceValueLimits(object value, LythonSourceSpan? span)
    {
        if (Limits.MaxStringLength is { } maxStringLength)
        {
            switch (value)
            {
                case PyString text when text.Length > maxStringLength:
                    throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
                case LythonRuntime.ReFindAllResult matches:
                    foreach (var item in matches.Items)
                    {
                        if (item is PyString pyText && pyText.Length > maxStringLength)
                        {
                            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
                        }
                    }

                    break;
            }
        }

        if (TryGetCollectionCount(value) is { } count)
        {
            EnforceCollectionCountLimit(count, span);
        }
    }

    private static bool ShouldTrackApproximateValue(object value)
    {
        return value switch
        {
            IPyGovernedValue { OwnerMemoryGovernor: not null } => false,
            LythonRuntime.ReFindAllResult { Items.OwnerMemoryGovernor: not null } => false,
            _ => true
        };
    }

    private static int? TryGetCollectionCount(object value)
    {
        return value switch
        {
            PyList list => list.Count,
            PyTuple tuple => tuple.Count,
            PyDict dict => dict.Count,
            PySet set => set.Count,
            LythonRuntime.ReFindAllResult matches => matches.Items.Count,
            _ => (int?)null
        };
    }

    internal static long EstimateApproximateValueBytes(object value)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return EstimateApproximateValueBytesCore(value, visited);
    }

    private static long EstimateApproximateValueBytesCore(object value, HashSet<object> visited)
    {
        return value switch
        {
            PyNone => 8,
            bool => 8,
            BigInteger integer => RuntimeMemoryEstimates.EstimateBigIntegerBytes(integer),
            double => 16,
            PyString text => 32L + text.Utf8Bytes.Length,
            PyList list => EstimateApproximateListBytes(list, visited),
            PyTuple tuple => EstimateApproximateTupleBytes(tuple, visited),
            PyDict dict => EstimateApproximateDictionaryBytes(dict, visited),
            PySet set => EstimateApproximateSetBytes(set, visited),
            LythonRuntime.ReFindAllResult matches => 32 + EstimateApproximateListBytes(matches.Items, visited),
            LythonRuntime.ExecutionContext.TextFileHandle handle => 64 + handle.Path.Length + handle.Mode.Length,
            LythonRuntime.DictKeysView or LythonRuntime.DictValuesView or LythonRuntime.DictItemsView => 64,
            _ => 64
        };
    }

    private static long EstimateApproximateListBytes(IEnumerable<object> items, HashSet<object> visited)
    {
        long total = 64;
        foreach (var item in items)
        {
            total += 16;
            total += EstimateApproximateNestedValueBytes(item, visited);
        }

        return total;
    }

    private static long EstimateApproximateTupleBytes(PyTuple tuple, HashSet<object> visited)
    {
        long total = 48;
        foreach (var item in tuple)
        {
            total += 16;
            total += EstimateApproximateNestedValueBytes(item, visited);
        }

        return total;
    }

    private static long EstimateApproximateDictionaryBytes(PyDict dict, HashSet<object> visited)
    {
        long total = 96;
        foreach (var pair in dict)
        {
            total += 32;
            total += EstimateApproximateNestedValueBytes(pair.Key, visited);
            total += EstimateApproximateNestedValueBytes(pair.Value, visited);
        }

        return total;
    }

    private static long EstimateApproximateSetBytes(PySet set, HashSet<object> visited)
    {
        long total = 80;
        foreach (var item in set)
        {
            total += 24;
            total += EstimateApproximateNestedValueBytes(item, visited);
        }

        return total;
    }

    private static long EstimateApproximateNestedValueBytes(object value, HashSet<object> visited)
    {
        if (!IsReferenceTracked(value))
        {
            return EstimateApproximateValueBytesCore(value, visited);
        }

        if (!visited.Add(value))
        {
            return 0;
        }

        return EstimateApproximateValueBytesCore(value, visited);
    }

    private static bool IsReferenceTracked(object value)
    {
        return value is PyList or PyTuple or PyDict or PySet or LythonRuntime.ReFindAllResult;
    }
}
