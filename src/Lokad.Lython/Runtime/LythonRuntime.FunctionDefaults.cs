using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Build the final default containers privately. The defining operation
    // owns them until a decorator or a binding can expose the function.
    private sealed class PendingFunctionDefaults : ExecutableTemporaryValue
    {
        private readonly FunctionParameterLayout _layout;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly MemoryGovernor.TemporaryMemoryReservation? _positionalStorage;
        private readonly int _keywordRecordCache;
        private object[]? _positionalItems;
        private int _position;
        private PyTuple? _positional;
        private PyDict? _keywords;

        internal PendingFunctionDefaults(FunctionParameterLayout layout, ExecutionContext context, LythonSourceSpan span,
            int keywordRecordCache = -1)
        {
            _keywordRecordCache = keywordRecordCache;
            _layout = layout;
            _context = context;
            _span = span;
            var governor = context.MemoryGovernor;
            var positionalCount = layout.PositionalDefaultCount;
            var keywordCount = layout.Defaults.Length - positionalCount;
            _positionalStorage = positionalCount == 0 ? null
                : governor.ReserveTemporary(PyTuple.EstimateApproximateBytes(positionalCount), span);
            try
            {
                context.CheckExecution(span);
                if (positionalCount != 0) _positionalItems = new object[positionalCount];
                if (keywordCount != 0) _keywords = context.CreateDefinitionRecord(keywordCount, keywordRecordCache, span);
            }
            catch { Dispose(); throw; }
        }

        internal FunctionDefaults Values => new(_positional, _keywords);

        internal void Add(FunctionParameterLayout.DefaultSlot slot, object value, LythonSourceSpan span)
        {
            if (slot.KeywordIndex < 0)
            {
                _context.CheckExecution(span);
                _positionalItems![_position++] = value;
            }
            else _keywords!.SetRecordItem(_layout.KeywordDefaultName(slot.KeywordIndex), value, _context, span,
                replaceExisting: false);
        }

        internal void Complete()
        {
            if (_positionalItems is { } items)
            {
                _positional = PyTuple.FromReservedOwnedArray(items, _context.MemoryGovernor, _span, _positionalStorage!);
                _positionalItems = null;
            }
            if (_keywords is not null) _context.CacheDefinitionRecordKeys(_keywords, _keywordRecordCache, _span);
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
            _positionalItems = null;
            try { _positionalStorage?.Dispose(); }
            finally { unpublished.RefundUnpublished(_context.MemoryGovernor); }
        }
    }

    private static ValueTask<PendingFunctionDefaults?> EvaluateFunctionDefaults(
        IReadOnlyList<LoweredFunctionParameter> parameters, FunctionParameterLayout layout,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        context.CheckExecution(span);
        return layout.Defaults.Length == 0 ? new((PendingFunctionDefaults?)null)
            : EvaluateDefaultsAsync(parameters, layout, context, span, asynchronous);
    }

    private static async ValueTask<PendingFunctionDefaults?> EvaluateDefaultsAsync(
        IReadOnlyList<LoweredFunctionParameter> parameters, FunctionParameterLayout layout,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var pending = new PendingFunctionDefaults(layout, context, span);
        try
        {
            foreach (var slot in layout.Defaults)
            {
                var expression = parameters[slot.ParameterIndex].DefaultValue.RequireNotNull();
                context.CheckExecution(expression.Span);
                var value = asynchronous ? await EvaluateLoweredExpressionAsync(expression, context).ConfigureAwait(false)
                    : EvaluateLoweredExpression(expression, context);
                pending.Add(slot, RuntimeValue(value), expression.Span);
            }
            pending.Complete();
            return pending;
        }
        catch { pending.Dispose(); throw; }
    }
}
