using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyTypingAlias : IPySubscriptableValue, IPyRenderableValue, LythonRuntime.ICallable, IPyDynamicAttributes
{
    private readonly object[] _arguments;

    public PyTypingAlias(string shortName) : this(shortName, true, null) { }

    public PyTypingAlias(string shortName, bool qualified) : this(shortName, qualified, null) { }

    public PyTypingAlias(string shortName, bool qualified, IEnumerable<object>? arguments)
    {
        ShortName = shortName;
        Qualified = qualified;
        _arguments = arguments?.ToArray() ?? [];
    }

    public string ShortName { get; }

    public bool Qualified { get; }

    public IReadOnlyList<object> Arguments => _arguments;

    public bool IsSubscripted => _arguments.Length != 0;

    public bool IsInertClassBase
        => ShortName is "Generic" or "Protocol" or "NamedTuple" or "TypedDict";

    public PyTypingAlias OriginAlias => new(ShortName, Qualified);

    public object GetSubscript(object index, LythonSourceSpan span)
    {
        _ = span;
        return new PyTypingAlias(ShortName, Qualified, NormalizeSubscriptArguments(index));
    }

    // Alias short names come from a fixed vocabulary, so cache the single
    // constant instead of minting per read.
    private PyString? _nameValue;

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "__name__" => _nameValue ??= PyString.FromString(ShortName),
            "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("typing"),
            _ => MissingMemberValue.Instance,
        };
        return !ReferenceEquals(value, MissingMemberValue.Instance);
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        return ShortName switch
        {
            "NamedTuple" => PyTyping.NamedTuple(arguments, span, context),
            "TypedDict" => PyTyping.TypedDict(arguments, span, context),
            _ => throw new LythonRuntimeException("TypeError", $"{RenderName()} is not instantiable in Lython.", span)
        };
    }

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ShortName == "NamedTuple"
            ? PyTyping.NamedTupleAsync(arguments, span, context, true)
            : ValueTask.FromResult(Invoke(arguments, span, context));

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString(Render(context));

    public PyString RenderInterpolated(PyRenderingContext context)
        => RenderPython(context);

    private string Render(PyRenderingContext context)
    {
        if (_arguments.Length == 0)
        {
            return RenderName();
        }

        return $"{RenderName()}[{string.Join(", ", _arguments.Select(argument => RenderArgument(argument, context)))}]";
    }

    private string RenderName()
        => Qualified ? $"typing.{ShortName}" : ShortName;

    private static object[] NormalizeSubscriptArguments(object index)
        => index is PyTuple tuple ? tuple.ToArray() : [index];

    private static string RenderArgument(object argument, PyRenderingContext context)
    {
        return argument switch
        {
            PyTypingAlias alias => alias.Render(context),
            PyBuiltinRuntimeType builtinType => builtinType.Name,
            PyType type => type.Name,
            INamedRuntimeCallable callable => callable.Name,
            _ => PyRendering.ToPythonString(argument, context)
        };
    }
}

internal static class PyTyping
{
    private static readonly HashSet<string> AliasNames = new(StringComparer.Ordinal)
    {
        "Any",
        "Optional",
        "Union",
        "List",
        "Dict",
        "Tuple",
        "Set",
        "FrozenSet",
        "Sequence",
        "Iterable",
        "Iterator",
        "Mapping",
        "MutableMapping",
        "Callable",
        "Type",
        "ClassVar",
        "Final",
        "Literal",
        "Annotated",
        "Generic",
        "Protocol",
        "NamedTuple",
        "TypedDict"
    };

    private static readonly IReadOnlyDictionary<string, PyTypingAlias> Aliases = AliasNames
        .ToDictionary(static name => name, static name => new PyTypingAlias(name), StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> BuiltinOrigins = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["List"] = "list",
        ["Dict"] = "dict",
        ["Tuple"] = "tuple",
        ["Set"] = "set",
        ["Type"] = "type",
    };

    public static bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (AliasNames.Contains(name))
        {
            value = Aliases[name];
            return true;
        }

