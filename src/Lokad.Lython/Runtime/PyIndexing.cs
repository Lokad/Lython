using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyIndexing
{
    // Names the receiver for index-type failures like CPython; Unnamed keeps
    // the legacy context-free text where no receiver shape is known.
    internal enum IndexTargetName
    {
        Unnamed,
        List,
        Tuple,
        Text,
        Sequence,
    }

    // Distinguishes reads from deletions and stores because out-of-range
    // failures name the operation like CPython.
    internal enum IndexOperation
    {
        Read,
        Delete,
        Assign,
    }

    public readonly record struct SliceBounds(int Start, int End, int Step)
    {
        public int Count => Step > 0
            ? Start >= End ? 0 : (int)(1L + ((long)End - 1 - Start) / Step)
            : Start <= End ? 0 : (int)(1L + ((long)Start - 1 - End) / -(long)Step);

        public IEnumerable<int> Indices()
        {
            if (Step > 0)
            {
                for (long i = Start; i < End; i += Step)
                {
                    yield return (int)i;
                }
            }
            else
            {
                for (long i = Start; i > End; i += Step)
                {
                    yield return (int)i;
                }
            }
        }

        public bool Contains(int index)
        {
            if (Step > 0)
            {
                return index >= Start && index < End && (index - (long)Start) % Step == 0;
            }

            return index <= Start && index > End && ((long)Start - index) % -(long)Step == 0;
        }
    }

    public static object ReadIndex(object target, object index, LythonSourceSpan span)
    {
        if (index is PySlice slice)
        {
            return ReadSlice(target, slice.StartBound, slice.StopBound, slice.StepBound, span);
        }

        return target switch
        {
            IPySubscriptableValue value => value.GetSubscript(index, span),
            IPyIndexableValue value => value.GetIndex(NormalizeIndex(index, value.Length, span, TargetKind(value))),
            PyDict dict => ReadDictIndex(dict, index, span),
            PyCounter counter => ReadCounterIndex(counter, index, span),
            _ when PyStringOps.TryAsString(target, out var text) => text.Index(NormalizeIndex(index, text.Length, span, IndexTargetName.Text)),
            _ => throw RuntimeErrors.NotSubscriptable(span)
        };
    }

    public static object ReadSlice(object target, object? start, object? end, object? step, LythonSourceSpan span, LythonRuntime.ExecutionContext? context = null)
    {
        var result = target switch
        {
            IPySliceableValue value => value.GetSlice(start, end, step, span),
            IPyIndexableValue value => value.GetSlice(SliceIndices(value.Length, start, end, step, span)),
            _ when PyStringOps.TryAsString(target, out var text) => text.Slice(NormalizeSliceBounds(text.Length, start, end, step, span)),
            _ => throw RuntimeErrors.NotSliceable(span)
        };

        // String slices built from unowned receivers would escape accounting.
        if (context is not null && result is PyString textResult && target is PyString receiver)
        {
            return LythonRuntime.OwnMethodResult(textResult, receiver, context.MemoryGovernor, span);
        }

        return result;
    }

    public static int NormalizeIndex(object? index, int length, LythonSourceSpan span)
        => NormalizeIndex(index, length, span, IndexTargetName.Unnamed);

    public static int NormalizeIndex(object? index, int length, LythonSourceSpan span, IndexTargetName target)
        => NormalizeIndex(index, length, span, target, IndexOperation.Read);

    public static int NormalizeIndex(object? index, int length, LythonSourceSpan span, IndexTargetName target, IndexOperation operation)
    {
        BigInteger integer;
        if (index is bool flag)
        {
            integer = flag ? BigInteger.One : BigInteger.Zero;
        }
        else if (index is not BigInteger big)
        {
            throw InvalidIndexType(index, target, span);
        }
        else
        {
            integer = big;
        }

        return ApplyIndexBounds(integer, length, span, target, operation);
    }

    public static int NormalizePopIndex(object? index, int length, LythonSourceSpan span)
    {
        BigInteger integer;
        if (index is bool flag)
        {
            integer = flag ? BigInteger.One : BigInteger.Zero;
        }
        else if (index is not BigInteger)
        {
            throw new LythonRuntimeException("TypeError", $"'{IndexTypeName(index)}' object cannot be interpreted as an integer", span);
        }
        else
        {
            integer = (BigInteger)index;
        }

        if (integer < int.MinValue || integer > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
        }

        var position = (int)integer;
        if (position < 0)
        {
            position += length;
        }

        if (position < 0 || position >= length)
        {
            throw new LythonRuntimeException("IndexError", "pop index out of range", span);
        }

        return position;
    }

    internal static IndexTargetName TargetKind(object target) => target switch
    {
        PyString => IndexTargetName.Text,
        PyList => IndexTargetName.List,
        PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject => IndexTargetName.Tuple,
        PyDeque => IndexTargetName.Sequence,
        _ => IndexTargetName.Unnamed,
    };

    private static int ApplyIndexBounds(BigInteger integer, int length, LythonSourceSpan span, IndexTargetName target, IndexOperation operation)
    {
        if (integer < int.MinValue || integer > int.MaxValue)
        {
            if (target == IndexTargetName.Unnamed)
            {
                throw new LythonRuntimeException("IndexError", "Index is out of range.", span);
            }

            throw new LythonRuntimeException("IndexError", "cannot fit 'int' into an index-sized integer", span);
        }

        var position = (int)integer;
        if (position < 0)
        {
            position += length;
        }

        if (position < 0 || position >= length)
        {
            throw OutOfRange(target, operation, span);
        }

        return position;
    }

    private static LythonRuntimeException OutOfRange(IndexTargetName target, IndexOperation operation, LythonSourceSpan span)
    {
        if (target == IndexTargetName.Unnamed)
        {
            return new LythonRuntimeException("IndexError", "Index is out of range.", span);
        }

        var name = target switch
        {
            IndexTargetName.List => "list",
            IndexTargetName.Tuple => "tuple",
            IndexTargetName.Text => "string",
            _ => "deque",
        };

        // Deque never names the operation; lists and tuples name stores.
        var message = operation == IndexOperation.Read || target == IndexTargetName.Sequence
            ? $"{name} index out of range"
            : $"{name} assignment index out of range";

        return new LythonRuntimeException("IndexError", message, span);
    }

    private static LythonRuntimeException InvalidIndexType(object? index, IndexTargetName target, LythonSourceSpan span)
    {
        var name = IndexTypeName(index);
        var message = target switch
        {
            IndexTargetName.List => $"list indices must be integers or slices, not {name}",
            IndexTargetName.Tuple => $"tuple indices must be integers or slices, not {name}",
            IndexTargetName.Text => $"string indices must be integers, not '{name}'",
            IndexTargetName.Sequence => $"sequence index must be integer, not '{name}'",
            _ => "Indices must be integers.",
        };

        return RuntimeErrors.Type(message, span);
    }

    private static string IndexTypeName(object? index) => index switch
    {
        null => "NoneType",
        PyNone => "NoneType",
        PyString => "str",
        double => "float",
        BigInteger or int or bool => "int",
        PyList => "list",
        PyDict or PyDefaultDict or PyCounter => "dict",
        PyTuple => "tuple",
        PySet => "set",
        PyBytes => "bytes",
        PyRange => "range",
        PyDeque => "deque",
        PyChainMap => "ChainMap",
        PyInstance instance => instance.Type.Name,
        _ => "object",
    };

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
            throw RuntimeErrors.MissingKey(index, span);
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
            bool flag => flag ? BigInteger.One : BigInteger.Zero,
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
