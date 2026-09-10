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

        public override IReadOnlyList<string> ExportedNames
            =>
            [
                "defaultdict",
                "Counter",
                "deque",
                "namedtuple",
                "OrderedDict",
                "ChainMap",
                "UserDict",
                "UserList",
                "UserString",
                "abc",
            ];

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "defaultdict" => new CollectionsCallable("collections.defaultdict", DefaultDict),
                "Counter" => new CollectionsCallable("collections.Counter", Counter),
                "deque" => new CollectionsCallable("collections.deque", Deque),
                "namedtuple" => new CollectionsCallable("collections.namedtuple", NamedTuple),
                "OrderedDict" => new CollectionsCallable("collections.OrderedDict", OrderedDict),
                "ChainMap" => new CollectionsCallable("collections.ChainMap", ChainMap),
                "UserDict" => new UnsupportedCollectionsCallable("collections.UserDict"),
                "UserList" => new UnsupportedCollectionsCallable("collections.UserList"),
                "UserString" => new UnsupportedCollectionsCallable("collections.UserString"),
                "abc" => CollectionsAbcModule.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private sealed class CollectionsAbcModule : PyModule
    {
        public static readonly CollectionsAbcModule Instance = new();
        private static readonly string[] Names =
        [
            "Iterable",
            "Iterator",
            "Sequence",
            "MutableSequence",
            "Mapping",
            "MutableMapping",
            "Set",
            "MutableSet",
            "Callable",
        ];

        private CollectionsAbcModule() : base("collections.abc")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Names;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (Names.Contains(name, StringComparer.Ordinal))
            {
                value = new PyTypingAlias(name, qualified: false);
                return true;
            }

            value = PyNone.Instance;
            return false;
        }
    }

    private sealed class CollectionsCallable : ICallable, IPyRenderableValue, INamedRuntimeCallable
    {
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;

        public CollectionsCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation)
        {
            Name = name;
            _implementation = implementation;
        }

        public string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _implementation(arguments, span, context);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class UnsupportedCollectionsCallable : ICallable, IPyRenderableValue, INamedRuntimeCallable
    {
        public UnsupportedCollectionsCallable(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            context.CheckExecutionBudget(span);
            throw new LythonRuntimeException("NotImplementedError", $"{Name} is not supported by Lython.", span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static object DefaultDict(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var positional = new List<object>();
        object defaultFactory = PyNone.Instance;
        object source = PyNone.Instance;
        var hasDefaultFactory = false;
        var hasSource = false;
        var keywordItems = new List<KeyValuePair<string, object>>();

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                positional.Add(argument.Value);
                continue;
            }

            if (argument.KeywordName == "default_factory")
            {
                if (hasDefaultFactory || positional.Count >= 1)
                {
                    throw new LythonRuntimeException("TypeError", "collections.defaultdict(...) got multiple values for argument 'default_factory'.", span);
                }

                defaultFactory = argument.Value;
                hasDefaultFactory = true;
                continue;
            }

            if (argument.KeywordName is "iterable" or "mapping")
            {
                if (hasSource || positional.Count >= 2)
                {
                    throw new LythonRuntimeException("TypeError", "collections.defaultdict(...) got multiple values for mapping.", span);
                }

                source = argument.Value;
                hasSource = true;
                continue;
            }

            keywordItems.Add(new(argument.KeywordName, argument.Value));
        }

        if (positional.Count > 2)
        {
            throw new LythonRuntimeException("TypeError", "collections.defaultdict([default_factory][, iterable], **kwargs) expects at most two positional arguments.", span);
        }

        if (positional.Count >= 1)
        {
            defaultFactory = RuntimeValue(positional[0]);
        }

        if (positional.Count == 2)
        {
            source = positional[1];
            hasSource = true;
        }

        var result = new PyDefaultDict(defaultFactory, context.MemoryGovernor, span);
        if (hasSource)
        {
            PopulateDefaultDict(result, source, span, context);
        }

        foreach (var pair in keywordItems)
        {
            result.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), RuntimeValue(pair.Value));
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
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

    private static object Counter(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object source = PyNone.Instance;
        var hasSource = false;
        var keywordItems = new List<KeyValuePair<string, object>>();
        var positionalCount = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                if (positionalCount >= 1)
                {
                    throw new LythonRuntimeException("TypeError", "collections.Counter([iterable], **kwargs) expects at most one positional argument.", span);
                }

                source = argument.Value;
                hasSource = true;
                positionalCount++;
                continue;
            }

            if (argument.KeywordName is "iterable" or "mapping")
            {
                if (hasSource)
                {
                    throw new LythonRuntimeException("TypeError", "collections.Counter(...) got multiple values for iterable.", span);
                }

                source = argument.Value;
                hasSource = true;
                continue;
            }

            keywordItems.Add(new(argument.KeywordName, argument.Value));
        }

        var result = new PyCounter(context.MemoryGovernor, span);
        if (hasSource)
        {
            PopulateCounter(result, source, span, context, subtract: false);
        }

        PopulateCounterKeywords(result, keywordItems, span, context, subtract: false);
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

    private static object Deque(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object iterable = PyNone.Instance;
        var hasIterable = false;
        int? maxLength = null;
        var hasMaxLength = false;
        var positionalCount = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                if (positionalCount == 0)
                {
                    iterable = argument.Value;
                    hasIterable = true;
                }
                else if (positionalCount == 1)
                {
                    maxLength = ExpectDequeMaxLength(argument.Value, span);
                    hasMaxLength = true;
                }
                else
                {
                    throw new LythonRuntimeException("TypeError", "collections.deque([iterable][, maxlen]) expects at most two positional arguments.", span);
                }

                positionalCount++;
                continue;
            }

            if (argument.KeywordName == "iterable")
            {
                if (hasIterable)
                {
                    throw new LythonRuntimeException("TypeError", "collections.deque(...) got multiple values for argument 'iterable'.", span);
                }

                iterable = argument.Value;
                hasIterable = true;
                continue;
            }

            if (argument.KeywordName == "maxlen")
            {
                if (hasMaxLength)
                {
                    throw new LythonRuntimeException("TypeError", "collections.deque(...) got multiple values for argument 'maxlen'.", span);
                }

                maxLength = ExpectDequeMaxLength(argument.Value, span);
                hasMaxLength = true;
                continue;
            }

            throw new LythonRuntimeException("TypeError", $"collections.deque(...) received an unexpected keyword argument '{argument.KeywordName}'.", span);
        }

        var result = hasIterable
            ? new PyDeque(ToSequence(iterable, span, context), maxLength, context.MemoryGovernor, span)
            : new PyDeque(maxLength, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object Deque(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "collections.deque([iterable]) expects zero or one argument.", span);
        }

        var result = arguments.Length == 0
            ? new PyDeque(null, context.MemoryGovernor, span)
            : new PyDeque(ToSequence(arguments[0], span, context), null, context.MemoryGovernor, span);
        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object NamedTuple(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Count(static argument => argument.IsPositional) > 2)
        {
            throw new LythonRuntimeException("TypeError", "collections.namedtuple(typename, field_names, *, rename=False, defaults=None, module=None) accepts only typename and field_names positionally.", span);
        }

        if (!TryGetArgument(arguments, 0, "typename", span, out var typeNameValue) ||
            !PyStringOps.TryAsString(typeNameValue, out var typeName))
        {
            throw new LythonRuntimeException("TypeError", "collections.namedtuple(typename, field_names, ...) expects a string type name.", span);
        }

        if (!TryGetArgument(arguments, 1, "field_names", span, out var fieldNamesValue))
        {
            throw new LythonRuntimeException("TypeError", "collections.namedtuple(typename, field_names, ...) missing field_names.", span);
        }

        var fieldNames = ParseNamedTupleFieldNames(fieldNamesValue, span, context);
        var rename = TryGetArgument(arguments, 2, "rename", span, out var renameValue) && IsTruthy(renameValue);
        // Drain at most one past the field count: longer inputs fail with the
        // same TypeError below without a proportional transient. Parsing the
        // names first also matches CPython left-to-right validation.
        object[] defaults = [];
        if (TryGetArgument(arguments, 3, "defaults", span, out var defaultsValue) && defaultsValue is not PyNone)
        {
            var collected = new List<object>();
            foreach (var item in ToSequence(defaultsValue, span, context))
            {
                collected.Add(item);
                if (collected.Count > fieldNames.Count)
                {
                    throw new LythonRuntimeException("TypeError", "collections.namedtuple(..., defaults=...) has more defaults than fields.", span);
                }
            }

            defaults = collected.ToArray();
        }

        if (defaults.Length > 0 && defaults.Length > fieldNames.Count)
        {
            throw new LythonRuntimeException("TypeError", "collections.namedtuple(..., defaults=...) has more defaults than fields.", span);
        }

        foreach (var argument in arguments)
        {
            if (argument.IsKeyword &&
                argument.KeywordName is not ("typename" or "field_names" or "rename" or "defaults" or "module"))
            {
                throw new LythonRuntimeException("TypeError", $"collections.namedtuple(...) received an unexpected keyword argument '{argument.KeywordName}'.", span);
            }
        }

        var fields = NormalizeNamedTupleFields(fieldNames, rename, span);
        if (defaults.Length > fields.Count)
        {
            throw new LythonRuntimeException("TypeError", "collections.namedtuple(..., defaults=...) has more defaults than fields.", span);
        }

        return new PyNamedTupleType(typeName.AsString(), fields, defaults);
    }

    private static object OrderedDict(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        object source = PyNone.Instance;
        var hasSource = false;
        var positionalCount = 0;
        var keywordItems = new List<KeyValuePair<string, object>>();

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                if (positionalCount >= 1)
                {
                    throw new LythonRuntimeException("TypeError", "collections.OrderedDict([mapping], **kwargs) expects at most one positional argument.", span);
                }

                source = argument.Value;
                hasSource = true;
                positionalCount++;
                continue;
            }

            if (argument.KeywordName is "mapping" or "iterable")
            {
                if (hasSource)
                {
                    throw new LythonRuntimeException("TypeError", "collections.OrderedDict(...) got multiple values for mapping.", span);
                }

                source = argument.Value;
                hasSource = true;
                continue;
            }

            keywordItems.Add(new(argument.KeywordName, argument.Value));
        }

        var dict = new PyDict(context.MemoryGovernor, span);
        if (hasSource)
        {
            PopulateDict(dict, source, "collections.OrderedDict([mapping], **kwargs)", span, context);
        }

        foreach (var pair in keywordItems)
        {
            dict.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), RuntimeValue(pair.Value));
            context.ObserveCollectionCount(dict.Count, span);
        }

        return dict;
    }

    private static object ChainMap(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var maps = new List<object>();
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "collections.ChainMap(*maps) does not accept keyword arguments.", span);
            }

            maps.Add(argument.Value);
        }

        return maps.Count == 0
            ? new PyChainMap([new PyDict(context.MemoryGovernor, span)])
            : new PyChainMap(PyChainMap.NormalizeMaps(maps, span, context.MemoryGovernor));
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

        foreach (var item in ToSequence(source, span, context))
        {
            if (item is not PyTuple tuple || tuple.Count != 2)
            {
                throw new LythonRuntimeException("TypeError", "collections.defaultdict(..., iterable) expects key/value pairs.", span);
            }

            dict.SetItem(ValidateDictionaryKey(tuple[0], span), RuntimeValue(tuple[1]));
            context.ObserveCollectionCount(dict.Count, span);
        }
    }

    private static void PopulateDict(PyDict dict, object source, string signature, LythonSourceSpan span, ExecutionContext context)
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

        foreach (var item in ToSequence(source, span, context))
        {
            if (item is not PyTuple tuple || tuple.Count != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects key/value pairs.", span);
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
                counter.Increment(pair.Key, subtract ? NegateCounterCount(delta, span, counter.OwnerMemoryGovernor) : delta, span);
                context.ObserveCollectionCount(counter.Count, span);
            }

            return;
        }

        if (source is PyDict dict)
        {
            foreach (var pair in dict)
            {
                var delta = ExpectCounterCount(pair.Value, span);
                counter.Increment(pair.Key, subtract ? NegateCounterCount(delta, span, counter.OwnerMemoryGovernor) : delta, span);
                context.ObserveCollectionCount(counter.Count, span);
            }

            return;
        }

        foreach (var item in ToSequence(source, span, context))
        {
            counter.Increment(RuntimeValue(item), subtract ? -BigInteger.One : BigInteger.One, span);
            context.ObserveCollectionCount(counter.Count, span);
        }
    }

    private static void PopulateCounterKeywords(
        PyCounter counter,
        IEnumerable<KeyValuePair<string, object>> keywordItems,
        LythonSourceSpan span,
        ExecutionContext context,
        bool subtract)
    {
        foreach (var pair in keywordItems)
        {
            var delta = ExpectCounterCount(pair.Value, span);
            counter.Increment(PyString.FromString(pair.Key, context.MemoryGovernor, span), subtract ? NegateCounterCount(delta, span, counter.OwnerMemoryGovernor) : delta, span);
            context.ObserveCollectionCount(counter.Count, span);
        }
    }

    private static object ExpectCounterCount(object value, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsNumber(value, out _) && value is not PyDecimal)
        {
            throw new LythonRuntimeException("TypeError", "Counter mapping values must be numeric.", span);
        }

        return value;
    }

    private static int? ExpectDequeMaxLength(object value, LythonSourceSpan span)
    {
        if (value is PyNone)
        {
            return null;
        }

        var integer = ExpectInteger(value, "collections.deque(..., maxlen=...) expects an integer or None.", span);
        if (integer < 0)
        {
            throw new LythonRuntimeException("ValueError", "maxlen must be non-negative.", span);
        }

        if (integer > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "deque maxlen is too large.", span);
        }

        return (int)integer;
    }

    private static bool TryGetArgument(CallArgumentValue[] arguments, int position, string keyword, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        value = PyNone.Instance;
        var positionalIndex = 0;
        var found = false;
        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                if (positionalIndex == position)
                {
                    if (found)
                    {
                        throw new LythonRuntimeException("TypeError", $"collections.namedtuple(...) got multiple values for argument '{keyword}'.", span);
                    }

                    value = argument.Value;
                    found = true;
                }

                positionalIndex++;
                continue;
            }

            if (argument.KeywordName == keyword)
            {
                if (found)
                {
                    throw new LythonRuntimeException("TypeError", $"collections.namedtuple(...) got multiple values for argument '{keyword}'.", span);
                }

                value = argument.Value;
                found = true;
            }
        }

        return found;
    }

    private static IReadOnlyList<string> ParseNamedTupleFieldNames(object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (PyStringOps.TryAsString(value, out var text))
        {
            return text.AsString()
                .Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
        }

        var names = new List<string>();
        foreach (var item in ToSequence(value, span, context))
        {
            if (!PyStringOps.TryAsString(item, out var name))
            {
                throw new LythonRuntimeException("TypeError", "collections.namedtuple(..., field_names) expects strings.", span);
            }

            names.Add(name.AsString());
        }

        return names;
    }

    private static IReadOnlyList<string> NormalizeNamedTupleFields(IReadOnlyList<string> fields, bool rename, LythonSourceSpan span)
    {
        var normalized = new List<string>(fields.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (!IsValidIdentifier(field) ||
                IsPythonKeyword(field) ||
                field.StartsWith('_') ||
                !seen.Add(field))
            {
                if (!rename)
                {
                    throw new LythonRuntimeException("ValueError", $"Encountered invalid or duplicate field name '{field}'.", span);
                }

                field = $"_{i}";
                while (seen.Contains(field))
                {
                    field = $"_{i}_{seen.Count}";
                }

                seen.Add(field);
            }

            normalized.Add(field);
        }

        return normalized;
    }

    private static bool IsValidIdentifier(string value)
    {
        if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPythonKeyword(string value)
        => value is
            "False" or "None" or "True" or "and" or "as" or "assert" or "async" or "await" or
            "break" or "class" or "continue" or "def" or "del" or "elif" or "else" or "except" or
            "finally" or "for" or "from" or "global" or "if" or "import" or "in" or "is" or
            "lambda" or "nonlocal" or "not" or "or" or "pass" or "raise" or "return" or "try" or
            "while" or "with" or "yield";
}
