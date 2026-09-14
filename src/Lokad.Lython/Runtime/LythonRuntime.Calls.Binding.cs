using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static Dictionary<string, object> BindFunctionArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        FunctionBindingPlan plan,
        ExecutionContext context)
    {
        context.State.NoteBoundCall();
        var pool = context.State.CallTemporaries;
        var bound = new Dictionary<string, object>(StringComparer.Ordinal);
        // The overflow list is scratch: most calls never spill positionals,
        // so materialize it only on the first spill, tracked in the call pool
        // invocation for a list that is dropped before return.
        PyList? extraPositional = null;
        var extraKeywords = new Dictionary<string, object>(StringComparer.Ordinal);
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
                        pool.TrackMutable(extraPositional, extraPositional.CommittedStorageBytes);
                    }

                    extraPositional.Add(argument.Value);
                    continue;
                }

                var parameter = plan.PositionalParameters[positionalIndex++];
                bound[parameter.Name] = argument.Value;
                continue;
            }

            var keywordName = argument.KeywordName;
            if (!plan.NamedParameters.TryGetValue(keywordName, out var named))
            {
                if (plan.VariadicDictionary is null)
                {
                    throw CallErrors.UnexpectedKeyword(plan.CallableKind, plan.CallableName, keywordName, span);
                }

                if (!extraKeywords.TryAdd(keywordName, argument.Value))
                {
                    throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, keywordName, span);
                }

                continue;
            }

            if (!bound.TryAdd(named.Name, argument.Value))
            {
                throw CallErrors.MultipleValues(plan.CallableKind, plan.CallableName, keywordName, span);
            }
        }

        foreach (var parameter in plan.PositionalParameters)
        {
            if (bound.ContainsKey(parameter.Name))
            {
                continue;
            }

            if (plan.DefaultValues.TryGetValue(parameter.Name, out var defaultValue))
            {
                bound[parameter.Name] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameter.Name, span);
        }

        foreach (var parameter in plan.KeywordOnlyParameters)
        {
            if (bound.ContainsKey(parameter.Name))
            {
                continue;
            }

            if (plan.DefaultValues.TryGetValue(parameter.Name, out var defaultValue))
            {
                bound[parameter.Name] = defaultValue;
                continue;
            }

            throw CallErrors.MissingArgument(plan.CallableKind, plan.CallableName, parameter.Name, span);
        }

        if (plan.VariadicList is not null)
        {
            var overflow = extraPositional;
            if (overflow is null || overflow.Count == 0)
            {
                bound[plan.VariadicList.Name] = CreateTuple(0, _ => PyNone.Instance, context, span);
            }
            else
            {
                ChargeReclamationPool.NotifyStorageReplaced(overflow, overflow.CommittedStorageBytes);
                var overflowTuple = CreateTuple(overflow.Count, i => overflow[i], context, span);
                pool.TrackMutable(overflowTuple, overflowTuple.CommittedStorageBytes);
                bound[plan.VariadicList.Name] = overflowTuple;
            }
        }

        if (plan.VariadicDictionary is not null)
        {
            var keywordDict = new PyDict(context.MemoryGovernor, span);
            pool.TrackMutable(keywordDict, keywordDict.CommittedStorageBytes);
            foreach (var pair in extraKeywords)
            {
                var keyword = PyString.FromString(pair.Key, context.MemoryGovernor, span);
                pool.TrackString(keyword);
                keywordDict.SetItem(keyword, pair.Value);
            }

            ChargeReclamationPool.NotifyStorageReplaced(keywordDict, keywordDict.CommittedStorageBytes);
            bound[plan.VariadicDictionary.Name] = keywordDict;
        }

        return bound;
    }

    // Constructed function objects retain a wrapper plus binding plan and
    // default map per evaluation. Defs and lambdas execute per evaluation,
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

    // MG11: default-argument maps survive with the function value. The 128B
    // constructed-value unit covers the wrapper, binding plan and an empty map,
    // so each defaulted parameter owns one 64B table slot beside it.
    private const long DefaultArgumentSlotBytes = 64;

    internal static void ChargeDefaultArguments(int defaultCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (defaultCount <= 0 || governor is null)
        {
            return;
        }

        var bytes = checked(DefaultArgumentSlotBytes * (long)defaultCount);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
    }

    // A function value retains its defining context chain for as long as the
    // function is retained: intermediate invocation contexts plus the variable
    // tables mirroring their locals stay alive through the closure, so each
    // executed def or lambda owns the so-far-unowned storage of every context
    // it retains, module frames included (imported modules keep per-module
    // scopes alive through their functions). Shared frames pay once (first-wins
    // aliasing), with later definitions paying only for variables bound since.
    // The function CLR wrapper itself stays MG04-owned.
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
            var owned = current.Frame.Parent is null
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

    // MG22: a constructed PyFunction retains its lowered body graph (lowered
    // statements plus their syntax and constant nodes) for as long as the
    // function is retained, even when the body never executes. Module slots,
    // scope tables, and source text do not cover that graph, so each distinct body
    // retained through an import owns a deep statement count at a conservative
    // per-node rate once per run (first-wins over aliases and re-executed sites).
    // PyExecutableFunction shares host-owned precompiled code objects and
    // stays outside this charge, like reused compiled scripts. The count walks
    // every nested statement list, including deferred nested def/class bodies,
    // so an uncalled outer function cannot hide an inner one; a nested body
    // that later executes pays again beside its outer walk (conservative).
    // Single-expression retention (lambdas, one deeply nested expression) is
    // bounded by the source and nesting input limits, not by this count.
    private const long RetainedCodeStatementBytes = 64;

    internal static void ChargeRetainedCode(
        IReadOnlyList<LoweredStatement> body,
        ExecutionContext? context,
        MemoryGovernor? governor,
        LythonSourceSpan? span)
    {
        if (body.Count == 0 || governor is null)
        {
            return;
        }

        if (context is not null && !IsImportRetainedCode(context))
        {
            return;
        }

        var state = context?.Services.State;
        if (state is not null && state.IsCodeBodyCharged(body))
        {
            return;
        }

        var bytes = checked(CountRetainedStatements(body) * RetainedCodeStatementBytes);
        // Reserve before marking: a denied reservation leaves the body unmarked
        // so a caught failure followed by a funded retry still pays for it.
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
        state?.MarkCodeBodyCharged(body);
    }

    // Only code retained through imports is owned per run. Definitions rooted
    // at the host-provided entry script (__main__) alias the reusable compiled
    // script (lowered bodies) or shared precompiled images (executable
    // functions), so per-run body ownership would charge host-owned state and
    // reshuffle host-calibrated budgets; a missing __name__ stays conservative
    // and pays. Import roots keep per-import lowered graphs that die with the
    // run, so every definition chained to one owns its distinct body.
    internal static bool IsImportRetainedCode(ExecutionContext? context)
    {
        var current = context;
        while (current?.ParentContext is not null)
        {
            current = current.ParentContext;
        }

        if (current?.Frame.Variables.TryGetValue("__name__", out var name) == true &&
            name is PyString module &&
            string.Equals(module.AsString(), "__main__", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    internal static long CountRetainedStatements(IReadOnlyList<LoweredStatement> body)
    {
        var count = 0L;
        var pending = new Stack<IReadOnlyList<LoweredStatement>>();
        pending.Push(body);
        while (pending.Count > 0)
        {
            var list = pending.Pop();
            for (var i = 0; i < list.Count; i++)
            {
                count++;
                switch (list[i])
                {
                    case LoweredFunctionDefinitionStatement functionDefinition:
                        pending.Push(functionDefinition.Body);
                        break;
                    case LoweredClassDefinitionStatement classDefinition:
                        pending.Push(classDefinition.Body);
                        break;
                    case LoweredIfStatement ifStatement:
                        if (ifStatement.ElseStatements is not null)
                        {
                            pending.Push(ifStatement.ElseStatements);
                        }

                        pending.Push(ifStatement.ThenStatements);
                        break;
                    case LoweredForStatement forStatement:
                        if (forStatement.ElseStatements is not null)
                        {
                            pending.Push(forStatement.ElseStatements);
                        }

                        pending.Push(forStatement.Body);
                        break;
                    case LoweredWhileStatement whileStatement:
                        if (whileStatement.ElseStatements is not null)
                        {
                            pending.Push(whileStatement.ElseStatements);
                        }

                        pending.Push(whileStatement.Body);
                        break;
                    case LoweredMatchStatement matchStatement:
                        foreach (var matchCase in matchStatement.Cases)
                        {
                            pending.Push(matchCase.Body);
                        }

                        break;
                    case LoweredWithStatement withStatement:
                        pending.Push(withStatement.Body);
                        break;
                    case LoweredTryStatement tryStatement:
                        if (tryStatement.FinallyBody is not null)
                        {
                            pending.Push(tryStatement.FinallyBody);
                        }

                        if (tryStatement.ElseBody is not null)
                        {
                            pending.Push(tryStatement.ElseBody);
                        }

                        foreach (var exceptClause in tryStatement.ExceptClauses)
                        {
                            pending.Push(exceptClause.Body);
                        }

                        pending.Push(tryStatement.TryBody);
                        break;
                    default:
                        break;
                }
            }
        }

        return count;
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

    private sealed class LambdaFunction : ICallable, IPyDynamicAttributes
    {
        // The lambda name is fixed vocabulary shared across all instances,
        // so reads alias stably like CPython instead of rebuilding per read.
        private static readonly PyString LambdaName = PyString.FromString("<lambda>");

        private readonly LoweredExpression _body;
        private readonly FunctionBindingPlan _bindingPlan;
        private readonly ExecutionContext _closure;

        public LambdaFunction(IReadOnlyList<LoweredFunctionParameter> parameters, LoweredExpression body, ExecutionContext closure, Dictionary<string, object> defaultValues)
        {
            _body = body;
            _closure = closure;
            _bindingPlan = new FunctionBindingPlan("<lambda>", PythonCallableKind.Lambda, parameters, defaultValues);
        }

        // Lambdas report CPython-style names from their binding plan and defining
        // scope: <lambda>, the nested qualname and the defining module.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = LambdaName;
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyFunctionBinding.EnclosingFunctionPath(_closure) is { } path
                    ? PyString.FromString(path + ".<locals>.<lambda>")
                    : LambdaName;
                return true;
            }

            // Lambdas never carry docstrings, so __doc__ reports None like CPython.
            if (name == "__doc__")
            {
                value = PyNone.Instance;
                return true;
            }

            if (name == "__module__" && PyFunctionBase.TryGetModuleName(_closure, out var moduleName))
            {
                value = moduleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var boundArguments = BindFunctionArguments(arguments, span, _bindingPlan, context);

            var frame = PyFunctionBinding.EnterInvocationFrame(
                _closure,
                ScopeDirectiveFacts.Empty,
                _bindingPlan,
                boundArguments,
                mirrorBoundArguments: true,
                ownerType: null,
                span);
            try
            {
                return EvaluateLoweredExpression(_body, frame);
            }
            catch (LythonRuntimeException ex)
            {
                PyFunctionBinding.AnnotateException(ex, frame, "<lambda>", span);
                throw;
            }
            finally
            {
                frame.LeaveFunctionCall();
            }
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var boundArguments = BindFunctionArguments(arguments, span, _bindingPlan, context);

            var frame = PyFunctionBinding.EnterInvocationFrame(
                _closure,
                ScopeDirectiveFacts.Empty,
                _bindingPlan,
                boundArguments,
                mirrorBoundArguments: true,
                ownerType: null,
                span);
            try
            {
                return await EvaluateLoweredExpressionAsync(_body, frame).ConfigureAwait(false);
            }
            catch (LythonRuntimeException ex)
            {
                PyFunctionBinding.AnnotateException(ex, frame, "<lambda>", span);
                throw;
            }
            finally
            {
                frame.LeaveFunctionCall();
            }
        }
    }

    internal sealed class ExceptionTypeValue : ICallable, IPyDynamicAttributes, IPyRenderableValue, IPythonExceptionType, IEquatable<ExceptionTypeValue>
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
        private static readonly PyString HashlibModuleName = PyString.FromString("hashlib");
        private static readonly PyString ImportlibModuleName = PyString.FromString("importlib");
        private static readonly PyString ItertoolsModuleName = PyString.FromString("itertools");
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
            "hashlib" => HashlibModuleName,
            "importlib" => ImportlibModuleName,
            "itertools" => ItertoolsModuleName,
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

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString(TypeName),
                "__module__" => SharedModuleLabel(ExceptionIdentity.ModuleName),
                "type" => PyString.FromString(TypeName),
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
            context.CheckExecutionBudget(span);
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
                return new PyException(ExceptionIdentity, FormatSystemExitMessage(value), value);
            }

            var values = arguments.Select(argument => argument.Value).ToArray();
            var args = new PyTuple(values, context.MemoryGovernor, span);
            var message = values.Length switch
            {
                0 => string.Empty,
                // Like CPython, a single KeyError argument renders through
                // repr instead of str; every other single argument uses str.
                1 when ExceptionIdentity.IsBuiltin && string.Equals(TypeName, "KeyError", StringComparison.Ordinal) =>
                    PyRendering.ToReprPyString(values[0], new PyRenderingContext(context)).AsString(),
                1 => PyRendering.ToInterpolatedString(values[0], new PyRenderingContext(context)),
                _ => PyRendering.ToReprPyString(args, new PyRenderingContext(context)).AsString(),
            };
            var payload = values.Length == 0 ? PyNone.Instance : values.Length == 1 ? values[0] : args;
            return new PyException(ExceptionIdentity, message, payload, args);
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
