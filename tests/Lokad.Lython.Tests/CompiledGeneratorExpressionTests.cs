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
        var parent = Assert.Single(module.FunctionBindings()).CodeObject!;
        var operation = Assert.Single(parent.Constants.OfType<ExecutableCreateGeneratorExpression>());
        var child = operation.CodeObject;
        Assert.Equal("<genexpr>", child.Name);
        Assert.False(module.RequiresLocalVariableMirroring);
        Assert.False(parent.RequiresLocalVariableMirroring);
        Assert.False(child.RequiresLocalVariableMirroring);
        Assert.Contains(".0", child.LocalNames);
        Assert.Contains("i", child.LocalNames);
        Assert.Contains("offset", child.ClosureNames);
        Assert.Contains(parent.LocalNameToSlot["offset"], parent.CapturedLocalSlots);
        Assert.Contains(child.Instructions, instruction => instruction.OpCode == ExecutableOpCode.Yield);
        Assert.Contains(child.Instructions, instruction => instruction.OpCode == ExecutableOpCode.ForNext);
        Assert.DoesNotContain(child.Instructions, instruction => instruction.OpCode == ExecutableOpCode.GetIter);
    }

    [Theory]
    [InlineData("close-before")]
    [InlineData("throw-before")]
    [InlineData("entry-refusal")]
    [InlineData("entry-cancel")]
    [InlineData("close-started")]
    public async Task TerminalEntryPreservesBorrowedOuter(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var operation = Assert.Single(Compile("result=(i for i in source)").Constants.OfType<ExecutableCreateGeneratorExpression>());
        var outer = new OwnedOuter();
        var generator = await LythonRuntime.PyGenerator.CreateExpressionAsync(operation.CodeObject, context,
            outer, operation.OuterSpan, operation.OuterSpan, true);
        Assert.Equal(1, outer.Acquisitions);
        Assert.Equal(0, outer.Moves);
        if (route == "close-started") Assert.True((await generator.TryMoveNextAsync()).HasValue);
        if (route == "entry-cancel") cancellation.Cancel();
        if (route == "entry-refusal")
        {
            var governor = context.MemoryGovernor;
            var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
            governor.Reserve(fill, operation.OuterSpan);
            governor.Commit(fill);
            _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, operation.OuterSpan));
        }
        var pending = Invoke().AsTask();
        Assert.Equal(route == "close-started" ? 1 : 0, outer.Moves);
        if (route is "throw-before" or "entry-refusal" or "entry-cancel")
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            if (route == "entry-refusal") Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            else if (route == "entry-cancel") Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            else Assert.Equal("ValueError", error.ExceptionType);
        }
        else await pending;
        Assert.False((await generator.TryMoveNextAsync()).HasValue);
        Assert.Equal(0, outer.Disposals);
        Assert.True(outer.MoveNext());
        Assert.Equal(route == "close-started" ? 2 : 1, outer.Moves);
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
        { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var operation = Assert.Single(Compile("result=(i for i in source)").Constants.OfType<ExecutableCreateGeneratorExpression>());
        var governor = context.MemoryGovernor;
        var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
        governor.Reserve(fill, operation.OuterSpan);
        governor.Commit(fill);
        var outer = new OwnedOuter();
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
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.NotNull(frontend.Script);
        return ExecutableScript.Compile(frontend.Script).EntryPoint;
    }

    private sealed class OwnedOuter : IPyIterableValue, IEnumerable<object>, IEnumerator<object>
    {
        public int Acquisitions { get; private set; }
        public int Moves { get; private set; }
        public int Disposals { get; private set; }
        public object Current => new BigInteger(7);
        object System.Collections.IEnumerator.Current => Current;
        public IEnumerable<object> Iterate() { Acquisitions++; return this; }
        public IEnumerator<object> GetEnumerator() => this;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext() { Moves++; return true; }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() => Disposals++;
    }
}
