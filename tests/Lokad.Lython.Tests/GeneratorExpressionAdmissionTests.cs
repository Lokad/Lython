using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class GeneratorExpressionAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StarmapDrainsTheFirstRowAndLeavesOrdinaryGeneratorIterationIntact(bool asynchronous)
    {
        const string source = """
            from itertools import starmap
            def inner(value):
                yield value
            def consume(value):
                return value + 1
            rows = (inner(value) for value in (7, 8))
            return starmap(consume, rows), rows
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var script = ExecutableScript.Compile(LythonFrontend.Compile(source).Script!);
        var pair = Assert.IsType<PyTuple>(LythonRuntime.ExecuteExecutableCodeObject(script.EntryPoint, context, default, null));
        var mapped = Assert.IsType<PyStarmapIterator>(pair[0]);
        var rows = Assert.IsType<LythonRuntime.PyGenerator>(pair[1]);

        try
        {
            var first = asynchronous ? await mapped.TryMoveNextAsync() : mapped.TryMoveNext();
            Assert.Equal(new BigInteger(8), Assert.IsType<BigInteger>(first.Value));

            var next = asynchronous ? await rows.TryMoveNextAsync() : rows.TryMoveNext();
            var ordinary = Assert.IsType<LythonRuntime.PyGenerator>(next.Value);
            Assert.Equal(new BigInteger(8), Assert.IsType<BigInteger>(ordinary.TryMoveNext().Value));
            Assert.False(ordinary.TryMoveNext().HasValue);
            Assert.False((await rows.TryMoveNextAsync()).HasValue);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
        }
        finally { context.State.CloseResources(); }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnpackedGeneratorDisplayDenialUnwindsFrameBeforeReturning(bool asynchronous)
    {
        const long capacity = 1024 * 1024;
        var budget = new LythonMemoryBudget(capacity);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var syntax = LythonFrontend.Compile("rows = ([left, 7, right] for left, right in source)").Script!;
        var code = ExecutableScript.Compile(syntax).EntryPoint;
        var create = Assert.Single(code.Constants.OfType<ExecutableCreateGeneratorExpression>());
        var row = new PyList([PyNone.Instance, PyNone.Instance]);
        var outer = new PyList([row]);
        var generator = Assert.IsType<LythonRuntime.PyGenerator>(await LythonRuntime.PyGenerator.CreateExpressionAsync(
            create.CodeObject, context, outer, create.OuterSpan, create.OuterSpan, asynchronous));
        Assert.True(budget.TryReserve(capacity - budget.ReservedBytes, out var blocker));
        using (blocker)
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                _ = asynchronous ? await generator.TryMoveNextAsync() : generator.TryMoveNext());
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Equal(PyList.EstimateCapacityBytes(3), failure.Attribution?.Requested);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task DeniedScopeAdmissionDiscardsUnpublishedGenerator()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        root.Variables["total"] = BigInteger.Zero;
        var frontend = LythonFrontend.Compile("(i for i in (7,) if (total:=i)<0)",regexProvider:Utf8RegexProvider.Instance);
        var expression = Assert.IsType<ExpressionStatementSyntax>(Assert.Single(frontend.Script!.Statements)).Expression;
        var lowered = Assert.IsType<LoweredGeneratorExpression>(LoweredScript.LowerExpression(expression));
        _ = ExecutableScript.Compile(frontend.Script!);
        var governor = root.MemoryGovernor;
        long staged = 0;
        var acquired = new OwnedOuter(() =>
        {
            // Refuse publication after the outer was acquired.
            var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
            governor.Reserve(fill, lowered.Span);
            governor.Commit(fill);
            staged = budget.ReservedBytes;
        });
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await LythonRuntime.PyGenerator.CreateExpressionAsync(lowered.Code, root, acquired,
                lowered.Span, lowered.Span, true));
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.Equal(BigInteger.Zero, root.Variables["total"]);
        Assert.Equal(0, acquired.Moves);
        Assert.True(budget.ReservedBytes < staged);
        Assert.Equal(1, acquired.Acquisitions);
        Assert.Equal(0, governor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class OwnedOuter(Action acquired) : IPyIterableValue, IEnumerable<object>, IEnumerator<object>
    {
        public int Moves { get; private set; }
        public int Acquisitions { get; private set; }
        public object Current => BigInteger.Zero;
        object System.Collections.IEnumerator.Current => Current;
        public IEnumerable<object> Iterate() { Acquisitions++; acquired(); return this; }
        public IEnumerator<object> GetEnumerator() => this;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext() { Moves++; return false; }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }
}
