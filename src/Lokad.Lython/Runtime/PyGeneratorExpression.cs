using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorExpression : IPyTruthyValue, IPyIterableValue
{
    private readonly IReadOnlyList<LoweredComprehensionClause> _clauses;
    private readonly LoweredExpression _itemExpression;
    private readonly LythonRuntime.ExecutionContext _closure;
    private readonly LythonSourceSpan _span;

    public PyGeneratorExpression(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        LoweredExpression itemExpression,
        LythonRuntime.ExecutionContext closure,
        LythonSourceSpan span)
    {
        _clauses = clauses;
        _itemExpression = itemExpression;
        _closure = closure;
        _span = span;
    }

    public bool IsTruthy() => true;

    public IEnumerable<object> Iterate()
    {
        return IterateClauses(_clauses, 0, _closure);
    }

    public async ValueTask<List<object>> IterateAsync()
    {
        var result = new List<object>();
        await IterateClausesAsync(_clauses, 0, _closure, result).ConfigureAwait(false);
        return result;
    }

    private IEnumerable<object> IterateClauses(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        LythonRuntime.ExecutionContext context)
    {
        var clause = clauses[index];
        var iterable = LythonRuntime.EvaluateLoweredExpression(clause.Iterable, context);
        foreach (var item in LythonRuntime.ToSequence(iterable, clause.Iterable.Span))
        {
            var scope = new LythonRuntime.ExecutionContext(context);
            LythonRuntime.AssignLoopTarget(clause.Target, item, clause.Iterable.Span, scope);

            if (clause.Condition is not null && !LythonRuntime.IsTruthy(LythonRuntime.EvaluateLoweredExpression(clause.Condition, scope)))
            {
                continue;
            }

            if (index == clauses.Count - 1)
            {
                yield return LythonRuntime.RuntimeValue(LythonRuntime.EvaluateLoweredExpression(_itemExpression, scope));
            }
            else
            {
                foreach (var nested in IterateClauses(clauses, index + 1, scope))
                {
                    yield return nested;
                }
            }
        }
    }

    private async ValueTask IterateClausesAsync(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        LythonRuntime.ExecutionContext context,
        List<object> result)
    {
        var clause = clauses[index];
        var iterable = await LythonRuntime.EvaluateLoweredExpressionAsync(clause.Iterable, context).ConfigureAwait(false);
        foreach (var item in LythonRuntime.ToSequence(iterable, clause.Iterable.Span))
        {
            var scope = new LythonRuntime.ExecutionContext(context);
            LythonRuntime.AssignLoopTarget(clause.Target, item, clause.Iterable.Span, scope);

            if (clause.Condition is not null &&
                !LythonRuntime.IsTruthy(await LythonRuntime.EvaluateLoweredExpressionAsync(clause.Condition, scope).ConfigureAwait(false)))
            {
                continue;
            }

            if (index == clauses.Count - 1)
            {
                result.Add(LythonRuntime.RuntimeValue(await LythonRuntime.EvaluateLoweredExpressionAsync(_itemExpression, scope).ConfigureAwait(false)));
            }
            else
            {
                await IterateClausesAsync(clauses, index + 1, scope, result).ConfigureAwait(false);
            }
        }
    }
}
