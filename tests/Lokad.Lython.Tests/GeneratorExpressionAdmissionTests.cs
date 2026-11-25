using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class GeneratorExpressionAdmissionTests
{
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
