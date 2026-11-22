using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static FunctionDefaults AllocateFunctionDefaults(FunctionParameterLayout layout, ExecutionContext context,
        LythonSourceSpan span)
    {
        context.CheckExecution(span);
        var positional = layout.PositionalDefaultCount == 0
            ? null : PyTuple.CreateDefaultStorage(layout.PositionalDefaultCount, context, span);
        try
        {
            var keywordCount = layout.Defaults.Length - layout.PositionalDefaultCount;
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
        var positionalCount = keywordsOnly ? 0 : layout.PositionalDefaultCount;
        var keys = layout.HasKeywordDefaults ? layout.PreparedKeywordDefaultKeys.RequireNotNull() : null;
        var positionalBytes = positionalCount == 0 ? 0 : PyTuple.EstimateDefaultStorage(positionalCount);
        var keywordCapacity = keys?.PreparedValueCapacity ?? 0;
        var bytes = checked(positionalBytes + (keys is null ? 0 : PyDict.RecordKeys.RecordBytes(keywordCapacity)));
        var governor = context.MemoryGovernor;
        context.CheckExecution(span);
        governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            var positional = positionalCount == 0 ? default : ExecutableSlot.OwnPrivateDefault(
                PyTuple.CreateAdmittedDefaultStorage(positionalCount, governor, span, positionalBytes));
            var keyword = keys is null ? default : keys.Count == 1
                ? ExecutableSlot.OwnPrivateKeywordDefault(PyNone.Instance)
                : ExecutableSlot.OwnPrivateDefault(new object?[keywordCapacity]);
            governor.Commit(bytes);
            if (positionalCount != 0) operands[0] = positional;
            if (keys is not null) operands[^1] = keyword;
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
