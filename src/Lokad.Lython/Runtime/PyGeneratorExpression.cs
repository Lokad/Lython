using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorExpression : IPyTruthyValue, IPyAsyncIteratorValue
{
    private readonly IReadOnlyList<LoweredComprehensionClause> _clauses;
    private readonly LoweredExpression _itemExpression;
    private readonly LythonRuntime.ExecutionContext _closure;
    private readonly LythonSourceSpan _span;
    /// <summary>
    /// The outermost iterable, evaluated and acquired when the generator is
    /// created (matching CPython). Rebinding the source name later, or effects
    /// in its __iter__, are observed at creation, not at first advance.
    /// </summary>
    private readonly IEnumerable<object> _outerSequence;
    private IEnumerator<object>? _iterator;
    private IAsyncEnumerator<object>? _asyncIterator;
    private bool _asyncCompleted;

    public PyGeneratorExpression(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        LoweredExpression itemExpression,
        LythonRuntime.ExecutionContext closure,
        LythonSourceSpan span,
        IEnumerable<object> outerSequence)
    {
        _clauses = clauses;
        _itemExpression = itemExpression;
        _closure = closure;
        _span = span;
        _outerSequence = outerSequence;
        // Generator objects retain clauses, item, closure and sequence per live
        // generator; charge one constructed-value unit like other iterators.
        PyIteratorBase.ChargeIteratorValue(closure.MemoryGovernor, span);
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
        // The outermost iterable was already evaluated and acquired at
        // construction; only later clauses evaluate theirs per advance.
        var items = index == 0
            ? _outerSequence
            : LythonRuntime.ToSequence(
                LythonRuntime.EvaluateLoweredExpression(clause.Iterable, context),
                clause.Iterable.Span,
                context);
        foreach (var item in items)
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
        var items = index == 0
            ? LythonRuntime.ToSequenceAsync(_outerSequence, clause.Iterable.Span)
            : LythonRuntime.ToSequenceAsync(await LythonRuntime.EvaluateLoweredExpressionAsync(clause.Iterable, context).ConfigureAwait(false), clause.Iterable.Span, context);
        await foreach (var item in items.ConfigureAwait(false))
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
