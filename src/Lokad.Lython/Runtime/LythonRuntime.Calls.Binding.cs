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
        var bound = new Dictionary<string, object>(StringComparer.Ordinal);
        // The overflow list is scratch: most calls never spill positionals,
        // so materialize it only on the first spill instead of charging every
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

                    extraPositional ??= new PyList([], context.MemoryGovernor, span);
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
            bound[plan.VariadicList.Name] = overflow is null
                ? CreateTuple(0, _ => PyNone.Instance, context, span)
                : CreateTuple(overflow.Count, i => overflow[i], context, span);
        }

        if (plan.VariadicDictionary is not null)
        {
            var keywordDict = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in extraKeywords)
            {
                keywordDict.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value);
            }

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

    // A function value retains its defining context for the run: the context,
    // frame and variable tables plus one slot per captured entry. Module-level
    // definitions share the run-rooted module frame, so only nested definitions
    // pay. The function CLR wrapper itself stays MG04-owned; shared frames may
    // pay once per definition.
    private const long ClosureContextBaseBytes = 512;
    private const long ClosureCellSlotBytes = 32;

    internal static void ChargeClosureRetention(ExecutionContext? closure, int capturedCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (closure is null || governor is null || closure.Frame.Parent is null)
        {
            return;
        }

        var bytes = checked(ClosureContextBaseBytes + ClosureCellSlotBytes * (long)Math.Max(capturedCount, 0));
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
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

    private sealed class ExceptionTypeValue : ICallable, IPyDynamicAttributes, IPyRenderableValue, IPythonExceptionType, IEquatable<ExceptionTypeValue>
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
