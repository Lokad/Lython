using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyIndexing
{
    public readonly record struct SliceBounds(int Start, int End, int Step)
    {
        public IEnumerable<int> Indices()
        {
            if (Step > 0)
            {
                for (var i = Start; i < End; i += Step)
                {
                    yield return i;
                }
            }
            else
            {
                for (var i = Start; i > End; i += Step)
                {
                    yield return i;
                }
            }
        }
    }

    public static object ReadIndex(object target, object index, LythonSourceSpan span)
    {
        return target switch
        {
            IPySubscriptableValue value => value.GetSubscript(index, span),
            IPyIndexableValue value => value.GetIndex(NormalizeIndex(index, value.Length, span)),
            PyDict dict => ReadDictIndex(dict, index, span),
            PyCounter counter => ReadCounterIndex(counter, index, span),
            _ when PyStringOps.TryAsString(target, out var text) => text.Index(NormalizeIndex(index, text.Length, span)),
            _ => throw RuntimeErrors.NotSubscriptable(span)
        };
    }

    public static object ReadSlice(object target, object? start, object? end, object? step, LythonSourceSpan span)
    {
        return target switch
        {
            IPySliceableValue value => value.GetSlice(start, end, step, span),
            IPyIndexableValue value => value.GetSlice(SliceIndices(value.Length, start, end, step, span)),
            _ when PyStringOps.TryAsString(target, out var text) => text.Slice(SliceIndices(text.Length, start, end, step, span).ToArray()),
            _ => throw RuntimeErrors.NotSliceable(span)
        };
    }

    public static int NormalizeIndex(object? index, int length, LythonSourceSpan span)
    {
        if (index is not BigInteger integer)
        {
            throw RuntimeErrors.Type("Indices must be integers.", span);
        }

        if (integer < int.MinValue || integer > int.MaxValue)
        {
            throw new LythonRuntimeException("IndexError", "Index is out of range.", span);
        }

        var position = (int)integer;
        if (position < 0)
        {
            position += length;
        }

        if (position < 0 || position >= length)
        {
            throw new LythonRuntimeException("IndexError", "Index is out of range.", span);
        }

        return position;
    }

    public static IEnumerable<int> SliceIndices(int length, object? start, object? end, object? step, LythonSourceSpan span)
        => NormalizeSliceBounds(length, start, end, step, span).Indices();

    public static SliceBounds NormalizeSliceBounds(int length, object? start, object? end, object? step, LythonSourceSpan span)
    {
        var stepValue = NormalizeSliceBound(step, span) ?? BigInteger.One;
        if (stepValue.IsZero)
        {
            throw RuntimeErrors.Value("slice step cannot be zero", span);
        }

        if (stepValue < int.MinValue || stepValue > int.MaxValue)
        {
            return new SliceBounds(0, 0, 1);
        }

        var stepInt = (int)stepValue;
        var startInt = NormalizeSliceStart(length, NormalizeSliceBound(start, span), stepInt);
        var endInt = NormalizeSliceEnd(length, NormalizeSliceBound(end, span), stepInt);
        return new SliceBounds(startInt, endInt, stepInt);
    }

    private static object ReadDictIndex(PyDict dict, object index, LythonSourceSpan span)
    {
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        if (!dict.TryGetValue(key, out var value))
        {
            throw RuntimeErrors.Key("Key was not found.", span);
        }

        return value;
    }

    private static object ReadCounterIndex(PyCounter counter, object index, LythonSourceSpan span)
    {
        var key = LythonRuntime.ValidateDictionaryKey(index, span);
        return counter.GetCount(key);
    }

    private static BigInteger? NormalizeSliceBound(object? value, LythonSourceSpan span)
    {
        return value switch
        {
            null => null,
            BigInteger integer => integer,
            _ => throw RuntimeErrors.Type("Slice indices must be integers or None.", span)
        };
    }

    private static int NormalizeSliceStart(int length, BigInteger? start, int step)
    {
        if (start is null)
        {
            return step > 0 ? 0 : length - 1;
        }

        var value = start.Value;
        if (value < 0)
        {
            value += length;
        }

        if (step > 0)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > length ? length : (int)value;
        }

        if (value < -1)
        {
            return -1;
        }

        return value >= length ? length - 1 : (int)value;
    }

    private static int NormalizeSliceEnd(int length, BigInteger? end, int step)
    {
        if (end is null)
        {
            return step > 0 ? length : -1;
        }

        var value = end.Value;
        if (value < 0)
        {
            value += length;
        }

        if (step > 0)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > length ? length : (int)value;
        }

        if (value < -1)
        {
            return -1;
        }

        return value >= length ? length - 1 : (int)value;
    }
}
