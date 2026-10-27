using System.Numerics;
using Lokad.Lython.Frontend;
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
                "defaultdict" => new CollectionsCallable("collections.defaultdict", DefaultDict, DefaultDictAsync),
                "Counter" => new CollectionsCallable("collections.Counter", Counter, CounterAsync),
                "deque" => new CollectionsCallable("collections.deque", Deque, DequeAsync),
                "namedtuple" => new CollectionsCallable("collections.namedtuple", NamedTuple, NamedTupleAsync),
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

    internal sealed class CollectionsCallable : ICallable, IPyRenderableValue, INamedRuntimeCallable, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

        internal LythonRuntime.TypeNewMethod? NewSlot { get; set; }

        public CollectionsCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation)
            : this(name, implementation, null)
        {
        }

        public CollectionsCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
        {
            Name = name;
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
            _shortName = PyString.FromString(name.Substring(name.LastIndexOf((char)46) + 1));
        }

        public string Name { get; }

        // Short type names are fixed per factory object, so reads alias
        // stably like CPython instead of rebuilding per read.
        private readonly PyString _shortName;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__" || name == "__qualname__")
            {
                value = _shortName;
                return true;
            }

            if (name == "__module__")
            {
                value = LythonRuntime.ExceptionTypeValue.SharedModuleLabel("collections");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TryGetMember(string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            // Collection types expose CPython-style __bases__ (dict for the
            // dict-backed kinds, object otherwise since ABCs are absent)
            // and an __mro__ running from the factory through object.
            if ((memberName == "__bases__" || memberName == "__mro__") && IsCollectionType(Name) &&
                GetFactoryBaseObjects(Name, context) is { } bases &&
                context.TryGetBuiltin("object", out var objectBase) && objectBase is not null)
            {
                if (memberName == "__bases__")
                {
                    value = new PyTuple(bases, context.MemoryGovernor, span);
                    return true;
                }

                // __mro__ runs from the factory itself and terminates at
                // object like CPython.
                var mroLength = bases.Length + 1;
                if (!ReferenceEquals(bases[bases.Length - 1], objectBase))
                {
                    mroLength++;
                }

                var mro = new object[mroLength];
                mro[0] = this;
                Array.Copy(bases, 0, mro, 1, bases.Length);
                if (mroLength > bases.Length + 1)
                {
                    mro[mroLength - 1] = objectBase;
                }

                value = new PyTuple(mro, context.MemoryGovernor, span);
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        private static object[]? GetFactoryBaseObjects(string name, LythonRuntime.ExecutionContext context)
        {
            if (!context.TryGetBuiltin("object", out var obj) || obj is null)
            {
                return null;
            }

            if (name is "collections.defaultdict" or "collections.Counter" or "collections.OrderedDict")
            {
                if (!context.TryGetBuiltin("dict", out var dict) || dict is null)
                {
                    return null;
                }

                return [dict];
            }

            return [obj];
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            return _implementation(arguments, span, context);
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            return _asyncImplementation is null
                ? ValueTask.FromResult(_implementation(arguments, span, context))
                : _asyncImplementation(arguments, span, context);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            // Collection types render like CPython type objects; the namedtuple
            // factory renders like the other engine builtins (CPython reports a
            // function with an address there).
            return PyString.FromString(IsCollectionType(Name) ? $"<class '{Name}'>" : Name is "collections.namedtuple" ? "<built-in function namedtuple>" : Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private static bool IsCollectionType(string name)
            => name is "collections.defaultdict" or "collections.Counter" or "collections.deque" or "collections.OrderedDict" or "collections.ChainMap";
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
            context.CheckExecution(span);
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
            var keyword = PyString.FromString(pair.Key, context.MemoryGovernor, span);
            result.SetItem(keyword, RuntimeValue(pair.Value));
            context.CheckExecution(span);
        }

        return result;
    }

    private static async ValueTask<object> DefaultDictAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        // Async twin of the constructor above: only the source drain awaits;
        // shaping, defaults and keyword handling stay identical.
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
            await PopulateDefaultDictAsync(result, source, span, context).ConfigureAwait(false);
        }

        foreach (var pair in keywordItems)
        {
            var keyword = PyString.FromString(pair.Key, context.MemoryGovernor, span);
            result.SetItem(keyword, RuntimeValue(pair.Value));
            context.CheckExecution(span);
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

    private static async ValueTask<object> CounterAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        // Async twin of the constructor above: argument shaping stays
        // identical; only the source drain awaits each pull.
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
            await PopulateCounterAsync(result, source, span, context, subtract: false).ConfigureAwait(false);
        }

        await PopulateCounterKeywordsAsync(result, keywordItems, span, context, subtract: false).ConfigureAwait(false);
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
                    maxLength = ExpectDequeMaxLength(argument.Value, span, context);
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

                maxLength = ExpectDequeMaxLength(argument.Value, span, context);
                hasMaxLength = true;
                continue;
            }

            throw new LythonRuntimeException("TypeError", $"collections.deque(...) received an unexpected keyword argument '{argument.KeywordName}'.", span);
        }

        var result = hasIterable
            ? new PyDeque(ToSequence(iterable, span, context), maxLength, context.MemoryGovernor, span)
            : new PyDeque(maxLength, context.MemoryGovernor, span);
        context.CheckExecution(span);
        return result;
    }

    private static async ValueTask<object> DequeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        // Async twin of the constructor above: only the iterable drain
        // awaits; shaping and maxlen handling stay identical.
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
                    maxLength = ExpectDequeMaxLength(argument.Value, span, context);
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

                maxLength = ExpectDequeMaxLength(argument.Value, span, context);
                hasMaxLength = true;
                continue;
            }

            throw new LythonRuntimeException("TypeError", $"collections.deque(...) received an unexpected keyword argument '{argument.KeywordName}'.", span);
        }

        using var items = hasIterable
            ? await PyIteration.MaterializeLeasedAsync(iterable, span, context).ConfigureAwait(false)
            : null;
        var result = items is not null
            ? new PyDeque(items.Items, maxLength, context.MemoryGovernor, span)
            : new PyDeque(maxLength, context.MemoryGovernor, span);
        context.CheckExecution(span);
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
        context.CheckExecution(span);
        return result;
    }

    // Pure call-shape facts shared by both namedtuple twins so arity, naming
    // and validation order cannot drift between sync and async composition.
    private sealed record NamedTupleCallShape(string TypeName, object FieldNamesValue);

    private static NamedTupleCallShape GetNamedTupleCallShape(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        foreach (var argument in arguments)
            if (argument.IsKeyword && argument.KeywordName is not ("typename" or "field_names" or "rename" or "defaults" or "module"))
                throw new LythonRuntimeException("TypeError", $"collections.namedtuple(...) received an unexpected keyword argument '{argument.KeywordName}'.", span);

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

        return new NamedTupleCallShape(typeName.AsString(), fieldNamesValue);
    }

    private static object NamedTuple(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var shape = GetNamedTupleCallShape(arguments, span, context);
        var names = ParseNamedTupleFieldNames(shape.FieldNamesValue, span, context);
        var fields = ValidateNamedTupleFields(shape, names, arguments, span);
        using var defaults = TryGetArgument(arguments, 3, "defaults", span, out var value) && value is not PyNone
            ? PyIteration.DrainLeased(RetainNamedTupleDefaults(ToSequence(value, span, context), fields.Count, span), span, context)
            : null;
        return BuildNamedTupleType(shape.TypeName, fields, defaults?.Items, arguments, span, context);
    }

    private static async ValueTask<object> NamedTupleAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var shape = GetNamedTupleCallShape(arguments, span, context);
        var names = await ParseNamedTupleFieldNamesAsync(shape.FieldNamesValue, span, context).ConfigureAwait(false);
        var fields = ValidateNamedTupleFields(shape, names, arguments, span);
        using var defaults = TryGetArgument(arguments, 3, "defaults", span, out var value) && value is not PyNone
            ? await PyIteration.DrainLeasedAsync(RetainNamedTupleDefaultsAsync(ToSequenceAsync(value, span, context), fields.Count, span), span, context).ConfigureAwait(false)
            : null;
        return BuildNamedTupleType(shape.TypeName, fields, defaults?.Items, arguments, span, context);
    }

    private static IReadOnlyList<string> ValidateNamedTupleFields(NamedTupleCallShape shape, IReadOnlyList<string> names,
        CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        if (!IsValidIdentifier(shape.TypeName) || IsPythonKeyword(shape.TypeName))
            throw new LythonRuntimeException("ValueError", "Type names must be valid identifiers and cannot be keywords.", span);
        var rename = TryGetArgument(arguments, 2, "rename", span, out var value) && IsTruthy(value);
        return NormalizeNamedTupleFields(names, rename, span);
    }

    // Keep only defaults that could be used. Consume the entire iterator before
    // reporting excess, preserving tail effects/errors without retaining its tail. The source
    // is already cancellation-checked by ToSequence/ToSequenceAsync.
    private static IEnumerable<object> RetainNamedTupleDefaults(IEnumerable<object> source, int fields,
        LythonSourceSpan span)
    {
        var kept = 0;
        var excess = false;
        foreach (var item in source)
        {
            if (kept < fields) { kept++; yield return item; }
            else excess = true;
        }
        if (excess) throw NamedTupleDefaultsError(span);
    }

    private static async IAsyncEnumerable<object> RetainNamedTupleDefaultsAsync(IAsyncEnumerable<object> source, int fields,
        LythonSourceSpan span)
    {
        var kept = 0;
        var excess = false;
        await foreach (var item in source.ConfigureAwait(false))
        {
            if (kept < fields) { kept++; yield return item; }
            else excess = true;
        }
        if (excess) throw NamedTupleDefaultsError(span);
    }

    private static LythonRuntimeException NamedTupleDefaultsError(LythonSourceSpan span)
        => new("TypeError", "collections.namedtuple(..., defaults=...) has more defaults than fields.", span);

    private static object BuildNamedTupleType(string name, IReadOnlyList<string> fields, IReadOnlyList<object>? defaults,
        CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        // Like CPython, an explicit module value is stored as-is while a
        // missing or None module resolves to the caller module by aliasing
        // its frame string, so reads cost no retained storage.
        object? moduleName = null;
        if (TryGetArgument(arguments, 4, "module", span, out var moduleValue) && moduleValue is not PyNone)
        {
            moduleName = moduleValue;
        }
        else if (PyFunctionBase.TryGetModuleName(context, out var callerModule))
        {
            moduleName = callerModule;
        }
        else
        {
            moduleName = PyNone.Instance;
        }

        var created = new PyNamedTupleType(name, fields, defaults, context.MemoryGovernor, span);
        created.ModuleName = moduleName;
        return created;
    }

    internal static PyNamedTupleType CreateAnnotatedNamedTuple(PyString name, IReadOnlyList<object> names,
        PyDict annotations, LythonSourceSpan span, ExecutionContext context)
    {
        // Fund retained CLR names and the temporary validation inventories before
        // decoding or copying. The type constructor funds its own final backing.
        var retainedBytes = 24L + 2L * name.Utf8Bytes.Length;
        foreach (PyString field in names)
        {
            context.CheckExecution(span);
            retainedBytes = checked(retainedBytes + 24L + 2L * field.Utf8Bytes.Length);
        }
        using var storage = context.MemoryGovernor.ReserveTemporary(checked(retainedBytes + 128L * names.Count + 32), span);
        context.CheckExecution(span);
        var typeName = name.AsString();
        if (!IsValidIdentifier(typeName) || IsPythonKeyword(typeName))
            throw new LythonRuntimeException("ValueError", "Type names must be valid identifiers and cannot be keywords.", span);
        var fields = new string[names.Count];
        for (var i = 0; i < fields.Length; i++) fields[i] = ((PyString)names[i]).AsString();
        var normalized = NormalizeNamedTupleFields(fields, false, span);
        var created = (PyNamedTupleType)BuildNamedTupleType(typeName, normalized, null, [], span, context);
        created.Annotations = annotations;
        storage.Commit(retainedBytes);
        return created;
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
            context.CheckExecution(span);
        }

        return dict;
    }

    private static object ChainMap(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
                throw new LythonRuntimeException("TypeError", "collections.ChainMap(*maps) does not accept keyword arguments.", span);
        }
        return new PyChainMap(arguments.Select(argument => PyChainMap.ExpectMap(argument.Value, span)), context, span);
    }

    private static void PopulateDefaultDict(PyDefaultDict dict, object source, LythonSourceSpan span, ExecutionContext context)
    {
        if (source is PyDict pyDict)
        {
            foreach (var pair in pyDict)
            {
                dict.SetItem(pair.Key, pair.Value);
                context.CheckExecution(span);
            }

            return;
        }

        if (source is PyDefaultDict defaultDict)
        {
            foreach (var pair in defaultDict)
            {
                dict.SetItem(pair.Key, pair.Value);
                context.CheckExecution(span);
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
            context.CheckExecution(span);
        }
    }

    private static async ValueTask PopulateDefaultDictAsync(PyDefaultDict dict, object source, LythonSourceSpan span, ExecutionContext context)
    {
        // Async twin of the fallback drain above: mapping fast paths stay
        // synchronous and pure while arbitrary pair streams await each pull.
        if (source is PyDict pyDict)
        {
            foreach (var pair in pyDict)
            {
                dict.SetItem(pair.Key, pair.Value);
                context.CheckExecution(span);
            }

            return;
        }

        if (source is PyDefaultDict defaultDict)
        {
            foreach (var pair in defaultDict)
            {
                dict.SetItem(pair.Key, pair.Value);
                context.CheckExecution(span);
            }

            return;
        }

        await foreach (var item in ToSequenceAsync(source, span, context).ConfigureAwait(false))
        {
            if (item is not PyTuple tuple || tuple.Count != 2)
            {
                throw new LythonRuntimeException("TypeError", "collections.defaultdict(..., iterable) expects key/value pairs.", span);
            }

            dict.SetItem(ValidateDictionaryKey(tuple[0], span), RuntimeValue(tuple[1]));
            context.CheckExecution(span);
        }
    }

    private static void PopulateDict(PyDict dict, object source, string signature, LythonSourceSpan span, ExecutionContext context)
    {
        if (source is PyDict pyDict)
        {
            foreach (var pair in pyDict)
            {
                dict.SetItem(pair.Key, pair.Value);
                context.CheckExecution(span);
            }

            return;
        }

        if (source is PyDefaultDict defaultDict)
        {
            foreach (var pair in defaultDict)
            {
                dict.SetItem(pair.Key, pair.Value);
                context.CheckExecution(span);
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
            context.CheckExecution(span);
        }
    }

    private static void PopulateCounter(PyCounter counter, object source, LythonSourceSpan span, ExecutionContext context, bool subtract)
        => PopulateCounterCoreAsync(counter, source, span, context, subtract, false).GetAwaiter().GetResult();

    private static ValueTask PopulateCounterAsync(PyCounter counter, object source, LythonSourceSpan span, ExecutionContext context, bool subtract)
        => PopulateCounterCoreAsync(counter, source, span, context, subtract, true);

    private static async ValueTask PopulateCounterCoreAsync(PyCounter counter, object source,
        LythonSourceSpan span, ExecutionContext context, bool subtract, bool asynchronous)
    {
        if (source is PyNone) return;
        if (source is PyDict or PyDefaultDict or PyCounter or PyChainMap)
        {
            await PopulateCounterMappingAsync(counter, EnumerateMappingItems(source, context, span),
                span, context, subtract, asynchronous).ConfigureAwait(false);
        }
        else if (asynchronous)
        {
            await foreach (var item in ToSequenceAsync(source, span, context).ConfigureAwait(false))
                await AddItemAsync(item).ConfigureAwait(false);
        }
        else
        {
            foreach (var item in ToSequence(source, span, context)) await AddItemAsync(item).ConfigureAwait(false);
        }

        ValueTask AddItemAsync(object item) => ApplyCounterDeltaAsync(counter, RuntimeValue(item),
            BigInteger.One, subtract, false, span, context, asynchronous);
    }

    private static void PopulateCounterKeywords(PyCounter counter, IEnumerable<KeyValuePair<string, object>> keywordItems,
        LythonSourceSpan span, ExecutionContext context, bool subtract)
        => PopulateCounterKeywordsAsync(counter, keywordItems, span, context, subtract, false).GetAwaiter().GetResult();

    private static ValueTask PopulateCounterKeywordsAsync(PyCounter counter, IEnumerable<KeyValuePair<string, object>> keywordItems,
        LythonSourceSpan span, ExecutionContext context, bool subtract, bool asynchronous = true)
        => PopulateCounterMappingAsync(counter, keywordItems.Select(pair => new KeyValuePair<object, object>(
            PyString.FromString(pair.Key, context.MemoryGovernor, span), RuntimeValue(pair.Value))),
            span, context, subtract, asynchronous);

    private static async ValueTask PopulateCounterMappingAsync(PyCounter counter, IEnumerable<KeyValuePair<object, object>> items,
        LythonSourceSpan span, ExecutionContext context, bool subtract, bool asynchronous)
    {
        // Python chooses copying once per update of an empty Counter. Keyword
        // updates make their own choice after any positional source is consumed.
        var copy = !subtract && counter.Count == 0;
        foreach (var pair in items)
        {
            context.CheckExecution(span);
            if (copy) StoreCounterItem(counter, pair.Key, pair.Value, context, span);
            else await ApplyCounterDeltaAsync(counter, pair.Key, pair.Value, subtract, true,
                span, context, asynchronous).ConfigureAwait(false);
        }
    }

    private static async ValueTask ApplyCounterDeltaAsync(PyCounter counter, object key, object delta, bool subtract,
        bool mapping, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        context.CheckExecution(span);
        var current = counter.TryGetValue(key, out var found, context, span) ? found : BigInteger.Zero;
        var left = mapping && !subtract ? delta : current;
        var right = mapping && !subtract ? current : delta;
        var operation = subtract ? BinaryOperatorSyntax.Subtract : BinaryOperatorSyntax.Add;
        var updated = asynchronous
            ? await EvaluateBinaryOperatorAsync(operation, left, right, context, span).ConfigureAwait(false)
            : EvaluateBinaryOperator(operation, left, right, context, span);
        context.CheckExecution(span);
        StoreCounterItem(counter, key, updated, context, span);
    }

    private static void StoreCounterItem(PyCounter counter, object key, object value, ExecutionContext context, LythonSourceSpan span)
    {
        using var ambient = PyStructuralGuard.PushAmbient(context, span);
        counter.SetItem(ValidateDictionaryKey(key, span), RuntimeValue(value));
    }

    private static object ExpectCounterCount(object value, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsNumber(value, out _) && value is not PyDecimal)
        {
            throw new LythonRuntimeException("TypeError", "Counter mapping values must be numeric.", span);
        }

        return value;
    }

    // deque(maxlen=...) requires an exact integer like CPython (bool and None
    // included, but no index coercion); negatives drop the trailing period,
    // out-of-ssize magnitudes report the ssize_t overflow and int-range
    // overflows keep the historical too-large shape.
    private static int? ExpectDequeMaxLength(object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (value is PyNone)
        {
            return null;
        }

        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "an integer is required", span);
        }

        if (integer > long.MaxValue || integer < long.MinValue)
        {
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
        }

        if (integer < 0)
        {
            throw new LythonRuntimeException("ValueError", "maxlen must be non-negative", span);
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

    private static async ValueTask<IReadOnlyList<string>> ParseNamedTupleFieldNamesAsync(object value, LythonSourceSpan span, ExecutionContext context)
    {
        // Async twin of the parser above: comma/whitespace splitting stays
        // pure while iterable field names await each pull.
        if (PyStringOps.TryAsString(value, out var text))
        {
            return text.AsString()
                .Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
        }

        var names = new List<string>();
        await foreach (var item in ToSequenceAsync(value, span, context).ConfigureAwait(false))
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
