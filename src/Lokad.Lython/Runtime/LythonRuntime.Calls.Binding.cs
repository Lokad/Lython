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
    // bound Values remain invocation-owned: nonmirrored executable frames borrow
    // them through completion, other frames copy refs out, and dataclass
    // dunders read transiently. Captures/exposed locals retain values or cells,
    // never this array. ThreadStatic plus take-null with keep-first
    // stays safe under recursion, suspension and parallel tests; slots are
    // cleared before caching (fresh arrays start null too). Only the user-function paths
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

    // The function invocation owns this buffer through default lookup and body
    // completion. Values/cells/snapshots have their own retained ownership.
    internal static void ReturnBoundArguments(BoundCallArguments bound)
    {
        try
        {
            if (bound.Values.Length > 3) Array.Clear(bound.Values);
            ReturnBoundValues(bound.Values);
        }
        finally { bound.StorageOwner?.Release(bound.StorageBytes); }
    }

    private static BoundCallArguments RentFunctionArgumentStorage(
        FunctionBindingPlan plan, LythonSourceSpan span, MemoryGovernor? governor, int? storageCapacity)
    {
        var parameterCount = plan.LayoutParameterNames.Count;
        var capacity = storageCapacity ?? parameterCount;
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, parameterCount);
        // Existing one/two/three-slot cleared pools are bounded control storage.
        // Unpooled executable storage covers capacity, flags and metadata before
        // allocation, without a separate per-call reservation object.
        var owner = storageCapacity is not null && capacity > 3
            ? governor ?? throw new ArgumentNullException(nameof(governor)) : null;
        var bytes = owner is null ? 0 : 64L + 16L * capacity;
        owner?.Reserve(bytes, span);
        object[] values = [];
        try
        {
            values = capacity == 0 ? [] : RentBoundValues(capacity);
            var bound = new BoundCallArguments(values, new ArgumentPresence(parameterCount), owner, bytes);
            owner?.Commit(bytes);
            return bound;
        }
        catch
        {
            ReturnBoundValues(values);
            owner?.ReleaseReserved(bytes);
            throw;
        }
    }

    internal static BoundCallArguments BindFunctionArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context,
        object? receiver = null,
        ExecutableCallSite? callSite = null,
        ReadOnlySpan<object?> operands = default,
        bool deferKeywordDefaults = false,
        int? storageCapacity = null)
    {
        // Values travel in plan layout order (positional, keyword-only, then
        // variadic names) with presence bits, instead of a per-call name
        // dictionary. The layout arrays live on the shared plan; only the value
        // slots are per-call.
        var storage = RentFunctionArgumentStorage(plan, span, context.MemoryGovernor, storageCapacity);
        try
        {
            return BindInto(storage, arguments, span, plan, context, deferKeywordDefaults, receiver, callSite, operands);
        }
        catch
        {
            // Arity failures after the rent must not strand the pooled box:
            // clear it and hand it back so the next bind reuses the array
            // instead of allocating (and retaining argument refs until GC).
            ReturnBoundArguments(storage);
            throw;
        }
    }

    internal static ValueTask<BoundCallArguments> BindFunctionArgumentsAsync(
        CallArgumentValue[] arguments, LythonSourceSpan span, FunctionBindingPlan plan, ExecutionContext context,
        object? receiver = null, ExecutableCallSite? callSite = null, ReadOnlySpan<object?> operands = default,
        int? storageCapacity = null)
    {
        var defer = plan.KeywordDefaultStorage is { HasProtocolKeys: true };
        // Consume borrowed operands before any default-key protocol can suspend.
        var bound = BindFunctionArguments(arguments, span, plan, context, receiver, callSite, operands, defer, storageCapacity);
        return defer ? CompleteProtocolDefaultsAsync(bound, span, plan, context) : new(bound);
    }

    // Eligible signatures contain only positional parameters. Read existing
    // operands once into owned bound storage, before a body can suspend.
    internal static BoundCallArguments BindPositionalFunctionArguments(
        ReadOnlySpan<object?> arguments, LythonSourceSpan span, FunctionBindingPlan plan, object? receiver = null,
        MemoryGovernor? governor = null, int? storageCapacity = null)
    {
        var storage = RentFunctionArgumentStorage(plan, span, governor, storageCapacity);
        var values = storage.Values;
        try
        {
            var offset = receiver is null ? 0 : 1;
            if (arguments.Length > plan.PositionalParameters.Count - offset)
                throw CallErrors.TooManyPositional(plan.CallableKind, plan.CallableName, span);
            var assigned = storage.Assigned;
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
            for (var i = arguments.Length + offset; i < assigned.Length; i++)
            {
                if (!plan.TryGetPositionalDefault(i, out var value))
                    throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, plan.PositionalParameters[i].Name, span);
                values[i] = value;
            }
            return storage with { Assigned = assigned };
        }
        catch
        {
            ReturnBoundArguments(storage);
            throw;
        }
    }

    private static async ValueTask<BoundCallArguments> CompleteProtocolDefaultsAsync(
        BoundCallArguments bound, LythonSourceSpan span, FunctionBindingPlan plan, ExecutionContext context)
    {
        var values = bound.Values;
        try
        {
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
            ReturnBoundArguments(bound);
            throw;
        }
    }

    // Post-rent binding body: every throw inside (too many positionals,
    // unexpected or duplicated keywords, missing arguments) funnels through
    // the catch in BindFunctionArguments, which returns the rented box.
    private static BoundCallArguments BindInto(
        BoundCallArguments storage,
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context,
        bool deferKeywordDefaults = false,
        object? receiver = null,
        ExecutableCallSite? callSite = null,
        ReadOnlySpan<object?> operands = default)
    {
        var values = storage.Values;
        var assigned = storage.Assigned;
        // The overflow list is scratch: most calls never spill positionals,
        // so materialize it only on the first spill under existing ownership.
        PyList? extraPositional = null;
        // Keyword overflow likewise materializes only for **kwargs functions
        // receiving unknown names; every other call skips the dictionary.
        Dictionary<string, object>? extraKeywords = null;
        var positionalIndex = 0;

        // A bound receiver follows exactly the same positional/overflow rules,
        // before supplied arguments. All values are consumed before any await.
        var argumentCount = callSite?.ArgumentCount ?? arguments.Length;
        for (var argumentIndex = receiver is null ? 0 : -1; argumentIndex < argumentCount; argumentIndex++)
        {
            var argument = argumentIndex < 0 ? CallArgumentValue.Positional(receiver!)
                : callSite is null ? arguments[argumentIndex]
                : CreateExecutableCallArgument(callSite.Arguments[argumentIndex], operands[argumentIndex].RequireNotNull());
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

        return storage with { Assigned = assigned };
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

    // Module frames contain only explicit bindings. An explicit alias to a
    // builtin still occupies a retained module slot, like any other binding.
    private static int CountOwnedModuleVariables(ExecutionContext context)
    {
        var owned = context.Frame.Variables.Count;

        // An unmirrored frame's namespace may be retained by a closure. Admit
        // its whole slot capacity before copying those bindings into Variables;
        // adding the existing entries is deliberately conservative.
        if (!context.MirrorsExecutableLocals && !context.MirrorLocalStores && context.CurrentExecutableFrame is { } frame)
            owned = checked(owned + frame.LocalCapacity);

        return owned;
    }

    internal static Dictionary<string, object> BuildDefaultArgumentMap(
        IReadOnlyList<LoweredFunctionParameter> parameters,
        Func<LoweredExpression, object> evaluate)
    {
        var defaultCount = parameters.Count(static parameter => parameter.DefaultValue is not null);
        var defaults = new Dictionary<string, object>(defaultCount, StringComparer.Ordinal);
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
        var defaultCount = parameters.Count(static parameter => parameter.DefaultValue is not null);
        var defaults = new Dictionary<string, object>(defaultCount, StringComparer.Ordinal);
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
            // Preserve literal text as a diagnostic fallback without invoking
            // argument protocols. The existing args renderer owns str/repr.
            var message = values is [PyString literal] ? literal.AsString() : string.Empty;
            var payload = values.Length == 0 ? PyNone.Instance : values.Length == 1 ? values[0] : args;
            var exception = PyException.CreateOwned(ExceptionIdentity, message, payload, context, span, args);
            exception.ArgsOverride = args;
            return exception;
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