        value = name switch
        {
            "TYPE_CHECKING" => false,
            "TypeVar" => PyTypeParameter.TypeVarType,
            "TypeVarTuple" => PyTypeParameter.TypeVarTupleType,
            "ParamSpec" => PyTypeParameter.ParamSpecType,
            "TypeAliasType" => PyTypeAlias.RuntimeType,
            "NoDefault" => PyTypeParameter.NoDefault,
            "NewType" => NewTypeCallable.Instance,
            "cast" => CastCallable.Instance,
            "get_origin" => GetOriginCallable.Instance,
            "get_args" => GetArgsCallable.Instance,
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }

    // NewType results share one opaque type object like the other runtime
    // types; construction stays unsupported since guests only receive them.
    internal static readonly PyBuiltinRuntimeType NewTypeType = new(
        "typing.NewType",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Runtime type objects cannot be constructed directly in Lython.", span));

    public static object NewType(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        var bound = CallBinder.BindNamedArguments(arguments, span, LythonKnownCallableSignatures.TypingNewType, PythonCallableKind.Function);
        if (!PyStringOps.TryAsString(bound[0], out var name))
        {
            throw new LythonRuntimeException("TypeError", "typing.NewType(name, tp) expects a string name and a base type.", span);
        }

        var created = new NewTypeIdentityCallable(name, bound[1], context, span);
        created.ModuleName = PyFunctionBase.TryGetModuleName(context, out var callerModule)
            ? callerModule
            : PyNone.Instance;
        return created;
    }

    public static object Cast(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        if (!TryGetNamedOrPositional(arguments, 0, "typ", out _) ||
            !TryGetNamedOrPositional(arguments, 1, "val", out var value) ||
            CountEffectiveArguments(arguments) != 2)
        {
            throw new LythonRuntimeException("TypeError", "typing.cast(typ, val) expects two arguments.", span);
        }

        return value;
    }

    public static object GetOrigin(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        if (!TryGetSingleArgument(arguments, "tp", out var value))
        {
            throw new LythonRuntimeException("TypeError", "typing.get_origin(tp) expects one argument.", span);
        }

        if (value is PyGenericAlias generic) return generic.Origin;
        if (value is PyUnpackedType) return new PyTypingAlias("Unpack");
        if (value is PyParamSpecPart part) return part.Parameter;
        if (value is not PyTypingAlias { IsSubscripted: true } alias)
        {
            return PyNone.Instance;
        }

        if (alias.ShortName is "Optional" or "Union")
        {
            return Aliases["Union"];
        }

        if (BuiltinOrigins.TryGetValue(alias.ShortName, out var builtinName) &&
            context.TryGetBuiltin(builtinName, out var builtin))
        {
            return builtin;
        }

        return alias.OriginAlias;
    }

    public static object GetArgs(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        if (!TryGetSingleArgument(arguments, "tp", out var value))
        {
            throw new LythonRuntimeException("TypeError", "typing.get_args(tp) expects one argument.", span);
        }

        if (value is PyGenericAlias generic) return generic.Arguments;
        if (value is PyUnpackedType unpacked) return new PyTuple([unpacked.Value], context.MemoryGovernor, span);
        if (value is not PyTypingAlias alias)
        {
            return PyTuple.Empty;
        }

        return alias.ShortName == "Optional" && alias.IsSubscripted
            ? new PyTuple(alias.Arguments.Concat([PyType.NoneType]))
            : new PyTuple(alias.Arguments);
    }

    public static object NamedTuple(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => NamedTupleAsync(arguments, span, context, false).GetAwaiter().GetResult();

    internal static async ValueTask<object> NamedTupleAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        context.CheckExecution(span);
        object nameValue = PyNone.Instance;
        object fields = PyNone.Instance;
        var positional = 0;
        foreach (var argument in arguments)
        {
            if (!argument.IsPositional) continue;
            switch (positional++)
            {
                case 0: nameValue = argument.Value; break;
                case 1: fields = argument.Value; break;
                default: throw new LythonRuntimeException("TypeError", "typing.NamedTuple expects at most two positional arguments.", span);
            }
        }
        if (positional == 0 || !PyStringOps.TryAsString(nameValue, out var name))
        {
            throw new LythonRuntimeException("TypeError", "typing.NamedTuple(typename, fields=None, ...) expects a string type name.", span);
        }
        if (arguments.Length != positional)
        {
            if (positional == 2)
                throw new LythonRuntimeException("TypeError", "Either list of fields or keywords can be provided to NamedTuple, not both.", span);
            fields = new PyList(arguments.Where(a => a.IsKeyword).Select(a => (object)new PyTuple(
                [PyString.FromString(a.KeywordName, context.MemoryGovernor, span), a.Value], context.MemoryGovernor, span)), context.MemoryGovernor, span);
        }
        else if (fields is PyNone) fields = PyTuple.Empty;

        // Python traverses fields once for names and again for annotations.
        // Keep this order for restartable inputs and exhausted generators.
        using var names = await PyIteration.DrainLeasedAsync(
            NamedTupleFieldNames(fields, null, span, context, asynchronous), span, context).ConfigureAwait(false);
        var annotations = new PyDict(context.MemoryGovernor, span);
        await foreach (var unused in NamedTupleFieldNames(fields, annotations, span, context, asynchronous).ConfigureAwait(false)) { }
        return LythonRuntime.CreateAnnotatedNamedTuple(name, names.Items, annotations, span, context);
    }

    private static async IAsyncEnumerable<object> NamedTupleFieldNames(object fields, PyDict? annotations,
        LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        await using var cursor = asynchronous
            ? await PyIteration.Cursor.CreateAsync(fields, span, context).ConfigureAwait(false)
            : PyIteration.Cursor.Create(fields, span, context);
        while (true)
        {
            var next = asynchronous ? await cursor.TryMoveNextAsync().ConfigureAwait(false)
                : cursor.TryMoveNext(out var item) ? PyIterationResult.Yield(item) : PyIterationResult.End;
            if (!next.HasValue) yield break;
            using var pair = asynchronous
                ? await PyIteration.MaterializeLeasedAsync(next.Value, span, context, 3).ConfigureAwait(false)
                : PyIteration.MaterializeLeased(next.Value, span, context, 3);
            if (pair.Items.Count != 2)
                throw new LythonRuntimeException("ValueError", "typing.NamedTuple fields must be pairs of names and annotations.", span);
            if (!PyStringOps.TryAsString(pair.Items[0], out var name))
                throw new LythonRuntimeException("TypeError", "typing.NamedTuple field names must be strings.", span);
            annotations?.SetItem(name, pair.Items[1]);
            yield return name;
        }
    }

    public static object TypedDict(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        if (!TryGetNamedOrPositional(arguments, 0, "typename", out var nameValue) ||
            !PyStringOps.TryAsString(nameValue, out var name))
        {
            throw new LythonRuntimeException("TypeError", "typing.TypedDict(typename, fields=None, ...) expects a string type name.", span);
        }

        var fields = TryGetNamedOrPositional(arguments, 1, "fields", out var fieldsValue)
            ? ParseFieldNames(fieldsValue)
            : [];
        return new PyTypingConstructedType(name.AsString(), fields);
    }

    private static IReadOnlyList<string> ParseFieldNames(object value)
    {
        if (ReferenceEquals(value, PyNone.Instance))
        {
            return [];
        }

        if (value is PyDict dict)
        {
            return dict
                .Select(pair => PyStringOps.TryAsString(pair.Key, out var name) ? name.AsString() : null)
                .Where(static name => name is not null)
                .Cast<string>()
                .ToArray();
        }

        if (value is IPyIterableValue iterable)
        {
            var names = new List<string>();
            foreach (var item in iterable.Iterate())
            {
                if (PyStringOps.TryAsString(item, out var directName))
                {
                    names.Add(directName.AsString());
                    continue;
                }

                if (item is PyTuple tuple &&
                    tuple.Count > 0 &&
                    PyStringOps.TryAsString(tuple[0], out var tupleName))
                {
                    names.Add(tupleName.AsString());
                }
            }

            return names;
        }

        return [];
    }

    private static bool TryGetSingleArgument(CallArgumentValue[] arguments, string keyword, [MaybeNullWhen(false)] out object value)
    {
        if (CountEffectiveArguments(arguments) != 1)
        {
            value = PyNone.Instance;
            return false;
        }

        return TryGetNamedOrPositional(arguments, 0, keyword, out value);
    }

    private static bool TryGetNamedOrPositional(CallArgumentValue[] arguments, int position, string keyword, [MaybeNullWhen(false)] out object value)
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
                    value = argument.Value;
                    found = true;
                }

