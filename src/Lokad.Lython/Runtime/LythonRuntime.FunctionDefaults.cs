using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Build the final default storage privately. The defining operation owns
    // it until a decorator or a binding can expose the function.
    private sealed class PendingFunctionDefaults : ExecutableTemporaryValue
    {
        private readonly FunctionParameterLayout _layout;
        private readonly ExecutionContext _context;
        private int _position;
        private PyTuple? _positional;
        private object? _keywords;

        internal PendingFunctionDefaults(FunctionParameterLayout layout, ExecutionContext context, LythonSourceSpan span)
        {
            _layout = layout;
            _context = context;
            var positionalCount = layout.PositionalDefaultCount;
            var keywordCount = layout.Defaults.Length - positionalCount;
            try
            {
                context.CheckExecution(span);
                if (positionalCount != 0)
                    _positional = PyTuple.CreateDefaultStorage(positionalCount, context, span);
                if (keywordCount != 0)
                    _keywords = layout.PreparedKeywordDefaultKeys is { } keys
                        ? keys.CreatePreparedValues(context, span)
                        : new PyDict(keywordCount, context.MemoryGovernor, span);
            }
            catch { Dispose(); throw; }
        }

        internal FunctionDefaults Values => new(_positional, _keywords);

        internal void Add(FunctionParameterLayout.DefaultSlot slot, ExecutableSlot value, LythonSourceSpan span)
        {
            if (slot.KeywordIndex < 0) _positional!.SetPrivateDefault(_position++, value, _context, span);
            else if (_keywords is object?[] values)
            {
                _context.CheckExecution(span);
                values[_layout.KeywordDefaultAt(slot.KeywordIndex).RecordPosition] = RuntimeValue(value.Object);
            }
            else ((PyDict)_keywords!).AddDefinitionValue(_layout.KeywordDefaultName(slot.KeywordIndex), RuntimeValue(value.Object), _context, span);
        }

        internal void Publish()
        {
            _positional = null;
            _keywords = null;
        }

        public override void Dispose()
        {
            var unpublished = Values;
            Publish();
            unpublished.RefundUnpublished(_context.MemoryGovernor);
        }
    }

    internal static FunctionBindingPlan CreateGeneratedBindingPlan(string name,
        IReadOnlyList<GeneratedFunctionParameter> parameters, ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = FunctionParameterLayout.EstimateBytes(parameters);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var layout = FunctionParameterLayout.Create(parameters, context.State.CancellationToken);
        using var pending = layout.Defaults.Length == 0 ? null : new PendingFunctionDefaults(layout, context, span);
        if (pending is not null)
            foreach (var slot in layout.Defaults)
                pending.Add(slot, ExecutableSlot.FromObject(parameters[slot.ParameterIndex].DefaultValue), span);
        var plan = new FunctionBindingPlan(name, PythonCallableKind.Function, layout, pending?.Values ?? default);
        storage.Commit(bytes);
        pending?.Publish();
        return plan;
    }

    private static ValueTask<PendingFunctionDefaults?> EvaluateFunctionDefaults(
        LoweredExpression[] defaults, FunctionParameterLayout layout,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        context.CheckExecution(span);
        return defaults.Length == 0 ? new((PendingFunctionDefaults?)null)
            : EvaluateDefaultsAsync(defaults, layout, context, span, asynchronous);
    }

    private static async ValueTask<PendingFunctionDefaults?> EvaluateDefaultsAsync(
        LoweredExpression[] defaults, FunctionParameterLayout layout,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var pending = new PendingFunctionDefaults(layout, context, span);
        try
        {
            for (var i = 0; i < defaults.Length; i++)
            {
                var expression = defaults[i];
                context.CheckExecution(expression.Span);
                var value = asynchronous ? await EvaluateLoweredExpressionAsync(expression, context).ConfigureAwait(false)
                    : EvaluateLoweredExpression(expression, context);
                pending.Add(layout.Defaults[i], ExecutableSlot.FromObject(value), expression.Span);
            }
            return pending;
        }
        catch { pending.Dispose(); throw; }
    }
}
