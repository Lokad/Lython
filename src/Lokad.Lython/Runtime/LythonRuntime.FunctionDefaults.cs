using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void StartExecutableDefaults(FunctionParameterLayout layout, Span<ExecutableSlot> operands,
        bool keywordsOnly, ExecutionContext context, LythonSourceSpan span)
    {
        var defaults = AllocateFunctionDefaults(layout, keywordsOnly, context, span);
        if (defaults.Positional.Reference is { } positional)
            operands[0] = defaults.InlinePositional
                ? ExecutableSlot.OwnPrivatePositionalDefault(positional) : ExecutableSlot.OwnPrivateDefault(positional);
        if (defaults.Keywords is { } keyword)
            operands[^1] = defaults.InlineKeyword
                ? ExecutableSlot.OwnPrivateKeywordDefault(keyword) : ExecutableSlot.OwnPrivateDefault(keyword);
    }

    // Admit the final backing, even when a singleton tuple is still implicit.
    private static FunctionDefaults AllocateFunctionDefaults(FunctionParameterLayout layout, bool keywordsOnly,
        ExecutionContext context, LythonSourceSpan span)
    {
        var positionalCount = keywordsOnly ? 0 : layout.PositionalDefaultCount;
        var keys = layout.PreparedKeywordDefaultKeys;
        var positionalBytes = positionalCount == 0 ? 0 : PyTuple.EstimateDefaultStorage(positionalCount);
        var keywordCapacity = keys?.PreparedValueCapacity ?? 0;
        var bytes = checked(positionalBytes + (keys is null ? 0 : PyDict.RecordKeys.RecordBytes(keywordCapacity)));
        var governor = context.MemoryGovernor;
        context.CheckExecution(span);
        governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            object? positional = positionalCount switch
            {
                0 => null,
                1 => PyNone.Instance,
                _ => PyTuple.CreateAdmittedDefaultStorage(positionalCount, governor, span, positionalBytes),
            };
            var inlineKeyword = keys?.Count == 1;
            object? keyword = keys is null ? null : inlineKeyword ? PyNone.Instance : new object?[keywordCapacity];
            governor.Commit(bytes);
            return new(ExecutableSlot.FromObject(positional), keyword, inlineKeyword, positionalCount == 1);
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    private static FunctionDefaults ReadExecutableDefaults(FunctionParameterLayout layout, ReadOnlySpan<ExecutableSlot> operands,
        bool directKeywordDefault = false)
    {
        System.Diagnostics.Debug.Assert(operands.Length == layout.DefaultStorageCount);
        var positional = layout.PositionalDefaultCount == 0 ? default : operands[0];
        var keyword = layout.HasKeywordDefaults ? operands[^1] : default;
        return new(ExecutableSlot.FromObject(positional.Reference),
            directKeywordDefault ? RuntimeValue(keyword.Object) : keyword.Reference,
            directKeywordDefault || keyword.HasPrivateKeywordDefault, positional.HasPrivatePositionalDefault);
    }

    private static object StoreInlinePositionalDefault(ExecutableSlot value, ExecutionState state, LythonSourceSpan span)
        => FunctionDefaults.PreparePositionalValue(value, state, span, out _).Object.RequireNotNull();

    private static void StorePreparedKeywordDefault(object?[] values, int index, ExecutableSlot value,
        ExecutionState state, LythonSourceSpan span)
    {
        state.CheckExecution(span);
        values[index] = RuntimeValue(value.Object);
    }

    private static void StoreFunctionDefault(ref FunctionDefaults defaults, int index, bool positional,
        ExecutableSlot value, ExecutionState state, LythonSourceSpan span)
    {
        if (positional)
        {
            if (defaults.InlinePositional)
                defaults = defaults with { Positional = FunctionDefaults.PreparePositionalValue(value, state, span, out _) };
            else ((PyTuple)defaults.Positional.Reference!).SetPrivateDefault(index, value, state, span);
        }
        else if (defaults.Keywords is object?[] values)
            StorePreparedKeywordDefault(values, index, value, state, span);
        else
        {
            state.CheckExecution(span);
            defaults = defaults with { Keywords = RuntimeValue(value.Object) };
        }
    }

    internal static FunctionBindingPlan CreateGeneratedBindingPlan(string name,
        IReadOnlyList<GeneratedFunctionParameter> parameters, ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = FunctionParameterLayout.EstimateBytes(parameters);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var layout = FunctionParameterLayout.Create(parameters, context.State.CancellationToken, span);
        var defaults = layout.Defaults.Length == 0 ? default : AllocateFunctionDefaults(layout, false, context, span);
        try
        {
            for (var i = 0; i < layout.Defaults.Length; i++)
            {
                var positional = i < layout.PositionalDefaultCount;
                var value = ExecutableSlot.FromObject(parameters[layout.Defaults[i].ParameterIndex].DefaultValue);
                StoreFunctionDefault(ref defaults, positional ? i : i - layout.PositionalDefaultCount,
                    positional, value, context.State, span);
            }
            var plan = new GeneratedFunctionBindingPlan(name, layout, defaults);
            storage.Commit(bytes);
            return plan;
        }
        catch { defaults.RefundUnpublished(context.MemoryGovernor); throw; }
    }

    private static ValueTask<FunctionDefaults> EvaluateFunctionDefaults(
        LoweredExpression[] defaults, FunctionParameterLayout layout,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        context.CheckExecution(span);
        return defaults.Length == 0 ? new(default(FunctionDefaults))
            : EvaluateDefaultsAsync(defaults, layout, context, span, asynchronous);
    }

    private static async ValueTask<FunctionDefaults> EvaluateDefaultsAsync(
        LoweredExpression[] defaults, FunctionParameterLayout layout,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var prepared = AllocateFunctionDefaults(layout, false, context, span);
        try
        {
            for (var i = 0; i < defaults.Length; i++)
            {
                var expression = defaults[i];
                context.CheckExecution(expression.Span);
                var value = asynchronous ? await EvaluateLoweredExpressionAsync(expression, context).ConfigureAwait(false)
                    : EvaluateLoweredExpression(expression, context);
                var positional = i < layout.PositionalDefaultCount;
                StoreFunctionDefault(ref prepared, positional ? i : i - layout.PositionalDefaultCount,
                    positional, ExecutableSlot.FromObject(value), context.State, expression.Span);
            }
            return prepared;
        }
        catch { prepared.RefundUnpublished(context.MemoryGovernor); throw; }
    }

    private static PyFunctionBase CreateLambdaWithUnpublishedDefaults(ExecutableCreateFunction binding,
        FunctionDefaults defaults, ExecutionContext context, LythonSourceSpan span)
    {
        try { return CreateLambdaFunction(binding, defaults, context, span); }
        catch { defaults.RefundUnpublished(context.MemoryGovernor); throw; }
    }
}
