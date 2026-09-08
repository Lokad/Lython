using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorExpression : IPyTruthyValue, IPyAsyncIteratorValue
{
    private readonly IReadOnlyList<LoweredComprehensionClause> _clauses;
    private readonly LoweredExpression _itemExpression;
    private readonly LythonRuntime.ExecutionContext _closure;
    private readonly LythonSourceSpan _span;
    private IEnumerator<object>? _iterator;
    private IAsyncEnumerator<object>? _asyncIterator;
    private bool _asyncCompleted;

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

    public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

    public bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _iterator ??= IterateClauses(_clauses, 0, _closure).GetEnumerator();
        if (_iterator.MoveNext())
        {
            value = _iterator.Current;
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public async ValueTask<List<object>> MaterializeAsync()
        // Shares the governed drain with PyIteration.MaterializeAsync; the closure
        // context and creation span preserve this generator's exact budget behavior.
        => await PyIteration.DrainAsync(IterateAsync(), _span, _closure).ConfigureAwait(false);

    public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_iterator is not null)
        {
            return TryMoveNext(out var syncValue)
                ? PyIterationResult.Yield(syncValue)
                : PyIterationResult.End;
        }

        if (_asyncCompleted)
        {
            return PyIterationResult.End;
        }

        _asyncIterator ??= IterateClausesAsync(_clauses, 0, _closure).GetAsyncEnumerator();
        if (await _asyncIterator.MoveNextAsync().ConfigureAwait(false))
        {
            return PyIterationResult.Yield(_asyncIterator.Current);
        }

        await _asyncIterator.DisposeAsync().ConfigureAwait(false);
        _asyncIterator = null;
        _asyncCompleted = true;
        return PyIterationResult.End;
    }

    private IEnumerable<object> IterateClauses(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        LythonRuntime.ExecutionContext context)
    {
        var clause = clauses[index];
        var iterable = LythonRuntime.EvaluateLoweredExpression(clause.Iterable, context);
        foreach (var item in LythonRuntime.ToSequence(iterable, clause.Iterable.Span, context))
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

    private async IAsyncEnumerable<object> IterateClausesAsync(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        LythonRuntime.ExecutionContext context)
    {
        var clause = clauses[index];
        var iterable = await LythonRuntime.EvaluateLoweredExpressionAsync(clause.Iterable, context).ConfigureAwait(false);
        await foreach (var item in LythonRuntime.ToSequenceAsync(iterable, clause.Iterable.Span, context).ConfigureAwait(false))
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
                yield return LythonRuntime.RuntimeValue(
                    await LythonRuntime.EvaluateLoweredExpressionAsync(_itemExpression, scope).ConfigureAwait(false));
            }
            else
            {
                await foreach (var nested in IterateClausesAsync(clauses, index + 1, scope).ConfigureAwait(false))
                {
                    yield return nested;
                }
            }
        }
    }
}
