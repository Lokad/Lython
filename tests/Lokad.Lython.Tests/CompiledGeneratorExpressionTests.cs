using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using ICallable = Lokad.Lython.Runtime.LythonRuntime.ICallable;

namespace Lokad.Lython.Tests;

public sealed class CompiledGeneratorExpressionTests
{
    [Fact]
    public void OrdinaryBodiesHaveInstructionsAndLiveCellsWithoutParentMirroring()
    {
        var module = Compile("def make(offset):\n return (i+offset for i in range(3) if i)\nresult=make(7)");
        var parent = Assert.Single(module.Functions).CodeObject!;
        var operation = Assert.Single(parent.Constants.OfType<ExecutableCreateGeneratorExpression>());
        var child = operation.CodeObject;
        Assert.Equal("<genexpr>", child.Name);
        Assert.Empty(parent.ExpressionFallbacks);
        Assert.Empty(child.ExpressionFallbacks);
        Assert.Empty(child.StatementFallbacks);
        Assert.False(module.RequiresLocalVariableMirroring);
        Assert.False(parent.RequiresLocalVariableMirroring);
        Assert.False(child.RequiresLocalVariableMirroring);
        Assert.Contains(".0", child.LocalNames);
        Assert.Contains("i", child.LocalNames);
        Assert.Contains("offset", child.ClosureNames);
        Assert.Contains(parent.LocalNameToSlot["offset"], parent.CapturedLocalSlots);
        Assert.Contains(child.Blocks.SelectMany(block => block.Instructions), instruction => instruction.OpCode == ExecutableOpCode.Yield);
        Assert.Contains(child.Blocks.SelectMany(block => block.Instructions), instruction => instruction.OpCode == ExecutableOpCode.ForNext);
        Assert.DoesNotContain(child.Blocks.SelectMany(block => block.Instructions), instruction => instruction.OpCode == ExecutableOpCode.GetIter);
    }

    [Theory]
    [InlineData("close-before")]
    [InlineData("throw-before")]
    [InlineData("entry-refusal")]
    [InlineData("entry-cancel")]
    [InlineData("close-started")]
    public async Task AcquiredNativeOuterRemainsFundedUntilAwaitedCleanup(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, CancellationToken = cancellation.Token });
        var operation = Assert.Single(Compile("result=(i for i in source)").Constants.OfType<ExecutableCreateGeneratorExpression>());
        var outer = new PausedOuter();
        var generator = await LythonRuntime.PyGenerator.CreateExpressionAsync(operation.CodeObject, context,
            outer, operation.OuterSpan, operation.OuterSpan, true);
        Assert.Equal(1, outer.Acquisitions);
        Assert.Equal(0, outer.Moves);
        if (route == "close-started") Assert.True((await generator.TryMoveNextAsync()).HasValue);
        if (route == "entry-cancel") cancellation.Cancel();
        if (route == "entry-refusal")
        {
            var governor = context.MemoryGovernor;
            var fill = governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes;
            governor.Reserve(fill, operation.OuterSpan);
            governor.Commit(fill);
            _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, operation.OuterSpan));
        }
        var held = budget.ReservedBytes;
        var pending = Invoke().AsTask();
        try
        {
            var first = await Task.WhenAny(outer.Disposing.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(outer.Disposing.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.Equal(held, budget.ReservedBytes);
            Assert.Equal(route == "close-started" ? 1 : 0, outer.Moves);
        }
        finally
        {
            outer.Release.TrySetResult();
            _ = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        if (route is "throw-before" or "entry-refusal" or "entry-cancel")
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            if (route == "entry-refusal") Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            else if (route == "entry-cancel") Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            else Assert.Equal("ValueError", error.ExceptionType);
        }
        else await pending;
        Assert.Equal(1, outer.Disposals);
        Assert.False((await generator.TryMoveNextAsync()).HasValue);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        async ValueTask Invoke()
        {
            if (route is "entry-refusal" or "entry-cancel") _ = await generator.TryMoveNextAsync();
            else
            {
                Assert.True(generator.TryGetMember(route == "throw-before" ? "throw" : "close", out var method));
                CallArgumentValue[] arguments = route == "throw-before"
                    ? [CallArgumentValue.Positional(new PyException("ValueError", "before", PyNone.Instance))] : [];
                _ = await Assert.IsAssignableFrom<ICallable>(method).InvokeAsync(arguments, operation.OuterSpan, context);
            }
        }
    }

    [Fact]
    public async Task FrameDenialPrecedesOuterAcquisition()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024 });
        var operation = Assert.Single(Compile("result=(i for i in source)").Constants.OfType<ExecutableCreateGeneratorExpression>());
        var governor = context.MemoryGovernor;
        var fill = governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes;
        governor.Reserve(fill, operation.OuterSpan);
        governor.Commit(fill);
        var outer = new PausedOuter();
        var held = budget.ReservedBytes;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await LythonRuntime.PyGenerator.CreateExpressionAsync(operation.CodeObject, context,
                outer, operation.OuterSpan, operation.OuterSpan, true));
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.Equal(held, budget.ReservedBytes);
        Assert.Equal(0, outer.Acquisitions);
        Assert.Equal(0, outer.Moves);
        Assert.Equal(0, outer.Disposals);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.NotNull(frontend.Script);
        return ExecutableScript.Compile(LoweredScript.Lower(frontend.Script)).EntryPoint;
    }

    private sealed class PausedOuter : IPyAsyncIterableValue, IEnumerable<object>, IAsyncEnumerable<object>, IAsyncEnumerator<object>
    {
        public TaskCompletionSource Disposing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Acquisitions { get; private set; }
        public int Moves { get; private set; }
        public int Disposals { get; private set; }
        public object Current => new BigInteger(7);
        public IEnumerable<object> Iterate() => this;
        public IEnumerator<object> GetEnumerator() => throw new InvalidOperationException("Unexpected synchronous traversal.");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public IAsyncEnumerable<object> IterateAsync() => this;
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default) { Acquisitions++; return this; }
        public ValueTask<bool> MoveNextAsync() { Moves++; return new(true); }
        public async ValueTask DisposeAsync()
        {
            Disposals++;
            Disposing.TrySetResult();
            await Release.Task.ConfigureAwait(false);
        }
    }
}
