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
    // The function invocation owns this buffer through default lookup and body
    // completion. Values/cells/snapshots have their own retained ownership.
    internal static void ReturnBoundArguments(BoundFunctionArguments bound)
        => ReturnBoundArguments(bound, bound.ParameterCount);

    internal static void ReturnBoundArguments(BoundFunctionArguments bound, int usedLength)
        => ReturnBoundArguments(bound.Values, bound.StorageOwner, usedLength);

    private static void ReturnBoundArguments(ExecutableSlot[] values, MemoryGovernor? storageOwner, int usedLength)
    {
        if (storageOwner is not null) storageOwner.ReturnFunctionWorkspace(values, usedLength);
        else Array.Clear(values);
    }

    internal static BoundFunctionArguments RentFunctionArgumentStorage(
        FunctionBindingPlan plan, LythonSourceSpan span, ExecutionContext context, int? storageCapacity)
    {
        var governor = context.MemoryGovernor;
        var parameterCount = plan.ParameterCount;
        var capacity = storageCapacity ?? parameterCount;
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, parameterCount);
        // One execution-owned cleared workspace covers final capacity and metadata.
        // Active callers retain it through cleanup; only returned idle storage
        // can be reused, including after suspension.
        var values = governor.RentFunctionWorkspace(capacity, span, context);
        return new BoundFunctionArguments(values, parameterCount, governor);
    }

    internal static BoundFunctionArguments BindFunctionArguments(
        ReadOnlySpan<CallArgumentValue> arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context,
        object? receiver = null,
        ExecutableCallSite? callSite = null,
        ReadOnlySpan<ExecutableSlot> operands = default,
        bool deferProtocolDefaults = false,
        int? storageCapacity = null)
    {
        // Values travel in plan layout order (positional, keyword-only, then
        // variadic names). The layout lives on the shared plan; the final value
        // slots use their existing null sentinel to distinguish omission from None.
        // No separate presence map is constructed or carried into execution.
        var storage = RentFunctionArgumentStorage(plan, span, context, storageCapacity);
        try
        {
            BindFunctionArgumentsInto(storage.Values, arguments, span, plan, context,
                deferProtocolDefaults, receiver, callSite, operands);
            return storage;
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

    internal static ValueTask<BoundFunctionArguments> BindFunctionArgumentsAsync(
        ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span, FunctionBindingPlan plan, ExecutionContext context,
        object? receiver = null, ExecutableCallSite? callSite = null, ReadOnlySpan<ExecutableSlot> operands = default,
        int? storageCapacity = null)
    {
        // Admission and supplied-argument construction can reenter and replace
        // defaults. Inspect their live storage at lookup, after owning borrowed
        // operands, rather than saving a pre-admission protocol decision.
        var bound = BindFunctionArguments(arguments, span, plan, context, receiver, callSite, operands,
            deferProtocolDefaults: true, storageCapacity: storageCapacity);
        for (var k = 0; k < plan.KeywordOnlyCount; k++)
            if (bound.Values[plan.PositionalCount + k].IsUnassigned)
                return CompleteProtocolDefaultsAsync(bound, span, plan, context);
        return new(bound);
    }

    private static async ValueTask<BoundFunctionArguments> CompleteProtocolDefaultsAsync(
        BoundFunctionArguments bound, LythonSourceSpan span, FunctionBindingPlan plan, ExecutionContext context)
    {
        var values = bound.Values;
        try
        {
            for (var k = 0; k < plan.KeywordOnlyCount; k++)
            {
                var index = plan.PositionalCount + k;
                if (!values[index].IsUnassigned) continue;
                var defaults = plan.KeywordDefaultStorage;
                if (defaults is null)
                    throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, plan.NameAt(plan.PositionalCount + k), span);
                var value = await defaults.TryGetKeyAsync(plan.KeywordDefaultName(k), context, span).ConfigureAwait(false);
                if (!value.Found)
                    throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, plan.NameAt(plan.PositionalCount + k), span);
                values[index].Object = value.Value;
            }
            return bound;
        }
        catch
        {
            ReturnBoundArguments(bound);
            throw;
        }
    }

    // One binding fill serves an admitted workspace or a synchronous caller's
    // borrowed destination. The caller owns storage/rollback; this routine
    // retains ordinary live-default and Python argument/error semantics.
    internal static void BindFunctionArgumentsInto(
        Span<ExecutableSlot> values,
        ReadOnlySpan<CallArgumentValue> arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context,
        bool deferProtocolDefaults = false,
        object? receiver = null,
        ExecutableCallSite? callSite = null,
        ReadOnlySpan<ExecutableSlot> operands = default)
    {
        var firstExtraPositional = 0;
        var extraPositionalCount = 0;
        PyDict? extraKeywords = null;
        long syntheticKeyBytes = 0;
        try
        {
            var positionalIndex = 0;

            // A flat operand call already supplies its argument shape. Copy its
            // positional prefix directly into the final slots, then share the same
            // live-default/variadic completion as every other call. No temporary
            // CallArgumentValue or second positional binding implementation is needed.
            var flatPositional = plan.VariadicListIndex < 0 &&
                (callSite?.IsPositionalOnly ?? arguments.IsEmpty);
            if (flatPositional)
            {
                var receiverCount = receiver is null ? 0 : 1;
                if (operands.Length > plan.PositionalCount - receiverCount)
                    throw CallErrors.TooManyPositional(plan.CallableKind, plan.CallableName, span);
                if (receiver is not null) values[positionalIndex++].Object = receiver;
                for (var i = 0; i < operands.Length; i++)
                {
                    if (operands[i].IsUnassigned) _ = operands[i].Object.RequireNotNull();
                    values[positionalIndex++] = operands[i];
                }
            }
            else BindSuppliedFunctionArguments(values, arguments, span, plan, context,
                receiver, callSite, operands, out firstExtraPositional, out extraPositionalCount, ref extraKeywords, ref syntheticKeyBytes);

            for (var i = 0; i < plan.PositionalCount; i++)
            {
                if (!values[i].IsUnassigned)
                {
                    continue;
                }

                var parameterName = plan.NameAt(i);
                if (plan.TryGetPositionalDefault(i, out var defaultValue))
                {
                    values[i].Object = defaultValue;
                    continue;
                }

                throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameterName, span);
            }

            for (var k = 0; k < plan.KeywordOnlyCount; k++)
            {
                var i = plan.PositionalCount + k;
                if (!values[i].IsUnassigned)
                {
                    continue;
                }

                var parameterName = plan.NameAt(plan.PositionalCount + k);
                var keywordDefaults = plan.KeywordDefaultStorage;
                // Exact native dictionaries finish synchronously. A protocol lookup
                // defers this and the remaining defaults in their original order.
                if (deferProtocolDefaults && keywordDefaults is { HasProtocolKeys: true }) break;
                if (keywordDefaults is not null && keywordDefaults.TryGetValue(plan.KeywordDefaultName(k), out var defaultValue, context, span))
                {
                    values[i].Object = defaultValue;
                    continue;
                }

                throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameterName, span);
            }

            if (plan.VariadicDictionaryIndex >= 0)
            {
                extraKeywords ??= new PyDict(context.MemoryGovernor, span);
                context.CheckExecution(span);
            }
            if (plan.VariadicListIndex >= 0)
            {
                values[plan.VariadicListIndex].Object = extraPositionalCount == 0 ? PyTuple.Empty
                    : CreateVariadicPositionalTuple(arguments, operands, callSite, receiver,
                        firstExtraPositional, extraPositionalCount, context, span);
            }

            if (extraKeywords is not null)
            {
                values[plan.VariadicDictionaryIndex].Object = extraKeywords;
                extraKeywords = null;
            }
        }
        finally
        {
            // Only the unexposed dictionary and labels created by this bind
            // can be refunded here. Expanded keys have their original owners.
            if (extraKeywords is not null)
            {
                try { extraKeywords.DiscardUnpublishedStorage(); }
                finally { context.MemoryGovernor.Release(syntheticKeyBytes); }
            }
        }
    }

    // Named and variadic calls interpret their supplied forms here. Ordinary
    // flat calls keep their direct fill and live-default completion together;
    // the shared binder need not inline this larger dynamic parsing branch.
    private static void BindSuppliedFunctionArguments(Span<ExecutableSlot> values,
        ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span, FunctionBindingPlan plan,
        ExecutionContext context, object? receiver, ExecutableCallSite? callSite,
        ReadOnlySpan<ExecutableSlot> operands, out int firstExtraPositional, out int extraPositionalCount,
        ref PyDict? extraKeywords, ref long syntheticKeyBytes)
    {
        firstExtraPositional = 0;
        extraPositionalCount = 0;
        var positionalIndex = 0;
        // A bound receiver follows exactly the same positional/overflow rules,
        // before supplied arguments. All values are consumed before any await.
        var argumentCount = callSite?.ArgumentCount ?? (arguments.IsEmpty ? operands.Length : arguments.Length);
        for (var argumentIndex = receiver is null ? 0 : -1; argumentIndex < argumentCount; argumentIndex++)
        {
            if ((argumentIndex & 1023) == 0) context.CheckExecution(span);
            // The supplied operands stay owned through binding. Remember only
            // the extent of the spill until argument and default checks pass.
            if (plan.VariadicListIndex >= 0 && positionalIndex == plan.PositionalCount &&
                IsSuppliedPositional(argumentIndex, arguments, callSite))
            {
                if (extraPositionalCount == 0) firstExtraPositional = argumentIndex;
                extraPositionalCount++;
                continue;
            }

            var argument = argumentIndex < 0 ? CallArgumentValue.Positional(receiver!)
                : callSite is null ? arguments.IsEmpty
                    ? CallArgumentValue.Positional(operands[argumentIndex].Object.RequireNotNull()) : arguments[argumentIndex]
                : CreateExecutableCallArgument(callSite.Arguments[argumentIndex], operands[argumentIndex].Object.RequireNotNull());
            if (argument.IsPositional)
            {
                if (positionalIndex >= plan.PositionalCount)
                    throw CallErrors.TooManyPositional(plan.CallableKind, plan.CallableName, span);

                // Expanded calls can supply a keyword before its starred
                // positional value. Neither order may overwrite a prior binding.
                if (!values[positionalIndex].IsUnassigned)
                    throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, plan.NameAt(positionalIndex), span);
                values[positionalIndex].Object = argument.Value;
                positionalIndex++;
                continue;
            }

            if (!plan.TryGetKeywordIndex(argument, context, span, out var keywordIndex))
            {
                if (plan.VariadicDictionaryIndex < 0)
                {
                    throw CallErrors.UnexpectedKeyword(plan.CallableKind, plan.CallableName, argument.GetKeywordName(context, span), span);
                }

                var governor = context.MemoryGovernor;
                extraKeywords ??= new PyDict(governor, span);
                var key = argument.OriginalKeyword;
                if (key is null)
                {
                    key = PyString.FromString(argument.KeywordName, governor, span);
                    if (ReferenceEquals(key.OwnerMemoryGovernor, governor))
                        syntheticKeyBytes = checked(syntheticKeyBytes + key.CommittedOwnedBytes);
                }
                if (!extraKeywords.TryAddNativeString(key, argument.Value, context, span))
                    throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, argument.GetKeywordName(context, span), span);

                continue;
            }

            if (!values[keywordIndex].IsUnassigned)
            {
                throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, argument.GetKeywordName(context, span), span);
            }

            values[keywordIndex].Object = argument.Value;
        }

    }

    private static bool IsSuppliedPositional(int index, ReadOnlySpan<CallArgumentValue> arguments, ExecutableCallSite? callSite)
        => index < 0 || (callSite is null
            ? arguments.IsEmpty || arguments[index].IsPositional
            : callSite.Arguments[index].Kind == CallArgumentKind.Positional);

    private static PyTuple CreateVariadicPositionalTuple(ReadOnlySpan<CallArgumentValue> arguments,
        ReadOnlySpan<ExecutableSlot> operands, ExecutableCallSite? callSite, object? receiver,
        int first, int count, ExecutionContext context, LythonSourceSpan span)
    {
        var governor = context.MemoryGovernor;
        using var storage = governor.ReserveTemporary(PyTuple.EstimateApproximateBytes(count), span);
        context.CheckExecution(span);
        var items = new object[count];
        var written = 0;
        for (var index = first; written < items.Length; index++)
        {
            if ((index & 1023) == 0) context.CheckExecution(span);
            if (!IsSuppliedPositional(index, arguments, callSite)) continue;
            items[written++] = index < 0 ? receiver.RequireNotNull()
                : (callSite is not null || arguments.IsEmpty ? operands[index].Object.RequireNotNull() : arguments[index].Value);
        }
        return PyTuple.FromReservedOwnedArray(items, governor, span, storage);
    }

    // Constructed function objects retain a wrapper plus binding plan and
    // default containers per evaluation. Defs and lambdas execute per evaluation,
    // so each constructed value charges once built.
    private const long FunctionValueBytes = 128;

    // Constructed class objects retain a type record plus one namespace slot per
    // member; admit the base plus per-member slots before construction. Member values
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

    internal sealed class ExceptionTypeValue : ICallable, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyRenderableValue, IPythonExceptionType, IPyHashableValue, IEquatable<ExceptionTypeValue>
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
            if (context.State.TryReadExceptionBases(ExceptionIdentity, out var cached))
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
                        : TryGetNativeModuleType(context, identity.ModuleName, identity.TypeName)
                            ?? throw new InvalidOperationException("Missing module exception type.");
                }
            var tuple = new PyTuple(resolved, context.MemoryGovernor, span);
            try { context.State.CacheExceptionBases(ExceptionIdentity, tuple, span); }
            catch
            {
                context.State.MemoryGovernor.RefundUnpublishedValue(tuple);
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

        public int GetPyHashCode() => GetHashCode();

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (ExceptionIdentity == ModuleException("re", "PatternError"))
                return CreatePatternErrorInstanceAsync(arguments, span, context, false).GetAwaiter().GetResult();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", $"{TypeName}(message) does not accept keyword arguments.", span);
                }
            }

            var args = PyTuple.FromPositionalArguments(arguments, context.MemoryGovernor, span, context);
            var payload = args.Count == 0 ? PyNone.Instance : args.Count == 1 ? args[0] : args;
            var exception = PyException.CreateOwned(ExceptionIdentity, string.Empty, payload, context, span, args);
            return exception;
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ExceptionIdentity == ModuleException("re", "PatternError")
                ? CreatePatternErrorInstanceAsync(arguments, span, context, true)
                : new(Invoke(arguments, span, context));
    }

}
