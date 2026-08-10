using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorExpression : IPyTruthyValue, IPyAsyncIteratorValue
{
    private readonly IReadOnlyList<LoweredComprehensionClause> _clauses;
    private readonly LoweredExpression _itemExpression;
    private readonly LythonRuntime.ExecutionContext _closure;
    private readonly LythonSourceSpan _span;
    private IEnumerator<object>? _iterator;
    private List<object>? _asyncItems;
    private int _asyncIndex;

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
        while (TryMoveNext(out var value))
        {
            yield return value;
        }
    }

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
    {
        var result = new List<object>();
        while (true)
        {
            var (hasValue, value) = await TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                break;
            }

            result.Add(value);
        }

        return result;
    }

    public async IAsyncEnumerable<object> IterateAsync()
    {
        while (true)
        {
            var (hasValue, value) = await TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                yield break;
            }

            yield return value;
        }
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_iterator is not null)
        {
            return TryMoveNext(out var syncValue)
                ? PyIterationResult.Yield(syncValue)
                : PyIterationResult.End;
        }

        if (_asyncItems is null)
        {
            _asyncItems = [];
            await IterateClausesAsync(_clauses, 0, _closure, _asyncItems).ConfigureAwait(false);
        }

        if (_asyncIndex < _asyncItems.Count)
        {
            return PyIterationResult.Yield(_asyncItems[_asyncIndex++]);
        }

        return PyIterationResult.End;
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
        await foreach (var item in LythonRuntime.ToSequenceAsync(iterable, clause.Iterable.Span).ConfigureAwait(false))
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
