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
        var extraPositional = new PyList([], context.MemoryGovernor, span);
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
            bound[plan.VariadicList.Name] = CreateTuple(extraPositional.Count, i => extraPositional[i], context, span);
        }

        if (plan.VariadicDictionary is not null)
        {
            var keywordDict = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in extraKeywords)
            {
                keywordDict.SetItem(PyString.FromString(pair.Key), pair.Value);
            }

            bound[plan.VariadicDictionary.Name] = keywordDict;
        }

        return bound;
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

    private sealed class LambdaFunction : ICallable
    {
        private readonly LoweredExpression _body;
        private readonly FunctionBindingPlan _bindingPlan;
        private readonly ExecutionContext _closure;

        public LambdaFunction(IReadOnlyList<LoweredFunctionParameter> parameters, LoweredExpression body, ExecutionContext closure, Dictionary<string, object> defaultValues)
        {
            _body = body;
            _closure = closure;
            _bindingPlan = new FunctionBindingPlan("<lambda>", PythonCallableKind.Lambda, parameters, defaultValues);
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

    private sealed class ExceptionTypeValue : ICallable, IPyDynamicAttributes, IPyRenderableValue, IEquatable<ExceptionTypeValue>
    {
        public ExceptionTypeValue(string typeName)
        {
            TypeName = typeName;
        }

        public string TypeName { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString(TypeName),
                "type" => PyString.FromString(TypeName),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class '" + TypeName + "'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public bool Equals(ExceptionTypeValue? other)
            => other is not null && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is ExceptionTypeValue other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TypeName);

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
                return new PyException(TypeName, FormatSystemExitMessage(value), value);
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
            return new PyException(TypeName, message, payload, args);
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