                positionalIndex++;
                continue;
            }

            if (argument.KeywordName == keyword)
            {
                value = argument.Value;
                found = true;
            }
        }

        return found;
    }

    private static int CountEffectiveArguments(CallArgumentValue[] arguments)
        => arguments.Length;

    internal static bool TryGetCallableName(string shortName, string memberName, [MaybeNullWhen(false)] out object value)
    {
        value = memberName switch
        {
            "__name__" => PyString.FromString(shortName),
            "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("typing"),
            _ => MissingMemberValue.Instance,
        };
        return !ReferenceEquals(value, MissingMemberValue.Instance);
    }

    private sealed class NewTypeCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable, IPyDynamicAttributes
    {
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => TryGetCallableName("NewType", name, out value);

        public static readonly NewTypeCallable Instance = new();

        public string Name => "typing.NewType";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => NewType(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.NewType>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CastCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable, IPyDynamicAttributes
    {
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => TryGetCallableName("cast", name, out value);

        public static readonly CastCallable Instance = new();

        public string Name => "typing.cast";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => Cast(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.cast>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class GetOriginCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable, IPyDynamicAttributes
    {
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => TryGetCallableName("get_origin", name, out value);

        public static readonly GetOriginCallable Instance = new();

        public string Name => "typing.get_origin";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => GetOrigin(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.get_origin>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class GetArgsCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable, IPyDynamicAttributes
    {
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => TryGetCallableName("get_args", name, out value);

        public static readonly GetArgsCallable Instance = new();

        public string Name => "typing.get_args";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => GetArgs(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.get_args>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class NewTypeIdentityCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable, IPyDynamicAttributes
    {
        private static readonly LythonCallableSignature IdentitySignature = LythonCallableSignature.Create(
            "NewType.__call__", ["obj"], requiredCount: 1, maximumPositionalArgumentCount: 1,
            variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        private readonly PyString _name;
        private readonly PyString _qualifiedName;
        private readonly object _supertype;

        public NewTypeIdentityCallable(PyString name, object supertype, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = 128L + 2L * name.Utf8Bytes.Length;
            using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
            context.CheckExecution(span);
            _qualifiedName = name;
            var separator = name.Utf8Bytes.Span.LastIndexOf((byte)'.');
            _name = separator < 0 ? name : PyString.FromUtf8(name.Utf8Bytes[(separator + 1)..], context.MemoryGovernor, span);
            Name = _name.AsString();
            _supertype = supertype;
            storage.Commit(bytes);
        }

        public string Name { get; }

        internal object? ModuleName { get; set; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = name switch
            {
                "__name__" => _name,
                "__qualname__" => _qualifiedName,
                "__supertype__" => _supertype,
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecution(span);
            return CallBinder.BindNamedArguments(arguments, span, IdentitySignature, PythonCallableKind.Function)[0];
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
            try
            {
                builder.Append(PyRendering.ToInterpolatedPyString(ModuleName ?? PyNone.Instance, context));
                builder.Append((byte)'.');
                builder.Append(_qualifiedName);
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}

internal sealed class PyTypingConstructedType : LythonRuntime.ICallable, IPyRenderableValue, IPyDynamicAttributes, INamedRuntimeCallable
{
    private readonly IReadOnlyList<string> _fieldNames;

    public PyTypingConstructedType(string name, IReadOnlyList<string> fieldNames)
    {
        Name = name;
        _fieldNames = fieldNames;
    }

    public string Name { get; }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        return CreateTypedDict(arguments, span, context);
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "__name__" => PyString.FromString(Name),
            "__annotations__" => BuildAnnotations(),
            _ => PyNone.Instance
        };
        return value is not PyNone;
    }

    private PyDict BuildAnnotations()
    {
        var dict = new PyDict();
        foreach (var fieldName in _fieldNames)
            dict.SetItem(PyString.FromString(fieldName), new PyTypingAlias("Any"));
        return dict;
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"<class '{Name}'>");
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private object CreateTypedDict(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var dict = new PyDict(context.MemoryGovernor, span);
        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                if (argument.Value is PyDict source)
                {
                    foreach (var pair in source)
                    {
                        dict.SetItem(pair.Key, pair.Value);
                    }

                    continue;
                }

                throw new LythonRuntimeException("TypeError", $"{Name}(...) expects keyword arguments or a dict.", span);
            }

            dict.SetItem(PyString.FromString(argument.KeywordName), argument.Value);
        }

        return dict;
    }
}
