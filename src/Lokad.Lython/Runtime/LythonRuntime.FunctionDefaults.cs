using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static FunctionDefaults AllocateFunctionDefaults(FunctionParameterLayout layout, ExecutionContext context,
        LythonSourceSpan span, bool inlineSingleKeyword = false)
    {
        context.CheckExecution(span);
        var positional = layout.PositionalDefaultCount == 0
            ? null : PyTuple.CreateDefaultStorage(layout.PositionalDefaultCount, context, span);
        try
        {
            var keywordCount = layout.Defaults.Length - layout.PositionalDefaultCount;
            if (inlineSingleKeyword && keywordCount == 1)
            {
                AdmitInlineKeywordDefault(context, span);
                return new(positional, PyNone.Instance, InlineKeyword: true);
            }
            object? keywords = keywordCount == 0 ? null : layout.PreparedKeywordDefaultKeys is { } keys
                ? keys.CreatePreparedValues(context, span) : new PyDict(keywordCount, context.MemoryGovernor, span);
            return new(positional, keywords);
        }
        catch
        {
            if (positional is not null) context.MemoryGovernor.RefundUnpublishedValue(positional);
            throw;
        }
    }

    // Admit both final containers before the first expression. Only after
    // allocation succeeds do the existing work slots take responsibility.
    private static void StartExecutableDefaults(FunctionParameterLayout layout, Span<ExecutableSlot> operands,
        bool keywordsOnly, ExecutionContext context, LythonSourceSpan span)
    {
        if (keywordsOnly)
        {
            operands[0] = PrepareExecutableKeywordDefaults(layout, context, span);
            return;
        }

        var defaults = AllocateFunctionDefaults(layout, context, span, inlineSingleKeyword: true);
        var next = 0;
        if (defaults.Positional is { } positional) operands[next++] = ExecutableSlot.OwnPrivateDefault(positional);
        if (defaults.Keywords is { } keywords)
            operands[next] = defaults.InlineKeyword ? ExecutableSlot.OwnPrivateKeywordDefault(keywords)
                : ExecutableSlot.OwnPrivateDefault(keywords);
    }

    private static ExecutableSlot PrepareExecutableKeywordDefaults(FunctionParameterLayout layout,
        ExecutionContext context, LythonSourceSpan span)
    {
        var keys = layout.PreparedKeywordDefaultKeys.RequireNotNull();
        if (keys.Count == 1)
        {
            AdmitInlineKeywordDefault(context, span);
            return ExecutableSlot.OwnPrivateKeywordDefault(PyNone.Instance);
        }
        return ExecutableSlot.OwnPrivateDefault(keys.CreatePreparedValues(context, span));
    }

    private static void AdmitInlineKeywordDefault(ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = PyDict.RecordKeys.SinglePreparedValueBytes;
        var governor = context.MemoryGovernor;
        governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            governor.Commit(bytes);
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    private static FunctionDefaults ReadExecutableDefaults(FunctionParameterLayout layout, ReadOnlySpan<ExecutableSlot> operands,
        bool directKeywordDefault = false)
    {
        System.Diagnostics.Debug.Assert(operands.Length == layout.DefaultStorageCount);
        var keyword = layout.HasKeywordDefaults ? operands[^1] : default;
        return new(layout.PositionalDefaultCount == 0 ? null : (PyTuple)operands[0].Reference!,
            directKeywordDefault ? RuntimeValue(keyword.Object) : keyword.Reference,
            directKeywordDefault || keyword.HasPrivateKeywordDefault);
    }

    private static void StorePreparedKeywordDefault(object?[] values, int index, ExecutableSlot value,
        ExecutionState state, LythonSourceSpan span)
    {
        state.CheckExecution(span);
        values[index] = RuntimeValue(value.Object);
    }

    private static void AddFunctionDefault(FunctionDefaults defaults, FunctionParameterLayout layout,
        FunctionParameterLayout.DefaultSlot slot, ref int positional, ExecutableSlot value, ExecutionContext context,
        LythonSourceSpan span)
    {
        if (slot.KeywordIndex < 0) defaults.Positional!.SetPrivateDefault(positional++, value, context.State, span);
        else if (defaults.Keywords is object?[] values)
            StorePreparedKeywordDefault(values, layout.KeywordDefaultAt(slot.KeywordIndex).RecordPosition, value, context.State, span);
        else ((PyDict)defaults.Keywords!).AddDefinitionValue(layout.KeywordDefaultName(slot.KeywordIndex), RuntimeValue(value.Object), context, span);
    }

    internal static FunctionBindingPlan CreateGeneratedBindingPlan(string name,
        IReadOnlyList<GeneratedFunctionParameter> parameters, ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = FunctionParameterLayout.EstimateBytes(parameters);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var layout = FunctionParameterLayout.Create(parameters, context.State.CancellationToken);
        var defaults = layout.Defaults.Length == 0 ? default : AllocateFunctionDefaults(layout, context, span);
        try
        {
            var positional = 0;
            foreach (var slot in layout.Defaults)
                AddFunctionDefault(defaults, layout, slot, ref positional, ExecutableSlot.FromObject(parameters[slot.ParameterIndex].DefaultValue), context, span);
            var plan = new FunctionBindingPlan(name, PythonCallableKind.Function, layout, defaults);
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
        var prepared = AllocateFunctionDefaults(layout, context, span);
        try
        {
            var positional = 0;
            for (var i = 0; i < defaults.Length; i++)
            {
                var expression = defaults[i];
                context.CheckExecution(expression.Span);
                var value = asynchronous ? await EvaluateLoweredExpressionAsync(expression, context).ConfigureAwait(false)
                    : EvaluateLoweredExpression(expression, context);
                AddFunctionDefault(prepared, layout, layout.Defaults[i], ref positional, ExecutableSlot.FromObject(value), context, expression.Span);
            }
            return prepared;
        }
        catch { prepared.RefundUnpublished(context.MemoryGovernor); throw; }
    }

    private static PyFunctionBase CreateLambdaWithUnpublishedDefaults(ExecutableLambdaBinding binding,
        FunctionDefaults defaults, ExecutionContext context, LythonSourceSpan span)
    {
        try { return CreateLambdaFunction(binding, defaults, context, span); }
        catch { defaults.RefundUnpublished(context.MemoryGovernor); throw; }
    }
}
