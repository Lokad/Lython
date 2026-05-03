using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class ItertoolsModule : PyModule
    {
        public static readonly ItertoolsModule Instance = new();

        private ItertoolsModule() : base("itertools")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "chain" => ChainFactory.Instance,
                "islice" => new ItertoolsCallable("itertools.islice", Islice),
                "product" => new ItertoolsCallable("itertools.product", Product),
                "zip_longest" => new ItertoolsCallable("itertools.zip_longest", ZipLongest),
                _ => null!,
            };

            return value is not null;
        }
    }

    internal sealed class ChainFactory : ICallable
    {
        public static readonly ChainFactory Instance = new();

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positionalCount = 0;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Name is not null)
                {
                    throw new LythonRuntimeException("TypeError", "itertools.chain(...) does not accept keyword arguments.", span);
                }

                positionalCount++;
            }

            var iterables = new IEnumerable<object>[positionalCount];
            for (var i = 0; i < positionalCount; i++)
            {
                iterables[i] = ToSequence(arguments[i].Value, span);
            }

            return new PyChainIterator(iterables);
        }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "from_iterable" => new ItertoolsCallable("itertools.chain.from_iterable", FromIterable),
                _ => null!,
            };

            return value is not null;
        }

        private static object FromIterable(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var positional = PositionalOnly(arguments, "itertools.chain.from_iterable", span);
            if (positional.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "itertools.chain.from_iterable(iterable) expects one iterable argument.", span);
            }

            return new PyChainIterator(new FromIterableSequences(ToSequence(positional[0], span), span));
        }
    }

    private sealed class ItertoolsCallable : ICallable
    {
        private readonly string _name;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;

        public ItertoolsCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation)
        {
            _name = name;
            _implementation = implementation;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _implementation(arguments, span, context);
        }
    }

    private static object Islice(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var positional = PositionalOnly(arguments, "itertools.islice", span);
        if (positional.Length is < 2 or > 4)
        {
            throw new LythonRuntimeException("TypeError", "itertools.islice(iterable, stop) or itertools.islice(iterable, start, stop[, step]) is required.", span);
        }

        var iterable = ToSequence(positional[0], span);
        long start;
        long stop;
        long step;
        if (positional.Length == 2)
        {
            start = 0;
            stop = ExpectNonNegativeLong(positional[1], "itertools.islice() stop must be a non-negative integer.", span);
            step = 1;
        }
        else
        {
            start = ExpectNonNegativeLong(positional[1], "itertools.islice() start must be a non-negative integer.", span);
            stop = ExpectNonNegativeLong(positional[2], "itertools.islice() stop must be a non-negative integer.", span);
            step = positional.Length == 4
                ? ExpectPositiveLong(positional[3], "itertools.islice() step must be a positive integer.", span)
                : 1;
        }

        return new PyIsliceIterator(iterable, start, stop, step);
    }

    private static object Product(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var positionalCount = 0;
        var repeat = 1;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is null)
            {
                positionalCount++;
                continue;
            }

            if (!string.Equals(argument.Name, "repeat", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "itertools.product(...) only supports the keyword argument repeat=.", span);
            }

            repeat = checked((int)ExpectNonNegativeLong(argument.Value, "itertools.product(..., repeat=...) expects repeat to be a non-negative integer.", span));
        }

        var pools = new object[positionalCount][];
        var positionalIndex = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is not null)
            {
                continue;
            }

            pools[positionalIndex++] = MaterializeSequence(argument.Value, span);
        }

        var repeated = new IReadOnlyList<object>[pools.Length * repeat];
        var repeatedIndex = 0;
        for (var i = 0; i < repeat; i++)
        {
            for (var j = 0; j < pools.Length; j++)
            {
                repeated[repeatedIndex++] = pools[j];
            }
        }

        return new PyProductIterator(repeated, context.MemoryGovernor, span);
    }

    private static object ZipLongest(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var iterables = new IEnumerable<object>[arguments.Length];
        var iterableCount = 0;
        object fillValue = PyNone.Instance;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is null)
            {
                iterables[iterableCount++] = ToSequence(argument.Value, span);
                continue;
            }

            if (!string.Equals(argument.Name, "fillvalue", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "itertools.zip_longest(...) only supports the keyword argument fillvalue=.", span);
            }

            fillValue = argument.Value;
        }

        return new PyZipLongestIterator(iterableCount == iterables.Length ? iterables : iterables[..iterableCount], fillValue, context.MemoryGovernor, span);
    }

    private static object[] PositionalOnly(CallArgumentValue[] arguments, string owner, LythonSourceSpan span)
    {
        var positional = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(...) does not accept keyword arguments in this form.", span);
            }

            positional[i] = arguments[i].Value;
        }

        return positional;
    }

    private static object[] MaterializeSequence(object value, LythonSourceSpan span)
    {
        var sequence = ToSequence(value, span);
        if (sequence is object[] direct)
        {
            var copy = new object[direct.Length];
            Array.Copy(direct, copy, direct.Length);
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = RuntimeValue(copy[i]);
            }

            return copy;
        }

        var list = new List<object>();
        foreach (var item in sequence)
        {
            list.Add(RuntimeValue(item));
        }

        return [.. list];
    }

    private sealed class FromIterableSequences : IEnumerable<IEnumerable<object>>
    {
        private readonly IEnumerable<object> _outer;
        private readonly LythonSourceSpan _span;

        public FromIterableSequences(IEnumerable<object> outer, LythonSourceSpan span)
        {
            _outer = outer;
            _span = span;
        }

        public IEnumerator<IEnumerable<object>> GetEnumerator()
        {
            foreach (var item in _outer)
            {
                yield return ToSequence(item, _span);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static long ExpectNonNegativeLong(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer) || integer < 0 || integer > long.MaxValue)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return (long)integer;
    }

    private static long ExpectPositiveLong(object value, string message, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer) || integer <= 0 || integer > long.MaxValue)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }

        return (long)integer;
    }
}
