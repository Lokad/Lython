using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using System.Text;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Pooled binder value slots for the common layout widths. The consumer
    // bound Values remain invocation-owned: matching executable frames borrow
    // them through completion, other frames copy refs out, and dataclass
    // dunders read transiently. Captures/exposed locals retain values or cells,
    // never this array. ThreadStatic plus take-null with keep-first
    // stays safe under recursion, suspension and parallel tests; slots are
    // cleared before caching (fresh arrays start null too). Binding failures
    // drop the box instead of caching it. Only the hot user-function paths
    // return boxes; colder paths simply do not cache.
    [ThreadStatic]
    private static object[]? _pooledOneBoundValue;
    [ThreadStatic]
    private static object[]? _pooledTwoBoundValues;
    [ThreadStatic]
    private static object[]? _pooledThreeBoundValues;

    private static object[] RentBoundValues(int count)
    {
        if (count == 1)
        {
            var rented = _pooledOneBoundValue;
            if (rented is not null)
            {
                _pooledOneBoundValue = null;
                return rented;
            }
            return new object[1];
        }
        if (count == 2)
        {
            var rented = _pooledTwoBoundValues;
            if (rented is not null)
            {
                _pooledTwoBoundValues = null;
                return rented;
            }
            return new object[2];
        }
        if (count == 3)
        {
            var rentedThree = _pooledThreeBoundValues;
            if (rentedThree is not null)
            {
                _pooledThreeBoundValues = null;
                return rentedThree;
            }
            return new object[3];
        }
        return new object[count];
    }

    internal static void ReturnBoundValues(object[] values)
    {
        if (values.Length == 1)
        {
            Array.Clear(values);
            _pooledOneBoundValue ??= values;
        }
        else if (values.Length == 2)
        {
            Array.Clear(values);
            _pooledTwoBoundValues ??= values;
        }
        else if (values.Length == 3)
        {
            Array.Clear(values);
            _pooledThreeBoundValues ??= values;
        }
    }

    internal static BoundCallArguments BindFunctionArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context)
    {
        // Values travel in plan layout order (positional, keyword-only, then
        // variadic names) with presence bits, instead of a per-call name
        // dictionary. The layout arrays live on the shared plan; only the value
        // slots are per-call.
        var values = plan.LayoutParameterNames.Count == 0
            ? Array.Empty<object>()
            : RentBoundValues(plan.LayoutParameterNames.Count);
        try
        {
            return BindInto(values, arguments, span, plan, context);
        }
        catch
        {
            // Arity failures after the rent must not strand the pooled box:
            // clear it and hand it back so the next bind reuses the array
            // instead of allocating (and retaining argument refs until GC).
            ReturnBoundValues(values);
            throw;
        }
    }

    internal static ValueTask<BoundCallArguments> BindFunctionArgumentsAsync(
        CallArgumentValue[] arguments, LythonSourceSpan span, FunctionBindingPlan plan, ExecutionContext context)
    {
        var defaults = plan.KeywordDefaultStorage;
        if (defaults is not { HasProtocolKeys: true })
            return new(BindFunctionArguments(arguments, span, plan, context));
        return BindProtocolDefaultsAsync(arguments, span, plan, context);
    }

    // Eligible signatures contain only positional parameters. Read existing
    // operands once into owned bound storage, before a body can suspend.
    internal static BoundCallArguments BindPositionalFunctionArguments(
        ReadOnlySpan<object?> arguments, LythonSourceSpan span, FunctionBindingPlan plan, object? receiver = null)
    {
        var values = plan.LayoutParameterNames.Count == 0 ? [] : RentBoundValues(plan.LayoutParameterNames.Count);
        try
        {
            var offset = receiver is null ? 0 : 1;
            if (arguments.Length > plan.PositionalParameters.Count - offset)
                throw CallErrors.TooManyPositional(plan.CallableKind, plan.CallableName, span);
            var assigned = new ArgumentPresence(values.Length);
            if (receiver is not null)
            {
                values[0] = receiver;
                assigned[0] = true;
            }
            for (var i = 0; i < arguments.Length; i++)
            {
                values[i + offset] = arguments[i].RequireNotNull();
                assigned[i + offset] = true;
            }
            for (var i = arguments.Length + offset; i < values.Length; i++)
            {
                if (!plan.TryGetPositionalDefault(i, out var value))
                    throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, plan.PositionalParameters[i].Name, span);
                values[i] = value;
            }
            return new BoundCallArguments(values, assigned);
        }
        catch
        {
            ReturnBoundValues(values);
            throw;
        }
    }

    private static async ValueTask<BoundCallArguments> BindProtocolDefaultsAsync(
        CallArgumentValue[] arguments, LythonSourceSpan span, FunctionBindingPlan plan,
        ExecutionContext context)
    {
        var values = plan.LayoutParameterNames.Count == 0 ? [] : RentBoundValues(plan.LayoutParameterNames.Count);
        try
        {
            var bound = BindInto(values, arguments, span, plan, context, deferKeywordDefaults: true);
            for (var k = 0; k < plan.KeywordOnlyParameters.Count; k++)
            {
                var index = plan.PositionalParameters.Count + k;
                if (bound.Assigned[index]) continue;
                var defaults = plan.KeywordDefaultStorage;
                if (defaults is null)
                    throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, plan.KeywordOnlyParameters[k].Name, span);
                var value = await defaults.TryGetBuiltinStringAsync(plan.KeywordDefaultNames[k], context, span).ConfigureAwait(false);
                if (!value.Found)
                    throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, plan.KeywordOnlyParameters[k].Name, span);
                values[index] = value.Value;
            }
            return bound;
        }
        catch
        {
            ReturnBoundValues(values);
            throw;
        }
    }

    // Post-rent binding body: every throw inside (too many positionals,
    // unexpected or duplicated keywords, missing arguments) funnels through
    // the catch in BindFunctionArguments, which returns the rented box.
    private static BoundCallArguments BindInto(
        object[] values,
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context,
        bool deferKeywordDefaults = false)
    {
        var pool = context.State.MemoryGovernor;
        var assigned = new ArgumentPresence(plan.LayoutParameterNames.Count);
        // The overflow list is scratch: most calls never spill positionals,
        // so materialize it only on the first spill, tracked in the call pool
        // invocation for a list that is dropped before return.
        PyList? extraPositional = null;
        // Keyword overflow likewise materializes only for **kwargs functions
        // receiving unknown names; every other call skips the dictionary.
        Dictionary<string, object>? extraKeywords = null;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                if (positionalIndex >= plan.PositionalParameters.Count)
                {
                    if (plan.VariadicList is null)
                    {
                        throw CallErrors.TooManyPositional(plan.CallableKind, plan.CallableName, span);
                    }

                    if (extraPositional is null)
                    {
                        extraPositional = new PyList([], context.MemoryGovernor, span);
                    }

                    extraPositional.Add(argument.Value);
                    continue;
                }

                values[positionalIndex] = argument.Value;
                assigned[positionalIndex] = true;
                positionalIndex++;
                continue;
            }

            var keywordName = argument.KeywordName;
            if (!plan.LayoutParameterIndex.TryGetValue(keywordName, out var keywordIndex) ||
                keywordIndex >= plan.NamedLayoutCount || keywordIndex < plan.PositionalOnlyCount)
            {
                if (plan.VariadicDictionary is null)
                {
                    throw CallErrors.UnexpectedKeyword(plan.CallableKind, plan.CallableName, keywordName, span);
                }

                extraKeywords ??= new Dictionary<string, object>(StringComparer.Ordinal);
                if (!extraKeywords.TryAdd(keywordName, argument.Value))
                {
                    throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, keywordName, span);
                }

                continue;
            }

            if (assigned[keywordIndex])
            {
                throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, keywordName, span);
            }

            values[keywordIndex] = argument.Value;
            assigned[keywordIndex] = true;
        }

        for (var i = 0; i < plan.PositionalParameters.Count; i++)
        {
            if (assigned[i])
            {
                continue;
            }

            var parameterName = plan.PositionalParameters[i].Name;
            if (plan.TryGetPositionalDefault(i, out var defaultValue))
            {
                values[i] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameterName, span);
        }

        for (var k = 0; !deferKeywordDefaults && k < plan.KeywordOnlyParameters.Count; k++)
        {
            var i = plan.PositionalParameters.Count + k;
            if (assigned[i])
            {
                continue;
            }

            var parameterName = plan.KeywordOnlyParameters[k].Name;
            var keywordDefaults = plan.KeywordDefaultStorage;
            if (keywordDefaults is not null && keywordDefaults.TryGetValue(plan.KeywordDefaultNames[k], out var defaultValue, context, span))
            {
                values[i] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameterName, span);
        }

        if (plan.VariadicList is not null)
        {
            var variadicIndex = plan.LayoutParameterIndex[plan.VariadicList.Name];
            var overflow = extraPositional;
            if (overflow is null || overflow.Count == 0)
            {
                values[variadicIndex] = CreateTuple(0, _ => PyNone.Instance, context, span);
            }
            else
            {
                var overflowTuple = CreateTuple(overflow.Count, i => overflow[i], context, span);
                values[variadicIndex] = overflowTuple;
            }

            assigned[variadicIndex] = true;
        }

        if (plan.VariadicDictionary is not null)
        {
            var keywordDict = new PyDict(context.MemoryGovernor, span);
            if (extraKeywords is not null)
            {
                foreach (var pair in extraKeywords)
                {
                    var keyword = PyString.FromString(pair.Key, context.MemoryGovernor, span);
                    keywordDict.SetItem(keyword, pair.Value);
                }
            }
            var keywordDictIndex = plan.LayoutParameterIndex[plan.VariadicDictionary.Name];
            values[keywordDictIndex] = keywordDict;
            assigned[keywordDictIndex] = true;
        }

        return new BoundCallArguments(values, assigned);
    }

    // Constructed function objects retain a wrapper plus binding plan and
    // default containers per evaluation. Defs and lambdas execute per evaluation,
    // so each constructed value charges once built.
    private const long FunctionValueBytes = 128;

    // Constructed class objects retain a type record plus one namespace slot per
    // member; charge the base plus per-member slots once built. Member values
    // stay owned by their own construction.
    private const long ClassTypeBaseBytes = 128;
    private const long ClassMemberSlotBytes = 64;

    internal static void ChargeClassTypeValue(int memberCount, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var bytes = checked(ClassTypeBaseBytes + ClassMemberSlotBytes * (long)memberCount);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
    }

    private static void ChargeFunctionValue(ExecutionContext? context, LythonSourceSpan? span)
    {
        context?.MemoryGovernor.Reserve(FunctionValueBytes, span);
        context?.MemoryGovernor.Commit(FunctionValueBytes);
    }

    // The definition-time name map is scratch; only the Python default
    // containers survive with the function. Cover replacement overlap while
    // constructing them, then return the scratch allowance deterministically.
    internal static MemoryGovernor.TemporaryMemoryReservation? ReserveDefaultArgumentsWorkingStorage(
        int defaultCount, MemoryGovernor? governor, LythonSourceSpan? span)
        => defaultCount <= 0 ? null : governor?.ReserveTemporary(64L + 64L * defaultCount, span);

    private const long ClosureContextBaseBytes = 512;
    private const long ClosureCellSlotBytes = 32;

    internal static void ChargeClosureRetention(ExecutionContext? closure, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (closure is null || governor is null)
        {
            return;
        }

        for (var current = closure; current is not null; current = current.ParentContext)
        {
            var isModuleFrame = current.Frame.Parent is null;
            var owned = isModuleFrame
                ? CountOwnedModuleVariables(current)
                : current.Frame.Variables.Count;

            if (!current.ClosureRetentionCharged)
            {
                var bytes = checked(ClosureContextBaseBytes + ClosureCellSlotBytes * (long)owned);
                governor.Reserve(bytes, span);
                governor.Commit(bytes);
                current.ClosureRetentionCharged = true;
                current.ClosureChargedVariableCount = owned;

                continue;
            }

            var uncharged = owned - current.ClosureChargedVariableCount;
            if (uncharged > 0)
            {
                var delta = checked(ClosureCellSlotBytes * (long)uncharged);
                governor.Reserve(delta, span);
                governor.Commit(delta);
                current.ClosureChargedVariableCount = owned;
            }

            // A paid context implies paid ancestors: every walk covers the whole
            // chain and chains only grow at the leaf, so the walk ends here.
            break;
        }

    }

    // Module frames start as copies of the run builtins table; those aliases
    // stay owned by the run, so only genuinely module-owned entries count here.
    // Shadowing assignments replace the shared reference and count normally.
    private static int CountOwnedModuleVariables(ExecutionContext context)
    {
        var builtins = context.Services.State.BuiltinVariables;
        var owned = 0;
        foreach (var pair in context.Frame.Variables)
        {
            if (!builtins.TryGetValue(pair.Key, out var shared) || !ReferenceEquals(shared, pair.Value))
            {
                owned++;
            }
        }

        return owned;
    }

    internal static Dictionary<string, object> BuildDefaultArgumentMap(
        IReadOnlyList<LoweredFunctionParameter> parameters,
        Func<LoweredExpression, object> evaluate)
    {
        var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (parameter.DefaultValue is not null)
            {
                defaults[parameter.Name] = RuntimeValue(evaluate(parameter.DefaultValue));
            }
        }

        return defaults;
    }

    internal static async ValueTask<Dictionary<string, object>> BuildDefaultArgumentMapAsync(
        IReadOnlyList<LoweredFunctionParameter> parameters,
        Func<LoweredExpression, ValueTask<object>> evaluate)
    {
        var defaults = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (parameter.DefaultValue is not null)
            {
                defaults[parameter.Name] = RuntimeValue(await evaluate(parameter.DefaultValue).ConfigureAwait(false));
            }
        }

        return defaults;
    }

    internal sealed class ExceptionTypeValue : ICallable, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyRenderableValue, IPythonExceptionType, IEquatable<ExceptionTypeValue>
    {
        // Builtin module labels form a fixed vocabulary (the exception modules
        // plus every top-level module owning builtin callables), so every known
        // module shares one constant forever and per-access __module__ reads alias
        // stably like CPython; nested or unknown module names keep building
        // fresh labels.
        private static readonly PyString BuiltinsModuleName = PyString.FromString("builtins");
        private static readonly PyString ArgparseModuleName = PyString.FromString("argparse");
        private static readonly PyString CopyModuleName = PyString.FromString("copy");
        private static readonly PyString CollectionsModuleName = PyString.FromString("collections");
        private static readonly PyString CsvModuleName = PyString.FromString("csv");
        private static readonly PyString DataclassesModuleName = PyString.FromString("dataclasses");
        private static readonly PyString DatetimeModuleName = PyString.FromString("datetime");
        private static readonly PyString DecimalModuleName = PyString.FromString("decimal");
        private static readonly PyString DifflibModuleName = PyString.FromString("difflib");
        private static readonly PyString FilecmpModuleName = PyString.FromString("filecmp");
        private static readonly PyString FnmatchModuleName = PyString.FromString("fnmatch");
        private static readonly PyString FunctoolsModuleName = PyString.FromString("functools");
        private static readonly PyString GlobModuleName = PyString.FromString("glob");
        private static readonly PyString GzipModuleName = PyString.FromString("gzip");
        private static readonly PyString StructModuleName = PyString.FromString("struct");
        private static readonly PyString ElementTreeModuleName = PyString.FromString("xml.etree.ElementTree");
        private static readonly PyString HashlibModuleName = PyString.FromString("hashlib");
        private static readonly PyString ImportlibModuleName = PyString.FromString("importlib");
        private static readonly PyString ItertoolsModuleName = PyString.FromString("itertools");
        private static readonly PyString IoModuleName = PyString.FromString("io");
        private static readonly PyString JsonModuleName = PyString.FromString("json");
        private static readonly PyString MathModuleName = PyString.FromString("math");
        private static readonly PyString OpenPyxlExceptionsModuleName = PyString.FromString("openpyxl.utils.exceptions");
        private static readonly PyString OperatorModuleName = PyString.FromString("operator");
        private static readonly PyString OsModuleName = PyString.FromString("os");
        private static readonly PyString PathlibModuleName = PyString.FromString("pathlib");
        private static readonly PyString PkgutilModuleName = PyString.FromString("pkgutil");
        private static readonly PyString RandomModuleName = PyString.FromString("random");
        private static readonly PyString ReModuleName = PyString.FromString("re");
        private static readonly PyString ShlexModuleName = PyString.FromString("shlex");
        private static readonly PyString ShutilModuleName = PyString.FromString("shutil");
        private static readonly PyString StatisticsModuleName = PyString.FromString("statistics");
        private static readonly PyString SysModuleName = PyString.FromString("sys");
        private static readonly PyString TimeModuleName = PyString.FromString("time");
        private static readonly PyString TypingModuleName = PyString.FromString("typing");
        private static readonly PyString SubprocessModuleName = PyString.FromString("subprocess");
        private static readonly PyString ZipfileModuleName = PyString.FromString("zipfile");

        internal static PyString SharedModuleLabel(string moduleName) => moduleName switch
        {
            "builtins" => BuiltinsModuleName,
            "argparse" => ArgparseModuleName,
            "copy" => CopyModuleName,
            "collections" => CollectionsModuleName,
            "csv" => CsvModuleName,
            "dataclasses" => DataclassesModuleName,
            "datetime" => DatetimeModuleName,
            "decimal" => DecimalModuleName,
            "difflib" => DifflibModuleName,
            "filecmp" => FilecmpModuleName,
            "fnmatch" => FnmatchModuleName,
            "functools" => FunctoolsModuleName,
            "glob" => GlobModuleName,
            "gzip" => GzipModuleName,
            "struct" => StructModuleName,
            "xml.etree.ElementTree" => ElementTreeModuleName,
            "hashlib" => HashlibModuleName,
            "importlib" => ImportlibModuleName,
            "itertools" => ItertoolsModuleName,
            "io" => IoModuleName,
            "json" => JsonModuleName,
            "math" => MathModuleName,
            "openpyxl.utils.exceptions" => OpenPyxlExceptionsModuleName,
            "operator" => OperatorModuleName,
            "os" => OsModuleName,
            "pathlib" => PathlibModuleName,
            "pkgutil" => PkgutilModuleName,
            "random" => RandomModuleName,
            "re" => ReModuleName,
            "shlex" => ShlexModuleName,
            "shutil" => ShutilModuleName,
            "statistics" => StatisticsModuleName,
            "sys" => SysModuleName,
            "time" => TimeModuleName,
            "typing" => TypingModuleName,
            "subprocess" => SubprocessModuleName,
            "zipfile" => ZipfileModuleName,
            _ => PyString.FromString(moduleName),
        };

        public ExceptionTypeValue(string typeName)
            : this(PythonExceptionIdentity.Builtin(typeName))
        {
        }

        public ExceptionTypeValue(PythonExceptionIdentity identity)
        {
            ExceptionIdentity = identity;
        }

        public PythonExceptionIdentity ExceptionIdentity { get; }

        public string TypeName => ExceptionIdentity.TypeName;

        internal TypeNewMethod? NewSlot { get; set; }

        // Exception identities are immutable, so the name renders once and every
        // later read aliases it (matching CPython identity) instead of minting a
        // fresh string per read. Only builtin identities reach this type, keeping
        // each cached constant tiny.
        private PyString? _nameValue;

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span,
            [MaybeNullWhen(false)] out object value)
        {
            if (name != "__bases__") return TryGetMember(name, out value);
            if (context.Services.State.TryReadExceptionBases(ExceptionIdentity, out var cached))
            {
                value = cached;
                return true;
            }
            var identities = ExceptionBaseIdentities.TryGetValue(ExceptionIdentity, out var bases) ? bases : [];
            using var scratch = context.MemoryGovernor.ReserveTemporary(32L + 8L * Math.Max(1, identities.Length), span);
            var resolved = new object[Math.Max(1, identities.Length)];
            if (identities.Length == 0)
                resolved[0] = TryGetBuiltinOrNull(context, "object")
                    ?? throw new InvalidOperationException("Missing object type.");
            else
                for (var index = 0; index < identities.Length; index++)
                {
                    var identity = identities[index];
                    resolved[index] = identity.IsBuiltin
                        ? TryGetBuiltinOrNull(context, identity.TypeName)
                            ?? throw new InvalidOperationException("Missing builtin exception type.")
                        : TryGetModuleMemberOrNull(context, identity.ModuleName, identity.TypeName)
                            ?? throw new InvalidOperationException("Missing module exception type.");
                }
            var tuple = new PyTuple(resolved, context.MemoryGovernor, span);
            try { context.Services.State.CacheExceptionBases(ExceptionIdentity, tuple, span); }
            catch
            {
                context.Services.State.MemoryGovernor.RefundUnpublishedValue(tuple);
                throw;
            }
            value = tuple;
            return true;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => _nameValue ??= PyString.FromString(TypeName),
                "__module__" => SharedModuleLabel(ExceptionIdentity.ModuleName),
                "type" => _nameValue ??= PyString.FromString(TypeName),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class '" + ExceptionIdentity.QualifiedName + "'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public bool Equals(ExceptionTypeValue? other)
            => other is not null && ExceptionIdentity == other.ExceptionIdentity;

        public override bool Equals(object? obj) => obj is ExceptionTypeValue other && Equals(other);

        public override int GetHashCode() => ExceptionIdentity.GetHashCode();

        private static string TakeMessageText(PyString rendered, ExecutionContext context, LythonSourceSpan span)
        {
            var text = rendered.AsString();
            return text;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", $"{TypeName}(message) does not accept keyword arguments.", span);
                }
            }

            if (TypeName == "SystemExit")
            {
                if (arguments.Length > 1)
                {
                    throw new LythonRuntimeException("TypeError", "SystemExit([code]) expects zero or one argument.", span);
                }

                var value = arguments.Length == 0 ? PyNone.Instance : arguments[0].Value;
                return PyException.CreateOwned(ExceptionIdentity, FormatSystemExitMessage(value), value, context, span);
            }

            var values = arguments.Select(argument => argument.Value).ToArray();
            var args = new PyTuple(values, context.MemoryGovernor, span);
            var message = values.Length switch
            {
                0 => string.Empty,
                // Like CPython, a single KeyError argument renders through
                // repr instead of str; every other single argument uses str.
                1 when ExceptionIdentity.IsBuiltin && string.Equals(TypeName, "KeyError", StringComparison.Ordinal) =>
                    TakeMessageText(PyRendering.ToReprPyString(values[0], new PyRenderingContext(context)), context, span),
                1 => TakeMessageText(PyRendering.ToInterpolatedPyString(values[0], new PyRenderingContext(context)), context, span),
                _ => TakeMessageText(PyRendering.ToReprPyString(args, new PyRenderingContext(context)), context, span),
            };
            var payload = values.Length == 0 ? PyNone.Instance : values.Length == 1 ? values[0] : args;
            return PyException.CreateOwned(ExceptionIdentity, message, payload, context, span, args);
        }
    }

    private static string FormatSystemExitMessage(object value)
        => value switch
        {
            PyNone => string.Empty,
            BigInteger integer => integer.ToString(),
            bool boolean => boolean ? "True" : "False",
            _ when PyStringOps.TryAsString(value, out var text) => text.AsString(),
            _ => value.ToString() ?? string.Empty
        };
}
