using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly Func<object, LythonSourceSpan, ExecutionContext, object> LengthOperation = GetLength;

    private static object Len(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "len() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        return GetLength(arguments[0], span, context);
    }

    private static object GetLength(object value, LythonSourceSpan span, ExecutionContext context)
        => value switch
        {
            PyChainMap chainMap => LenChainMap(chainMap, span, context),
            PyRange range => range.Length,
            IPySizedValue sized => new BigInteger(sized.Length),
            IReadOnlyCollection<object> collection => new BigInteger(collection.Count),
            System.Collections.ICollection collection => new BigInteger(collection.Count),
            PyInstance instance => GetInstanceLength(instance, context, span),
            _ => throw new LythonRuntimeException("TypeError", $"object of type '{RuntimeErrors.OperandTypeName(value)}' has no len()", span)
        };

    private static ValueTask<object> LenAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => arguments.Length == 1 && arguments[0] is PyInstance instance
            ? GetInstanceLengthAsync(instance, context, span)
            : ValueTask.FromResult(Len(arguments, span, context));

    internal static BigInteger LenChainMap(PyChainMap chainMap, LythonSourceSpan span, ExecutionContext context)
        => new(chainMap.CountMergedKeys(context, span));

    private static BigInteger GetInstanceLength(PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        var member = PyAttributeLookup.BindForInstance(instance, GetLengthSlot(instance, span), context, span);
        if (member is not ICallable callable) throw NotCallableError(member, context, span);
        return ValidateInstanceLength(callable.Invoke([], span, context), span);
    }

    private static async ValueTask<object> GetInstanceLengthAsync(PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        var member = await PyAttributeLookup.BindForInstanceAsync(instance, GetLengthSlot(instance, span), context, span).ConfigureAwait(false);
        if (member is not ICallable callable) throw NotCallableError(member, context, span);
        return ValidateInstanceLength(await callable.InvokeAsync([], span, context).ConfigureAwait(false), span);
    }

    private static object GetLengthSlot(PyInstance instance, LythonSourceSpan span)
        => instance.Type.TryLookupInMro("__len__", 0, out var rawMember, out _)
            ? rawMember
            : throw new LythonRuntimeException("TypeError", $"object of type '{instance.Type.Name}' has no len()", span);

    private static BigInteger ValidateInstanceLength(object value, LythonSourceSpan span)
    {
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

    // Explicit __dir__ calls preserve member order like CPython (only the dir
    // builtin sorts); construction is governed exactly like the sorted list.
    private static PyList CreateUnsortedNameList(IEnumerable<string> names, ExecutionContext context, LythonSourceSpan span)
        => new(
            names.Distinct(StringComparer.Ordinal)
                .Select(name => PyString.FromString(name, context.MemoryGovernor, span)),
            context.MemoryGovernor,
            span);

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

        if (context.CurrentExecutableFrame is null)
        {
            foreach (var name in context.ScopeFacts.NonlocalNames)
                if (context.TryReadNonlocalValue(name, out _)) yield return name;
        }
        else
        {
            foreach (var pair in context.CurrentExecutableFrame.EnumerateVisibleLocals())
            {
                yield return pair.Key;
            }
        }
    }

        private static readonly string[] StringDirNames = ["capitalize", "casefold", "center", "count", "encode", "endswith", "expandtabs", "find", "format", "format_map", "index", "isalnum", "isalpha", "isascii", "isdecimal", "isdigit", "isidentifier", "islower", "isnumeric", "isprintable", "isspace", "istitle", "isupper", "join", "ljust", "lower", "lstrip", "maketrans", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rjust", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "translate", "upper", "zfill"];
    private static readonly string[] BytesDirNames = ["capitalize", "center", "count", "decode", "endswith", "expandtabs", "find", "fromhex", "hex", "index", "isalnum", "isalpha", "isascii", "isdigit", "islower", "isspace", "istitle", "isupper", "join", "ljust", "lower", "lstrip", "maketrans", "partition", "removeprefix", "removesuffix", "replace", "rfind", "rindex", "rjust", "rpartition", "rsplit", "rstrip", "split", "splitlines", "startswith", "strip", "swapcase", "title", "translate", "upper", "zfill"];
    private static readonly string[] ListDirNames = ["append", "clear", "copy", "count", "extend", "index", "insert", "pop", "remove", "reverse", "sort"];
    private static readonly string[] TupleDirNames = ["count", "index"];
    private static readonly string[] RangeDirNames = ["count", "index", "start", "step", "stop"];
    private static readonly string[] RangeMethodDirNames = ["count", "index", "start", "step", "stop"];
    private static readonly string[] NamedTupleDirNames = ["_asdict", "_field_defaults", "_fields", "_make", "_replace", "count", "index"];
    private static readonly string[] TypingNamedTupleDirNames = ["_fields", "_replace", "count", "index"];
    private static readonly string[] NamedTupleTypeDirNames = ["_fields", "_field_defaults", "_make", "__new__", "count", "index"];
    private static readonly string[] FloatDirNames = ["as_integer_ratio", "conjugate", "fromhex", "hex", "imag", "is_integer", "real"];
    private static readonly string[] FloatMethodDirNames = ["as_integer_ratio", "conjugate", "fromhex", "hex", "imag", "is_integer", "real"];
    private static readonly string[] IntDirNames = ["as_integer_ratio", "bit_count", "bit_length", "conjugate", "denominator", "from_bytes", "imag", "is_integer", "numerator", "real", "to_bytes"];
    private static readonly string[] IntMethodDirNames = ["as_integer_ratio", "bit_count", "bit_length", "conjugate", "denominator", "from_bytes", "imag", "is_integer", "numerator", "real", "to_bytes"];
    private static readonly string[] DictDirNames = ["clear", "copy", "fromkeys", "get", "items", "keys", "pop", "popitem", "setdefault", "update", "values"];
    private static readonly string[] SetDirNames = ["add", "clear", "copy", "difference", "difference_update", "discard", "intersection", "intersection_update", "isdisjoint", "issubset", "issuperset", "pop", "remove", "symmetric_difference", "symmetric_difference_update", "union", "update"];
    private static readonly string[] DictKeysViewDirNames = ["isdisjoint"];
    private static readonly string[] DictItemsViewDirNames = ["isdisjoint"];

    private static object DivMod(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => DivModCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> DivModAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => DivModCoreAsync(arguments, span, context, true);
    private static async ValueTask<object> DivModCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "divmod(a, b) expects two arguments.", span);
        }

        if (arguments[0] is PyTimedelta leftDelta && arguments[1] is PyTimedelta rightDelta)
        {
            return PyDateTimeOps.DivMod(leftDelta, rightDelta, context, span);
        }

        // User-defined __divmod__ takes precedence like CPython, with reflected
        // fallback; a NotImplemented answer declines like the other protocol cores.
        var protocol = await EvaluateBinaryProtocolAsync(new("__divmod__", "__rdivmod__"),
            arguments[0], arguments[1], context, span,
            asynchronous ? InvokeBinarySpecialMethodAsync : InvokeBinarySpecialMethod).ConfigureAwait(false);
        if (protocol.Kind == SpecialMethodInvocationKind.Invoked) return protocol.Value;

        if (arguments[0] is PyTimedelta || arguments[1] is PyTimedelta)
        {
            throw RuntimeErrors.UnsupportedOperands("divmod()", arguments[0], arguments[1], span);
        }

        try
        {
            if (TryGetNumericOperands(arguments[0], arguments[1], out var leftNumber, out var rightNumber) &&
                (leftNumber.IsFloat || rightNumber.IsFloat))
            {
                var pair = PyNumberOps.FloatDivMod(PyNumberOps.ToDoubleChecked(leftNumber), PyNumberOps.ToDoubleChecked(rightNumber));
                return new PyTuple([pair.Quotient, pair.Remainder], context.MemoryGovernor, span);
            }
            return new PyTuple(
                [
                    EvaluateFloorDivide(arguments[0], arguments[1], context, span, "divmod()"),
                    EvaluateModulo(arguments[0], arguments[1], context, span, "divmod()")
                ],
                context.MemoryGovernor,
                span);
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "ZeroDivisionError" &&
            (arguments[0] is double || arguments[1] is double))
        {
            throw new LythonRuntimeException("ZeroDivisionError", "float divmod()", span);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ZeroDivisionError", "float divmod()", span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    // Range bounds are inline values; charge one table slot for the object itself.
    private const long RangeValueBytes = 64;

    private static long BoundMagnitudeBytes(BigInteger bound)
        => RuntimeMemoryEstimates.GetMagnitudeBitLength(bound) > 64
            ? RuntimeMemoryEstimates.EstimateBigIntegerBytes(bound)
            : 0;

    private static object Range(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length < 1)
        {
            throw new LythonRuntimeException("TypeError", "range expected at least 1 argument, got 0", span);
        }

        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", $"range expected at most 3 arguments, got {arguments.Length}", span);
        }

        BigInteger start = BigInteger.Zero;
        BigInteger stop;
        BigInteger step = BigInteger.One;

        if (arguments.Length == 1)
        {
            stop = RangeBound(arguments[0], context, span);
        }
        else if (arguments.Length == 2)
        {
            start = RangeBound(arguments[0], context, span);
            stop = RangeBound(arguments[1], context, span);
        }
        else
        {
            start = RangeBound(arguments[0], context, span);
            stop = RangeBound(arguments[1], context, span);
            step = RangeBound(arguments[2], context, span);
        }

        // Bounds coerce through __index__ like CPython; failures name the type.
        static BigInteger RangeBound(object value, ExecutionContext context, LythonSourceSpan span)
        {
            var coerced = CoerceIndexProtocol(value, context, span);
            if (!PyNumberOps.TryAsInteger(coerced, out var bound))
            {
                throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
            }

            return bound;
        }

        if (step == BigInteger.Zero)
        {
            throw new LythonRuntimeException("ValueError", "range() arg 3 must not be zero", span);
        }

        var boundPayloadBytes = BoundMagnitudeBytes(start) + BoundMagnitudeBytes(stop) + BoundMagnitudeBytes(step);
        var rangeBytes = checked(RangeValueBytes + boundPayloadBytes);
        context.MemoryGovernor.Reserve(rangeBytes, span);
        context.MemoryGovernor.Commit(rangeBytes);
        var range = new PyRange(start,stop,step,context.MemoryGovernor,span);
        return range;
    }

    private static object Enumerate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "enumerate(iterable[, start]) expects one or two arguments.", span);
        }

        BigInteger index = BigInteger.Zero;
        if (arguments.Length == 2)
        {
            // The start coerces through __index__ like CPython; failures name
            // the type instead of the builtin signature.
            var coerced = CoerceIndexProtocol(arguments[1], context, span);
            if (!PyNumberOps.TryAsInteger(coerced, out var startValue))
            {
                throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(arguments[1], context) + "' object cannot be interpreted as an integer", span);
            }

            index = startValue;
        }

        var enumerateResult = new PyEnumerateIterator(arguments[0], index, span, context);
        return enumerateResult;
    }

    private static object Zip(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positionalCount = 0;
        var strict = false;
        var sawStrict = false;
        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                positionalCount++;
                continue;
            }

            if (argument.KeywordName != "strict" || sawStrict)
            {
                throw new LythonRuntimeException("TypeError", $"zip() got an unexpected keyword argument '{argument.KeywordName}'", span);
            }

            sawStrict = true;
            strict = IsTruthy(argument.Value);
        }

        var zipResult = new PyZipIterator(positionalCount, strict, span, context);
        try
        {
            zipResult.InitializeCursors(arguments, context);
            return zipResult;
        }
        catch { zipResult.DiscardUnpublishedStorage(); throw; }
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
                context.CheckExecution(span);
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
                if (iterator is StringIOObject stream) stream.EnsureOpen(span);
                return iterator;
            }

            // R08: iter() returns whatever __iter__ produces (often the instance
            // itself), validated eagerly instead of wrapped sight unseen.
            if (arguments[0] is PyInstance or IPyProtocolIterator)
            {
                return PyUserIterator.Create(arguments[0], context, span).Iterator;
            }

            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            var iterEnumerableResult = new PyEnumerableIterator(arguments[0], span, context);
            return iterEnumerableResult;
        }

        if (arguments.Length == 2)
        {
            if (arguments[0] is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "iter(v, w): v must be callable", span);
            }

            var iterSentinelResult = new PyCallableSentinelIterator(callable, arguments[1], context, span);
            return iterSentinelResult;
        }

        throw new LythonRuntimeException("TypeError", "iter(object[, sentinel]) expects one or two arguments.", span);
    }

    private static ValueTask<object> IterAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        // R08: only user-defined __iter__ resolution can suspend; every other
        // shape shares the synchronous implementation.
        if (arguments.Length == 1 && arguments[0] is PyInstance or IPyProtocolIterator)
        {
            return ResolveIterAsync(arguments[0], span, context);
        }

        return new ValueTask<object>(Iter(arguments, span, context));
    }

    private static async ValueTask<object> ResolveIterAsync(object instance, LythonSourceSpan span, ExecutionContext context)
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
            var indexableReversedResult = new PyReversedIterator(indexable.Length, indexable.GetIndex);
            return indexableReversedResult;
        }

        if (target is PyRange range)
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            var rangeReversedResult = new PyEnumerableIterator(range.GetSlice(PyNone.Instance, PyNone.Instance, BigInteger.MinusOne, span), span, context, "range_iterator");
            return rangeReversedResult;
        }

        if (PyStringOps.TryAsString(target, out var text))
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            var stringReversedResult = new PyReversedIterator(text.Length, text.Index);
            return stringReversedResult;
        }

        // Dict-backed mappings reverse over governed key snapshots with
        // the shared size-change discipline.
        if (target is PyDict dict)
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            var dictReversedResult = dict.CreateReversedKeysIterator(context.MemoryGovernor, span);
            return dictReversedResult;
        }

        if (target is PyDefaultDict defaultDict)
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            var defaultDictReversedResult = defaultDict.InnerDict.CreateReversedKeysIterator(context.MemoryGovernor, span);
            return defaultDictReversedResult;
        }

        if (target is PyCounter counter)
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            var counterReversedResult = counter.InnerDict.CreateReversedKeysIterator(context.MemoryGovernor, span);
            return counterReversedResult;
        }

        throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.OperandTypeName(target) + "' object is not reversible", span);
    }

    private static object Map(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => MapCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

    private static ValueTask<object> MapAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => MapCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> MapCoreAsync(object[] arguments, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length < 2)
            throw new LythonRuntimeException("TypeError", "map(function, iterable, ...) expects at least one iterable.", span);
        return await PyMapIterator.CreateAsync(arguments, context, span, asynchronous).ConfigureAwait(false);
    }

    private static object Filter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => FilterCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> FilterAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => FilterCoreAsync(arguments, span, context, true);
    private static async ValueTask<object> FilterCoreAsync(object[] arguments, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 2)
            throw new LythonRuntimeException("TypeError", "filter(function, iterable) expects two arguments.", span);
        context = GetGlobalContext(context);
        using var storage = context.MemoryGovernor.ReserveTemporary(288, span);
        var source = asynchronous ? await PyIteration.Cursor.CreateAsync(arguments[1], span, context).ConfigureAwait(false)
            : PyIteration.Cursor.Create(arguments[1], span, context);
        var result = new PyFilterIterator(arguments[0] is PyNone ? null : arguments[0], source, context, span);
        storage.Commit(288);
        return result;
    }

    private static object Slice(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length < 1)
        {
            throw new LythonRuntimeException("TypeError", "slice expected at least 1 argument, got 0", span);
        }

        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", "slice expected at most 3 arguments, got " + arguments.Length, span);
        }

        // Explicit calls retain a slice, as do materialized subscription keys.
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
        if (arguments[0] is PyInstance or IPyProtocolIterator)
        {
            if (!PyUserIterator.HasNext(arguments[0], context, span))
            {
                throw RuntimeErrors.NotAnIterator(arguments[0], span);
            }

            try
            {
                return RuntimeValue(PyUserIterator.InvokeNext(arguments[0], context, span));
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration")
            {
                if (arguments.Length == 2)
                {
                    return arguments[1];
                }

                throw;
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
            throw RuntimeErrors.NotAnIterator(arguments[0], span);
        }

        if (arguments.Length == 2)
        {
            return arguments[1];
        }

        throw new LythonRuntimeException("StopIteration", "", span, null, (arguments[0] as PyGenerator)?.ReturnValue);
    }

    private static async ValueTask<object> NextAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is not 1 and not 2)
        {
            throw new LythonRuntimeException("TypeError", "next(iterator[, default]) expects one or two arguments.", span);
        }

        PyIterationResult advanced;
        if (arguments[0] is PyInstance or IPyProtocolIterator)
        {
            if (!PyUserIterator.HasNext(arguments[0], context, span))
            {
                throw RuntimeErrors.NotAnIterator(arguments[0], span);
            }

            try
            {
                return RuntimeValue(await PyUserIterator.InvokeNextAsync(arguments[0], context, span).ConfigureAwait(false));
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration")
            {
                if (arguments.Length == 2)
                {
                    return arguments[1];
                }

                throw;
            }
        }
        else
        {
            advanced = arguments[0] switch
            {
                IPyAsyncIteratorValue asyncIterator => await asyncIterator.TryMoveNextAsync().ConfigureAwait(false),
                IPyIteratorValue iterator => iterator.TryMoveNext(out var item)
                    ? PyIterationResult.Yield(item)
                    : PyIterationResult.End,
                _ => throw RuntimeErrors.NotAnIterator(arguments[0], span),
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

        throw new LythonRuntimeException("StopIteration", "", span, null, (arguments[0] as PyGenerator)?.ReturnValue);
    }

}
