using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyGenericAlias : IPyDynamicAttributes, IPySubscriptableValue, IPyIterableValue,
    IPyRenderableValue, LythonRuntime.ICallable, IPyHashableValue, IPyGovernedValue
{
    internal static readonly PyBuiltinRuntimeType RuntimeType = new("types.GenericAlias",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Use generic subscripting in Lython.", span));
    internal static readonly PyBuiltinRuntimeType UnionType = new("types.UnionType",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Cannot construct UnionType directly", span));
    internal static readonly PyBuiltinRuntimeType TypingRuntimeType = new("typing._GenericAlias",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Cannot construct _GenericAlias directly", span));
    private object? _unpacked;
    private readonly PyTuple? _annotatedTypeArguments;
    private readonly PyTuple? _annotationMetadata;
    internal PyTypingAlias? Definition { get; }
    public object Origin { get; }
    public PyTuple Arguments { get; }
    public PyTuple Parameters { get; }
    public MemoryGovernor? OwnerMemoryGovernor { get; }
    public LythonSourceSpan? AllocationSpan { get; }
    internal bool IsUnion => ReferenceEquals(Origin, UnionType) || ReferenceEquals(Origin, PyTyping.Definition("Union"));
    private bool IsLiteral => Definition is { ShortName: "Literal" };
    internal object InspectionOrigin => Definition is { ShortName: "Annotated" } ? Definition : Origin;

    public static object Create(object origin, object index, MemoryGovernor governor, LythonSourceSpan span,
        PyTypingAlias? definition = null)
    {
        var arguments = index is PyTuple tuple ? tuple : new PyTuple([index], governor, span);
        if (definition is { ShortName: "Callable" } && arguments.Count == 2 && arguments[0] is PyList callParameters)
            arguments = new PyTuple(callParameters.Concat([arguments[1]]), governor, span);
        if (definition is { ShortName: "Annotated" } && arguments[0] is PyGenericAlias { Definition.ShortName: "Annotated" } nested)
            arguments = new PyTuple(nested.Arguments.Concat(arguments.Skip(1)), governor, span);
        if (definition is not null && definition.ShortName != "Literal" && arguments.Any(a => a is PyNone))
            arguments = new PyTuple(arguments.Select((a, i) => a is PyNone && (definition.ShortName != "Annotated" || i == 0)
                ? PyType.NoneType : a), governor, span);

        var union = ReferenceEquals(origin, UnionType) || ReferenceEquals(origin, PyTyping.Definition("Union"));
        var literal = definition is { ShortName: "Literal" };
        if (union || literal)
        {
            long capacity = 0;
            foreach (var argument in arguments)
            {
                PyStructuralGuard.NoteWork();
                capacity = checked(capacity + (argument is PyGenericAlias alias && (union ? alias.IsUnion : alias.IsLiteral) ? alias.Arguments.Count : 1));
            }
            if (capacity > int.MaxValue) throw new LythonRuntimeException("OverflowError", "Too many alias alternatives.", span);
            using var scratch = governor.ReserveTemporary(checked(128 + 64 * capacity), span);
            var alternatives = new List<object>((int)capacity);
            foreach (var argument in arguments)
            {
                if (argument is PyGenericAlias alias && (union ? alias.IsUnion : alias.IsLiteral))
                    foreach (var item in alias.Arguments) Add(item);
                else Add(argument);
            }
            if (union && alternatives.Count == 1) return alternatives[0];
            arguments = new PyTuple(alternatives, governor, span);

            void Add(object item)
            {
                PyStructuralGuard.NoteWork();
                if (union && item is PyNone) item = PyType.NoneType;
                if (!ContainsAlternative(alternatives, item, literal)) alternatives.Add(item);
            }
        }

        long possibleParameters = 0;
        foreach (var argument in arguments)
        {
            PyStructuralGuard.NoteWork();
            possibleParameters = checked(possibleParameters + (argument is PyTypeParameter ? 1 : argument is PyGenericAlias alias ? alias.Parameters.Count :
                argument is PyUnpackedType { Value: PyTypeParameter } ? 1 : 0));
        }
        // Arguments already have a funded immutable owner. Only the parameter
        // inventory needs working capacity; do not charge another argument copy.
        using var working = governor.ReserveTemporary(checked(256 + 128 * possibleParameters), span);
        var parameters = new List<object>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < arguments.Count; i++)
        {
            PyStructuralGuard.NoteWork();
            if (literal || definition is { ShortName: "Annotated" } && i != 0) continue;
            var argument = arguments[i];
            if (argument is PyTypeParameter parameter && seen.Add(parameter)) parameters.Add(parameter);
            if (argument is PyUnpackedType { Value: PyTypeParameter unpacked } && seen.Add(unpacked)) parameters.Add(unpacked);
            if (argument is PyGenericAlias alias)
                foreach (var nestedParameter in alias.Parameters)
                    if (seen.Add(nestedParameter)) parameters.Add(nestedParameter);
        }
        var parameterTuple = parameters.Count == 0 ? PyTuple.Empty : new PyTuple(parameters, governor, span);
        var created = new PyGenericAlias(origin, arguments, parameterTuple, definition, governor, span);
        working.Commit(128);
        return created;
    }

    private PyGenericAlias(object origin, PyTuple arguments, PyTuple parameters, PyTypingAlias? definition,
        MemoryGovernor governor, LythonSourceSpan span)
    {
        Origin = origin; Arguments = arguments; Parameters = parameters; Definition = definition;
        OwnerMemoryGovernor = governor; AllocationSpan = span;
        if (definition is { ShortName: "Annotated" })
        {
            _annotatedTypeArguments = new PyTuple([arguments[0]], governor, span);
            _annotationMetadata = new PyTuple(arguments.Skip(1), governor, span);
        }
    }

    public object GetSubscript(object index, LythonSourceSpan span)
    {
        if (Parameters.Count == 0) throw new LythonRuntimeException("TypeError", "Alias is not generic", span);
        using var traversal = PyStructuralGuard.EnterSingle(this, span);
        using var scratch = OwnerMemoryGovernor!.ReserveTemporary(128L + 128L * (Parameters.Count + Arguments.Count), span);
        var replacements = index is PyTuple tuple ? tuple : new PyTuple([index], OwnerMemoryGovernor!, span);
        if (replacements.Count != Parameters.Count) throw new LythonRuntimeException("TypeError", "Incorrect number of generic arguments", span);
        var values = new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < Parameters.Count; i++) values[Parameters[i]] = replacements[i];
        var substituted = new PyTuple(Arguments.Select((argument, i) => Definition is { ShortName: "Annotated" } && i != 0
            ? argument : Substitute(argument)), OwnerMemoryGovernor!, span);
        return Create(Origin, substituted, OwnerMemoryGovernor!, span, Definition);

        object Substitute(object argument)
        {
            PyStructuralGuard.NoteWork();
            if (values.TryGetValue(argument, out var replacement)) return replacement;
            if (argument is PyGenericAlias { Parameters.Count: > 0 } nested)
            {
                using var nestedTraversal = PyStructuralGuard.EnterSingle(nested, span);
                var nestedArguments = new PyTuple(nested.Arguments.Select((value, i) => nested.Definition is { ShortName: "Annotated" } && i != 0
                    ? value : Substitute(value)), OwnerMemoryGovernor!, span);
                return Create(nested.Origin, nestedArguments, OwnerMemoryGovernor!, span, nested.Definition);
            }
            return argument;
        }
    }

    internal PyTuple GetInspectionArguments(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (Definition is { ShortName: "Annotated" }) return new PyTuple(Arguments, context.MemoryGovernor, span);
        if (Definition is not { ShortName: "Callable" } || Arguments.Count == 2 && Arguments[0] is PyTypeParameter or PyEllipsis)
            return Arguments;
        var parameters = new PyList(Arguments.Take(Arguments.Count - 1), context.MemoryGovernor, span);
        return new PyTuple([parameters, Arguments[Arguments.Count - 1]], context.MemoryGovernor, span);
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "__origin__" when !ReferenceEquals(Origin, UnionType) => _annotatedTypeArguments is not null ? Arguments[0] : Origin,
            "__args__" => _annotatedTypeArguments ?? Arguments,
            "__parameters__" => Parameters,
            "__metadata__" when _annotationMetadata is not null => _annotationMetadata,
            _ => MissingMemberValue.Instance,
        };
        return !ReferenceEquals(value, MissingMemberValue.Instance) || Definition is not null && Definition.TryGetMember(name, out value);
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => InvokeAsyncCore(arguments, span, context, false).GetAwaiter().GetResult();
    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => InvokeAsyncCore(arguments, span, context, true);
    private async ValueTask<object> InvokeAsyncCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (Definition is not null || IsUnion || Origin is not LythonRuntime.ICallable callable)
            throw new LythonRuntimeException("TypeError", "This alias is not instantiable in Lython.", span);
        var value = asynchronous ? await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false) : callable.Invoke(arguments, span, context);
        if (value is PyInstance instance) instance.SetAttribute("__orig_class__", this);
        return value;
    }
    public IEnumerable<object> Iterate() { yield return _unpacked ??= new PyUnpackedType(this); }

    private static bool ContainsAlternative(IEnumerable<object> items, object candidate, bool literal)
    {
        foreach (var item in items)
        {
            PyStructuralGuard.NoteWork();
            if ((!literal || LiteralType(item) == LiteralType(candidate)) && PyEquality.AreEqual(item, candidate)) return true;
        }
        return false;
    }
    private static Type LiteralType(object value) => value is bool ? typeof(bool) : PyNumberOps.IsInteger(value) ? typeof(int) : value.GetType();

    public int GetPyHashCode()
    {
        using var traversal = PyStructuralGuard.EnterSingle(this, AllocationSpan);
        if (!IsUnion && !IsLiteral)
            return HashCode.Combine(Origin is IPyHashableValue hashable ? hashable.GetPyHashCode() : RuntimeHelpers.GetHashCode(Origin), Definition, Arguments.GetPyHashCode());
        var hash = IsUnion ? 0x241901 : 0x241902;
        foreach (var argument in Arguments)
        {
            PyStructuralGuard.NoteWork();
            var itemHash = PyValueComparer.Instance.GetHashCode(argument);
            hash ^= IsLiteral ? HashCode.Combine(LiteralType(argument), itemHash) : itemHash;
        }
        return HashCode.Combine(hash, Arguments.Count);
    }
    public override bool Equals(object? other)
    {
        if (other is not PyGenericAlias alias) return false;
        using var traversal = PyStructuralGuard.EnterPair(this, alias, AllocationSpan);
        if (IsUnion || IsLiteral)
            return IsUnion == alias.IsUnion && IsLiteral == alias.IsLiteral && Arguments.Count == alias.Arguments.Count &&
                Arguments.All(argument => ContainsAlternative(alias.Arguments, argument, IsLiteral));
        return ReferenceEquals(Origin, alias.Origin) && ReferenceEquals(Definition, alias.Definition) && PyEquality.AreEqual(Arguments, alias.Arguments);
    }
    public override int GetHashCode() => GetPyHashCode();

    public PyString RenderPython(PyRenderingContext context)
    {
        using var traversal = PyStructuralGuard.EnterSingle(this, AllocationSpan, context.Context);
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, AllocationSpan);
        try
        {
            var infix = IsUnion && Definition is null;
            var optional = IsUnion && !infix && Arguments.Count == 2 && Arguments.Any(a => ReferenceEquals(a, PyType.NoneType));
            if (!infix)
            {
                if (Definition is not null) builder.AppendString(optional ? "typing.Optional" : IsUnion ? "typing.Union" : Definition.RenderName());
                else AppendArgument(builder, Origin, context);
                builder.Append((byte)'[');
            }
            if (Definition is { ShortName: "Callable" } && !(Arguments.Count == 2 && Arguments[0] is PyTypeParameter or PyEllipsis))
            {
                builder.Append((byte)'[');
                for (var i = 0; i < Arguments.Count - 1; i++)
                {
                    context.Context.CheckExecution(AllocationSpan);
                    if (i != 0) builder.AppendAscii(", ");
                    AppendArgument(builder, Arguments[i], context);
                }
                builder.AppendAscii("], ");
                AppendArgument(builder, Arguments[Arguments.Count - 1], context);
            }
            else
            {
                if (Arguments.Count == 0) builder.AppendAscii("()");
                var rendered = 0;
                foreach (var argument in Arguments)
                {
                    context.Context.CheckExecution(AllocationSpan);
                    if (optional && ReferenceEquals(argument, PyType.NoneType)) continue;
                    if (rendered++ != 0) builder.AppendAscii(infix ? " | " : ", ");
                    if (infix && ReferenceEquals(argument, PyType.NoneType)) builder.AppendAscii("None");
                    else AppendArgument(builder, argument, context);
                }
            }
            if (!infix) builder.Append((byte)']');
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }
    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    internal static void AppendArgument(GovernedByteBuilder builder, object value, PyRenderingContext context)
    {
        switch (value)
        {
            case PyType type: builder.AppendString(type.Name); break;
            case PyTypeAlias alias: builder.Append(alias.NameValue); break;
            case PyBuiltinRuntimeType type: builder.AppendString(type.Name); break;
            case PyTyping.NewTypeIdentityCallable named: builder.Append(named.RenderPython(context)); break;
            case INamedRuntimeCallable callable: builder.AppendString(callable.Name); break;
            case PyNone: builder.AppendAscii("None"); break;
            default: builder.Append(PyRendering.ToReprPyString(value, context)); break;
        }
    }
}
