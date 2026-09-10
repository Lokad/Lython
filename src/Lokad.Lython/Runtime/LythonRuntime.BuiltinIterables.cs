using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object Len(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "len(value) expects one argument.", span);
        }

        return arguments[0] switch
        {
            PyChainMap chainMap => LenChainMap(chainMap, span, context),
            IPySizedValue sized => new BigInteger(sized.Length),
            IReadOnlyCollection<object> collection => new BigInteger(collection.Count),
            System.Collections.ICollection collection => new BigInteger(collection.Count),
            PyInstance instance => GetInstanceLength(instance, context, span),
            _ => throw new LythonRuntimeException("TypeError", "Object has no len().", span)
        };
    }

    // len() builds only the throwaway dedup set; reuse the merge estimate
    // conservatively since duplicates collapse in the set but pay in visits.
    private static BigInteger LenChainMap(PyChainMap chainMap, LythonSourceSpan span, ExecutionContext context)
    {
        using var scratch = context.MemoryGovernor.ReserveTemporary(chainMap.EstimateMergeScratchBytes(), span);
        return new BigInteger(chainMap.Count);
    }

    private static BigInteger GetInstanceLength(PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        if (!instance.TryGetAttribute("__len__", context, span, out var member) || member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"object of type '{instance.Type.Name}' has no len()", span);
        }

        var value = callable.Invoke([], span, context);
        if (!PyNumberOps.TryAsInteger(value, out var length))
        {
            throw new LythonRuntimeException("TypeError", "__len__() should return an integer", span);
        }

        if (length < 0)
        {
            throw new LythonRuntimeException("ValueError", "__len__() should return >= 0", span);
        }

        return length;
    }

    private static PyList CreateNameList(IEnumerable<string> names, ExecutionContext context, LythonSourceSpan span)
        => new(
            names.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(name => PyString.FromString(name, context.MemoryGovernor, span)),
            context.MemoryGovernor,
            span);

    private static IEnumerable<string> EnumerateCurrentLocalNames(ExecutionContext context)
    {
        foreach (var name in context.Variables.Keys)
        {
            if (!ExecutionState.BuiltinNames.Contains(name))
            {
                yield return name;
            }
        }

        if (context.CurrentExecutableFrame is not null)
        {
            foreach (var pair in context.CurrentExecutableFrame.EnumerateLocals())
            {
                yield return pair.Key;
            }
        }
    }

    private static readonly string[] StringDirNames = ["capitalize", "center", "count", "encode", "endswith", "expandtabs", "find", "format", "index", "isalnum", "isalpha", "isdigit", "islower", "isspace", "isupper", "join", "lower", "lstrip", "maketrans", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rjust", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "translate", "upper", "zfill"];
    private static readonly string[] BytesDirNames = ["decode", "fromhex", "maketrans", "translate"];
    private static readonly string[] ListDirNames = ["append", "clear", "copy", "count", "extend", "index", "insert", "pop", "remove", "reverse", "sort"];
    private static readonly string[] TupleDirNames = ["count", "index"];
    private static readonly string[] NamedTupleDirNames = ["_asdict", "_field_defaults", "_fields", "_make", "_replace", "count", "index"];
    private static readonly string[] TypingNamedTupleDirNames = ["_fields", "_replace", "count", "index"];
    private static readonly string[] NamedTupleTypeDirNames = ["_fields", "_field_defaults", "_make", "__new__", "count", "index"];
    private static readonly string[] FloatDirNames = ["as_integer_ratio", "conjugate", "hex", "imag", "is_integer", "real"];
    private static readonly string[] FloatMethodDirNames = ["as_integer_ratio", "conjugate", "fromhex", "hex", "is_integer"];
    private static readonly string[] IntDirNames = ["as_integer_ratio", "bit_length", "conjugate", "denominator", "imag", "is_integer", "numerator", "real", "to_bytes"];
    private static readonly string[] IntMethodDirNames = ["as_integer_ratio", "bit_length", "conjugate", "from_bytes", "is_integer", "to_bytes"];
    private static readonly string[] DictDirNames = ["clear", "copy", "fromkeys", "get", "items", "keys", "pop", "popitem", "setdefault", "update", "values"];
    private static readonly string[] SetDirNames = ["add", "clear", "copy", "difference", "difference_update", "discard", "intersection", "intersection_update", "isdisjoint", "issubset", "issuperset", "pop", "remove", "symmetric_difference", "symmetric_difference_update", "union", "update"];

    private static object DivMod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "divmod(a, b) expects two arguments.", span);
        }

        if (arguments[0] is PyTimedelta leftDelta && arguments[1] is PyTimedelta rightDelta)
        {
            return PyDateTimeOps.DivMod(leftDelta, rightDelta, context, span);
        }

        try
        {
            return new PyTuple(
                [
                    EvaluateFloorDivide(arguments[0], arguments[1], context, span),
                    EvaluateModulo(arguments[0], arguments[1], context, span)
                ],
                context.MemoryGovernor,
                span);
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "ZeroDivisionError" &&
            (arguments[0] is double || arguments[1] is double))
        {
            throw new LythonRuntimeException("ZeroDivisionError", "float divmod()", span);
        }
    }

    // Range bounds are inline values; charge one table slot for the object itself.
    private const long RangeValueBytes = 64;

    private static object Range(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        BigInteger start;
        BigInteger stop;
        BigInteger step;

        if (arguments.Length == 1 && TryRangeBound(arguments[0], out var stopOnly))
        {
            start = BigInteger.Zero;
            stop = stopOnly;
            step = BigInteger.One;
        }
        else if (arguments.Length == 2 && TryRangeBound(arguments[0], out var startArg) && TryRangeBound(arguments[1], out var stopArg))
        {
            start = startArg;
            stop = stopArg;
            step = BigInteger.One;
        }
        else if (arguments.Length == 3 && TryRangeBound(arguments[0], out var startValue) && TryRangeBound(arguments[1], out var stopValue) && TryRangeBound(arguments[2], out var stepValue))
        {
            start = startValue;
            stop = stopValue;
            step = stepValue;
        }
        else
        {
            throw new LythonRuntimeException("TypeError", "range(stop), range(start, stop), or range(start, stop, step) expects integer arguments.", span);
        }

        static bool TryRangeBound(object value, out BigInteger bound)
        {
            if (value is BigInteger integer)
            {
                bound = integer;
                return true;
            }

            if (value is bool flag)
            {
                bound = flag ? BigInteger.One : BigInteger.Zero;
                return true;
            }

            bound = default;
            return false;
        }

        if (step == BigInteger.Zero)
        {
            throw new LythonRuntimeException("ValueError", "range() arg 3 must not be zero", span);
        }

        context.MemoryGovernor.Reserve(RangeValueBytes, span);
        context.MemoryGovernor.Commit(RangeValueBytes);
        return new PyRange(start, stop, step);
    }

    private static object Enumerate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "enumerate(iterable[, start]) expects one or two arguments.", span);
        }

        var index = arguments.Length == 2 && arguments[1] is BigInteger start
            ? start
            : arguments.Length == 1
                ? BigInteger.Zero
                : throw new LythonRuntimeException("TypeError", "enumerate(iterable, start) expects an integer start.", span);

        return new PyEnumerateIterator(arguments[0], index, span, context);
    }

    private static object Zip(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var iterables = new List<object>();
        var strict = false;
        var sawStrict = false;
        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                iterables.Add(argument.Value);
                continue;
            }

            if (argument.KeywordName != "strict" || sawStrict)
            {
                throw new LythonRuntimeException("TypeError", $"zip() got an unexpected keyword argument '{argument.KeywordName}'", span);
            }

            sawStrict = true;
            strict = IsTruthy(argument.Value);
        }

        return new PyZipIterator([.. iterables], strict, span, context);
    }

    private static object Zip(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, span);
        if (arguments.Length == 0)
        {
            return result;
        }

        var enumerators = new System.Collections.IEnumerator[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            enumerators[i] = ToSequence(arguments[i], span, context).GetEnumerator();
        }

        try
        {
            while (true)
            {
                var ready = true;
                foreach (var enumerator in enumerators)
                {
                    if (enumerator.MoveNext())
                    {
                        continue;
                    }

                    ready = false;
                    break;
                }

                if (!ready)
                {
                    return result;
                }

                result.Add(CreateTuple(enumerators.Length, i => RuntimeValue(enumerators[i].Current), context, span));
                context.ObserveCollectionCount(result.Count, span);
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }

    private static object Iter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 1)
        {
            if (arguments[0] is IPyIteratorValue iterator)
            {
                return iterator;
            }

            // R08: iter() returns whatever __iter__ produces (often the instance
            // itself), validated eagerly instead of wrapped sight unseen.
            if (arguments[0] is PyInstance instance)
            {
                return new PyUserIterator(instance, context, span).Iterator;
            }

            return new PyEnumerableIterator(arguments[0], span, context);
        }

        if (arguments.Length == 2)
        {
            if (arguments[0] is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "iter(callable, sentinel) expects the first argument to be callable.", span);
            }

            return new PyCallableSentinelIterator(callable, arguments[1], context, span);
        }

        throw new LythonRuntimeException("TypeError", "iter(object[, sentinel]) expects one or two arguments.", span);
    }

    private static ValueTask<object> IterAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        // R08: only user-defined __iter__ resolution can suspend; every other
        // shape shares the synchronous implementation.
        if (arguments.Length == 1 && arguments[0] is PyInstance instance)
        {
            return ResolveIterAsync(instance, span, context);
        }

        return new ValueTask<object>(Iter(arguments, span, context));
    }

    private static async ValueTask<object> ResolveIterAsync(PyInstance instance, LythonSourceSpan span, ExecutionContext context)
        => (await PyUserIterator.CreateAsync(instance, context, span).ConfigureAwait(false)).Iterator;

    private static object Reversed(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "reversed(sequence) expects one argument.", span);
        }

        var target = arguments[0];
        if (PyMemberAccess.TryResolve(target, "__reversed__", context, span, out var reversedMember))
        {
            if (reversedMember is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "__reversed__ must be callable.", span);
            }

            return RuntimeValue(callable.Invoke([], span, context));
        }

        if (target is IPyIndexableValue indexable)
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            return new PyReversedIterator(indexable.Length, indexable.GetIndex);
        }

        if (PyStringOps.TryAsString(target, out var text))
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            return new PyReversedIterator(text.Length, text.Index);
        }

        throw new LythonRuntimeException("TypeError", "reversed(sequence) expects a reversible sequence.", span);
    }

    private static object Map(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length < 2)
        {
            throw new LythonRuntimeException("TypeError", "map(function, iterable, ...) expects a callable and at least one iterable.", span);
        }

        if (arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "map(function, iterable, ...) expects function to be callable.", span);
        }

        var iterables = new object[arguments.Length - 1];
        for (var i = 1; i < arguments.Length; i++)
        {
            _ = PyIteration.Cursor.Create(arguments[i], span, context);
            iterables[i - 1] = arguments[i];
        }

        return new PyMapIterator(callable, iterables, context, span);
    }

    private static object Filter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "filter(function, iterable) expects two arguments.", span);
        }

        var function = arguments[0] switch
        {
            PyNone => null,
            ICallable callable => callable,
            _ => throw new LythonRuntimeException("TypeError", "filter(function, iterable) expects function to be callable or None.", span)
        };

        return new PyFilterIterator(function, arguments[1], context, span);
    }

    private static object Slice(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "slice(stop) or slice(start, stop[, step]) expects one to three arguments.", span);
        }

        // Slice syntax never materializes an object; only explicit calls retain one.
        context.MemoryGovernor.Reserve(64L, span);
        context.MemoryGovernor.Commit(64L);
        return arguments.Length switch
        {
            1 => new PySlice(PyNone.Instance, arguments[0], PyNone.Instance),
            2 => new PySlice(arguments[0], arguments[1], PyNone.Instance),
            _ => new PySlice(arguments[0], arguments[1], arguments[2]),
        };
    }

    private static object Next(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "next(iterator[, default]) expects one or two arguments.", span);
        }

        // R08: direct next() over user iterators resolves __next__ through the
        // same protocol instead of rejecting non-wrapper iterators.
        if (arguments[0] is PyInstance instance)
        {
            if (!PyUserIterator.HasNext(instance, context, span))
            {
                throw new LythonRuntimeException("TypeError", "next() argument must be an iterator", span);
            }

            if (PyUserIterator.TryAdvanceInstance(instance, context, span, out var item))
            {
                return RuntimeValue(item);
            }
        }
        else if (arguments[0] is IPyIteratorValue iterator)
        {
            if (iterator.TryMoveNext(out var value))
            {
                return RuntimeValue(value);
            }
        }
        else
        {
            throw new LythonRuntimeException("TypeError", "next() argument must be an iterator", span);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
    }

    private static async ValueTask<object> NextAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "next(iterator[, default]) expects one or two arguments.", span);
        }

        PyIterationResult advanced;
        if (arguments[0] is PyInstance instance)
        {
            if (!PyUserIterator.HasNext(instance, context, span))
            {
                throw new LythonRuntimeException("TypeError", "next() argument must be an iterator", span);
            }

            advanced = await PyUserIterator.TryAdvanceInstanceAsync(instance, context, span).ConfigureAwait(false);
        }
        else
        {
            advanced = arguments[0] switch
            {
                IPyAsyncIteratorValue asyncIterator => await asyncIterator.TryMoveNextAsync().ConfigureAwait(false),
                IPyIteratorValue iterator => iterator.TryMoveNext(out var item)
                    ? PyIterationResult.Yield(item)
                    : PyIterationResult.End,
                _ => throw new LythonRuntimeException("TypeError", "next() argument must be an iterator", span),
            };
        }

        var (hasValue, value) = advanced;
        if (hasValue)
        {
            return RuntimeValue(value);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "iterator is exhausted", span);
    }

}
