using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class CollectionsModule : PyModule
    {
        public static readonly CollectionsModule Instance = new();

        private CollectionsModule() : base("collections")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "defaultdict" => new BuiltinCallable("collections.defaultdict", DefaultDict, ["default_factory", "iterable"], requiredCount: 0),
                "Counter" => new BuiltinCallable("collections.Counter", Counter, ["iterable"], requiredCount: 0),
                "deque" => new BuiltinCallable("collections.deque", Deque, ["iterable"], requiredCount: 0),
                _ => null!,
            };

            return value is not null;
        }
    }

    private static object DefaultDict(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 2)
        {
            throw new LythonRuntimeException("TypeError", "collections.defaultdict([default_factory][, iterable]) expects zero to two arguments.", span);
        }

        var defaultFactory = arguments.Length >= 1 ? RuntimeValue(arguments[0]) : PyNone.Instance;
        var result = new PyDefaultDict(defaultFactory, context.MemoryGovernor, span);

        if (arguments.Length == 2)
        {
            PopulateDefaultDict(result, arguments[1], span, context);
        }

        return result;
    }

    private static object Counter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "collections.Counter([iterable]) expects zero or one argument.", span);
        }

        var result = new PyCounter(context.MemoryGovernor, span);
        if (arguments.Length == 1)
        {
            PopulateCounter(result, arguments[0], span, context, subtract: false);
        }

        return result;
    }

    private static object Deque(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "collections.deque([iterable]) expects zero or one argument.", span);
        }

        var result = arguments.Length == 0
            ? new PyDeque()
            : new PyDeque(ToSequence(arguments[0], span));
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static void PopulateDefaultDict(PyDefaultDict dict, object source, LythonSourceSpan span, ExecutionContext context)
    {
        if (source is PyDict pyDict)
        {
            foreach (var pair in pyDict)
            {
                dict.SetItem(pair.Key, pair.Value);
                context.ObserveCollectionCount(dict.Count, span);
            }

            return;
        }

        if (source is PyDefaultDict defaultDict)
        {
            foreach (var pair in defaultDict)
            {
                dict.SetItem(pair.Key, pair.Value);
                context.ObserveCollectionCount(dict.Count, span);
            }

            return;
        }

        foreach (var item in ToSequence(source, span))
        {
            if (item is not PyTuple tuple || tuple.Count != 2)
            {
                throw new LythonRuntimeException("TypeError", "collections.defaultdict(..., iterable) expects key/value pairs.", span);
            }

            dict.SetItem(ValidateDictionaryKey(tuple[0], span), RuntimeValue(tuple[1]));
            context.ObserveCollectionCount(dict.Count, span);
        }
    }

    private static void PopulateCounter(PyCounter counter, object source, LythonSourceSpan span, ExecutionContext context, bool subtract)
    {
        if (source is PyCounter otherCounter)
        {
            foreach (var pair in otherCounter)
            {
                var delta = ExpectCounterCount(pair.Value, span);
                counter.Increment(pair.Key, subtract ? -delta : delta);
                context.ObserveCollectionCount(counter.Count, span);
            }

            return;
        }

        if (source is PyDict dict)
        {
            foreach (var pair in dict)
            {
                var delta = ExpectCounterCount(pair.Value, span);
                counter.Increment(pair.Key, subtract ? -delta : delta);
                context.ObserveCollectionCount(counter.Count, span);
            }

            return;
        }

        foreach (var item in ToSequence(source, span))
        {
            counter.Increment(RuntimeValue(item), subtract ? -BigInteger.One : BigInteger.One);
            context.ObserveCollectionCount(counter.Count, span);
        }
    }

    private static BigInteger ExpectCounterCount(object value, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "Counter mapping values must be integers.", span);
        }

        return integer;
    }
}
