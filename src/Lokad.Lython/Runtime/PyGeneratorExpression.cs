using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed class PyGeneratorExpression : IPyTruthyValue, IPyAsyncIteratorValue, IPyDynamicAttributes
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
    private readonly object _outerIterator;
    private readonly IAsyncEnumerable<object>? _acquiredAsyncSequence;
    private readonly ScopeDirectiveFacts _scopeFacts;
    // Eager async outer for async-created generators over async iterables
    // (files and friends): resolving the async enumerator at creation
    // preserves creation-time effects and rebinding like the sync outer,
    // while pulls suspend properly instead of failing on the sync path.
    // Generators over plain values never take this path and keep the shared
    // sync outer for both modes.
    private IAsyncEnumerator<object>? _outerAsyncEnumerator;
    private LythonRuntime.ExecutionContext? _iterationScope;
    private IEnumerator<object>? _iterator;
    private IAsyncEnumerator<object>? _asyncIterator;
    private bool _completed;

    public PyGeneratorExpression(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        LoweredExpression itemExpression,
        LythonRuntime.ExecutionContext closure,
        LythonSourceSpan span,
        PyIteration.GeneratorOuter outer, ScopeDirectiveFacts scopeFacts)
    {
        _clauses = clauses;
        _itemExpression = itemExpression;
        _closure = closure;
        _span = span;
        _outerSequence = outer.Sequence;
        _outerIterator = outer.Iterator;
        _acquiredAsyncSequence = outer.AsyncSequence;
        _scopeFacts = scopeFacts;
        // Generator objects retain clauses, item, closure and sequence per live
        // generator; charge one constructed-value unit like other iterators.
        PyIteratorBase.ChargeIteratorValue(closure.MemoryGovernor, span);
    }

    public bool IsTruthy() => true;
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        => LythonRuntime.IteratorMembers.TryGetMember(this, name, out value);

    public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

    public bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_completed) { value = PyNone.Instance; return false; }
        try
        {
            _iterationScope ??= CreateIterationScope();
            _iterator ??= IterateClauses(_clauses, 0, _iterationScope).GetEnumerator();
            if (_iterator.MoveNext())
            {
                value = _iterator.Current;
                return true;
            }
            _completed = true;
            value = PyNone.Instance;
            return false;
        }
        catch (LythonRuntimeException exception) when (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
        {
            _completed = true;
            throw LythonRuntime.GeneratorRaisedStopIteration(exception, _closure, _span);
        }
        catch { _completed = true; throw; }
    }

    internal void AttachAsyncOuter(object outerValue)
    {
        if (outerValue is IPyAsyncIterableValue asyncIterable)
        {
            _outerAsyncEnumerator = asyncIterable.IterateAsync().GetAsyncEnumerator();
        }
    }

    private async IAsyncEnumerable<object> EnumerateOuterAsync()
    {
        var enumerator = _outerAsyncEnumerator;
        _outerAsyncEnumerator = null;
        if (enumerator is null)
        {
            yield break;
        }

        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask<List<object>> MaterializeAsync()
        // Shares the governed drain with PyIteration.MaterializeAsync; the closure
        // context and creation span preserve this generator's exact budget behavior.
        => await PyIteration.DrainAsync(IterateAsync(), _span, _closure).ConfigureAwait(false);

    public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_completed) return PyIterationResult.End;
        if (_iterator is not null)
        {
            return TryMoveNext(out var syncValue)
                ? PyIterationResult.Yield(syncValue)
                : PyIterationResult.End;
        }

        try
        {
            _iterationScope ??= CreateIterationScope();
            _asyncIterator ??= IterateClausesAsync(_clauses, 0, _iterationScope).GetAsyncEnumerator();
            if (await _asyncIterator.MoveNextAsync().ConfigureAwait(false))
                return PyIterationResult.Yield(_asyncIterator.Current);
            _completed = true;
            return PyIterationResult.End;
        }
        catch (LythonRuntimeException exception) when (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
        {
            _completed = true;
            throw LythonRuntime.GeneratorRaisedStopIteration(exception, _closure, _span);
        }
        catch { _completed = true; throw; }
        finally
        {
            if (_completed)
            {
                var iterator = _asyncIterator;
                _asyncIterator = null;
                try
                {
                    if (iterator is not null) await iterator.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    // Scope admission can fail before clause enumeration takes
                    // ownership of the eagerly acquired async outer iterator.
                    var outer = _outerAsyncEnumerator;
                    _outerAsyncEnumerator = null;
                    if (outer is not null) await outer.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private LythonRuntime.ExecutionContext CreateIterationScope()
    {
        // New nonlocal target-map capacity belongs to the retained iteration
        // scope. Fund it before allocation; ordinary generators need no map.
        var bytes = _scopeFacts.NonlocalNames.Count == 0 ? 0 : checked(128L + 96L * _scopeFacts.NonlocalNames.Count);
        if (bytes != 0)
        {
            _closure.MemoryGovernor.Reserve(bytes, _span);
            _closure.MemoryGovernor.Commit(bytes);
        }
        try
        {
            var scope = new LythonRuntime.ExecutionContext(_closure, _scopeFacts)
            {
                FunctionName = "<genexpr>", FirstPositionalParameterName = ".0"
            };
            scope.Variables[".0"] = _outerIterator;
            return scope;
        }
        catch { if (bytes != 0) _closure.MemoryGovernor.Release(bytes); throw; }
    }

    private IEnumerable<object> IterateClauses(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        LythonRuntime.ExecutionContext scope)
    {
        var clause = clauses[index];
        // The outermost iterable was already evaluated and acquired at
        // construction; only later clauses evaluate theirs per advance.
        var items = index == 0
            ? _outerSequence
            : LythonRuntime.ToSequence(
                LythonRuntime.EvaluateLoweredExpression(clause.Iterable, scope),
                clause.Iterable.Span,
                scope);
        foreach (var item in items)
        {
            // One shared scope per generator run: loop targets rebind the same
            // cells, so closures observe final values like eager comprehensions.
            LythonRuntime.AssignLoopTarget(clause.Target, item, clause.Iterable.Span, scope);

            if (clause.Condition is not null && !LythonRuntime.IsTruthy(LythonRuntime.EvaluateLoweredExpression(clause.Condition, scope), scope, clause.Condition.Span))
            {
                continue;
            }

            if (index == clauses.Count - 1)
            {
                var produced = LythonRuntime.RuntimeValue(LythonRuntime.EvaluateLoweredExpression(_itemExpression, scope));
                yield return produced;
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
        LythonRuntime.ExecutionContext scope)
    {
        var clause = clauses[index];
        var items = index == 0 && _outerAsyncEnumerator is not null
            ? EnumerateOuterAsync()
            : index == 0
                ? _acquiredAsyncSequence ?? LythonRuntime.ToSequenceAsync(_outerSequence, clause.Iterable.Span)
                : LythonRuntime.ToSequenceAsync(await LythonRuntime.EvaluateLoweredExpressionAsync(clause.Iterable, scope).ConfigureAwait(false), clause.Iterable.Span, scope);
        await foreach (var item in items.ConfigureAwait(false))
        {
            // One shared scope per generator run: loop targets rebind the same
            // cells, so closures observe final values like eager comprehensions.
            await LythonRuntime.AssignLoopTargetAsync(clause.Target, item, clause.Iterable.Span, scope).ConfigureAwait(false);

            if (clause.Condition is not null &&
                !LythonRuntime.IsTruthy(await LythonRuntime.EvaluateLoweredExpressionAsync(clause.Condition, scope).ConfigureAwait(false), scope, clause.Condition.Span))
            {
                continue;
            }

            if (index == clauses.Count - 1)
            {
                var produced = LythonRuntime.RuntimeValue(
                    await LythonRuntime.EvaluateLoweredExpressionAsync(_itemExpression, scope).ConfigureAwait(false));
                yield return produced;
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
