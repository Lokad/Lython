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
        private int _position;
        private PyTuple? _positional;
        private PyDict? _keywords;

        internal PendingFunctionDefaults(FunctionParameterLayout layout, ExecutionContext context, LythonSourceSpan span,
            PyDict.RecordKeys? keywordKeys = null)
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
                    _keywords = keywordKeys?.CreateRecord(context, span, keywordCount)
                        ?? new PyDict(keywordCount, context.MemoryGovernor, span);
            }
            catch { Dispose(); throw; }
        }

        internal FunctionDefaults Values => new(_positional, _keywords);

        internal void Add(FunctionParameterLayout.DefaultSlot slot, ExecutableSlot value, LythonSourceSpan span)
        {
            if (slot.KeywordIndex < 0) _positional!.SetPrivateDefault(_position++, value, _context, span);
            else _keywords!.AddDefinitionValue(_layout.KeywordDefaultName(slot.KeywordIndex), RuntimeValue(value.Object), _context, span);
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
