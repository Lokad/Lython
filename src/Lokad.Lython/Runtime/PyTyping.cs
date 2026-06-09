using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyTypingAlias : IPySubscriptableValue, IPyRenderableValue, LythonRuntime.ICallable
{
    private readonly object[] _arguments;

    public PyTypingAlias(string shortName, bool qualified = true, IEnumerable<object>? arguments = null)
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

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        return ShortName switch
        {
            "NamedTuple" => PyTyping.NamedTuple(arguments, span, context),
            "TypedDict" => PyTyping.TypedDict(arguments, span, context),
            _ => throw new LythonRuntimeException("TypeError", $"{RenderName()} is not instantiable in Lython.", span)
        };
    }

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
    public static readonly IReadOnlyList<string> ExportedNames =
    [
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
        "TYPE_CHECKING",
        "TypeVar",
        "NewType",
        "Generic",
        "Protocol",
        "NamedTuple",
        "TypedDict",
        "cast",
        "get_origin",
        "get_args"
    ];

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

    public static bool TryGetMember(string name, out object value)
    {
        if (AliasNames.Contains(name))
        {
            value = new PyTypingAlias(name);
            return true;
        }

        value = name switch
        {
            "TYPE_CHECKING" => false,
            "TypeVar" => TypeVarCallable.Instance,
            "NewType" => NewTypeCallable.Instance,
            "cast" => CastCallable.Instance,
            "get_origin" => GetOriginCallable.Instance,
            "get_args" => GetArgsCallable.Instance,
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }

    public static object TypeVar(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (!TryGetNamedOrPositional(arguments, 0, "name", out var nameValue) ||
            !PyStringOps.TryAsString(nameValue, out var name))
        {
            throw new LythonRuntimeException("TypeError", "typing.TypeVar(name, ...) expects a string name.", span);
        }

        return new PyTypingAlias(name.AsString(), qualified: false);
    }

    public static object NewType(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (!TryGetNamedOrPositional(arguments, 0, "name", out var nameValue) ||
            !PyStringOps.TryAsString(nameValue, out var name) ||
            !TryGetNamedOrPositional(arguments, 1, "tp", out _))
        {
            throw new LythonRuntimeException("TypeError", "typing.NewType(name, tp) expects a string name and a base type.", span);
        }

        return new NewTypeIdentityCallable(name.AsString());
    }

    public static object Cast(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
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
        context.CheckExecutionBudget(span);
        if (!TryGetSingleArgument(arguments, "tp", out var value))
        {
            throw new LythonRuntimeException("TypeError", "typing.get_origin(tp) expects one argument.", span);
        }

        return value is PyTypingAlias { IsSubscripted: true } alias
            ? alias.OriginAlias
            : PyNone.Instance;
    }

    public static object GetArgs(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (!TryGetSingleArgument(arguments, "tp", out var value))
        {
            throw new LythonRuntimeException("TypeError", "typing.get_args(tp) expects one argument.", span);
        }

        return value is PyTypingAlias alias
            ? new PyTuple(alias.Arguments)
            : PyTuple.Empty;
    }

    public static object NamedTuple(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (!TryGetNamedOrPositional(arguments, 0, "typename", out var nameValue) ||
            !PyStringOps.TryAsString(nameValue, out var name))
        {
            throw new LythonRuntimeException("TypeError", "typing.NamedTuple(typename, fields=None, ...) expects a string type name.", span);
        }

        var fields = TryGetNamedOrPositional(arguments, 1, "fields", out var fieldsValue)
            ? ParseFieldNames(fieldsValue)
            : [];
        return new PyTypingConstructedType(name.AsString(), PyTypingConstructedKind.NamedTuple, fields);
    }

    public static object TypedDict(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (!TryGetNamedOrPositional(arguments, 0, "typename", out var nameValue) ||
            !PyStringOps.TryAsString(nameValue, out var name))
        {
            throw new LythonRuntimeException("TypeError", "typing.TypedDict(typename, fields=None, ...) expects a string type name.", span);
        }

        var fields = TryGetNamedOrPositional(arguments, 1, "fields", out var fieldsValue)
            ? ParseFieldNames(fieldsValue)
            : [];
        return new PyTypingConstructedType(name.AsString(), PyTypingConstructedKind.TypedDict, fields);
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

    private static bool TryGetSingleArgument(CallArgumentValue[] arguments, string keyword, out object value)
    {
        if (CountEffectiveArguments(arguments) != 1)
        {
            value = PyNone.Instance;
            return false;
        }

        return TryGetNamedOrPositional(arguments, 0, keyword, out value);
    }

    private static bool TryGetNamedOrPositional(CallArgumentValue[] arguments, int position, string keyword, out object value)
    {
        value = PyNone.Instance;
        var positionalIndex = 0;
        var found = false;
        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                if (positionalIndex == position)
                {
                    value = argument.Value;
                    found = true;
                }

                positionalIndex++;
                continue;
            }

            if (argument.Name == keyword)
            {
                value = argument.Value;
                found = true;
            }
        }

        return found;
    }

    private static int CountEffectiveArguments(CallArgumentValue[] arguments)
        => arguments.Length;

    private sealed class TypeVarCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable
    {
        public static readonly TypeVarCallable Instance = new();

        public string Name => "typing.TypeVar";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => TypeVar(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.TypeVar>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class NewTypeCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable
    {
        public static readonly NewTypeCallable Instance = new();

        public string Name => "typing.NewType";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => NewType(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.NewType>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CastCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable
    {
        public static readonly CastCallable Instance = new();

        public string Name => "typing.cast";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => Cast(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.cast>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class GetOriginCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable
    {
        public static readonly GetOriginCallable Instance = new();

        public string Name => "typing.get_origin";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => GetOrigin(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.get_origin>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class GetArgsCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable
    {
        public static readonly GetArgsCallable Instance = new();

        public string Name => "typing.get_args";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => GetArgs(arguments, span, context);

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<function typing.get_args>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class NewTypeIdentityCallable : LythonRuntime.ICallable, IPyRenderableValue, INamedRuntimeCallable
    {
        public NewTypeIdentityCallable(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (!TryGetSingleArgument(arguments, "value", out var value))
            {
                throw new LythonRuntimeException("TypeError", $"{Name}(value) expects one argument.", span);
            }

            return value;
        }

        public PyString RenderPython(PyRenderingContext context)
            => PyString.FromString($"<function NewType.{Name}>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}

internal enum PyTypingConstructedKind
{
    NamedTuple,
    TypedDict
}

internal sealed class PyTypingConstructedType : LythonRuntime.ICallable, IPyRenderableValue, IPyDynamicAttributes, INamedRuntimeCallable
{
    private readonly IReadOnlyList<string> _fieldNames;

    public PyTypingConstructedType(string name, PyTypingConstructedKind kind, IReadOnlyList<string> fieldNames)
    {
        Name = name;
        Kind = kind;
        _fieldNames = fieldNames;
    }

    public string Name { get; }

    public PyTypingConstructedKind Kind { get; }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        return Kind == PyTypingConstructedKind.TypedDict
            ? CreateTypedDict(arguments, span, context)
            : CreateNamedTuple(arguments, span);
    }

    public bool TryGetMember(string name, out object value)
    {
        value = name switch
        {
            "__name__" => PyString.FromString(Name),
            "__annotations__" => BuildAnnotations(),
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }

    public bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
    }

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString($"<class '{Name}'>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private object CreateTypedDict(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var dict = new PyDict(context.MemoryGovernor, span);
        foreach (var argument in arguments)
        {
            if (argument.Name is null)
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

            dict.SetItem(PyString.FromString(argument.Name), argument.Value);
        }

        return dict;
    }

    private object CreateNamedTuple(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        var values = new object[_fieldNames.Count];
        Array.Fill(values, PyNone.Instance);
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                if (positionalIndex >= values.Length)
                {
                    throw new LythonRuntimeException("TypeError", $"{Name}(...) received too many positional arguments.", span);
                }

                values[positionalIndex++] = argument.Value;
                continue;
            }

            var fieldIndex = IndexOfField(_fieldNames, argument.Name);
            if (fieldIndex < 0)
            {
                throw new LythonRuntimeException("TypeError", $"{Name}(...) received an unexpected keyword argument '{argument.Name}'.", span);
            }

            values[fieldIndex] = argument.Value;
        }

        return new PyTypingNamedTupleObject(Name, _fieldNames, values);
    }

    private static int IndexOfField(IReadOnlyList<string> fields, string name)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (string.Equals(fields[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private PyDict BuildAnnotations()
    {
        var dict = new PyDict();
        foreach (var fieldName in _fieldNames)
        {
            dict.SetItem(PyString.FromString(fieldName), new PyTypingAlias("Any"));
        }

        return dict;
    }
}

internal sealed class PyTypingNamedTupleObject : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes
{
    private readonly string _typeName;
    private readonly IReadOnlyList<string> _fieldNames;
    private readonly object[] _values;

    public PyTypingNamedTupleObject(string typeName, IReadOnlyList<string> fieldNames, object[] values)
    {
        _typeName = typeName;
        _fieldNames = fieldNames;
        _values = values;
    }

    public int Count => _values.Length;

    public int Length => _values.Length;

    public object this[int index] => _values[index];

    public object GetItem(int index) => _values[index];

    public object CreateSlice(IEnumerable<object> items)
        => new PyTuple(items);

    public object GetIndex(int index) => _values[index];

    public object GetSlice(IEnumerable<int> indices)
        => new PyTuple(indices.Select(index => _values[index]));

    public bool IsTruthy() => _values.Length != 0;

    public IEnumerable<object> Iterate() => _values;

    public bool TryGetMember(string name, out object value)
    {
        var index = IndexOfField(name);
        if (index >= 0)
        {
            value = _values[index];
            return true;
        }

        value = name switch
        {
            "_fields" => new PyTuple(_fieldNames.Select(PyString.FromString).Cast<object>()),
            "_replace" => new BoundNamedTupleReplace(this),
            _ => PyNone.Instance
        };

        return value is not PyNone;
    }

    public bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        var parts = _fieldNames
            .Select((fieldName, index) => $"{fieldName}={PyRendering.ToPythonString(_values[index], context)}");
        return PyString.FromString($"{_typeName}({string.Join(", ", parts)})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public IEnumerator<object> GetEnumerator() => ((IEnumerable<object>)_values).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _values.GetEnumerator();

    private object[] ToArray() => [.. _values];

    private int IndexOfField(string name)
    {
        for (var i = 0; i < _fieldNames.Count; i++)
        {
            if (string.Equals(_fieldNames[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class BoundNamedTupleReplace : LythonRuntime.ICallable, IPyRenderableValue
    {
        private readonly PyTypingNamedTupleObject _owner;

        public BoundNamedTupleReplace(PyTypingNamedTupleObject owner)
        {
            _owner = owner;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var values = _owner.ToArray();
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    throw new LythonRuntimeException("TypeError", $"{_owner._typeName}._replace(...) expects keyword arguments.", span);
                }

                var fieldIndex = _owner.IndexOfField(argument.Name);
                if (fieldIndex < 0)
                {
                    throw new LythonRuntimeException("ValueError", $"{_owner._typeName}._replace(...) got unexpected field name '{argument.Name}'.", span);
                }

                values[fieldIndex] = argument.Value;
            }

            return new PyTypingNamedTupleObject(_owner._typeName, _owner._fieldNames, values);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<bound method {_owner._typeName}._replace>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
