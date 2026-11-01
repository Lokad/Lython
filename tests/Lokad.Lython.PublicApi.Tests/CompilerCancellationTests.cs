using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CompilerCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringAnnotationStorageUnwindsCompilationAndPreservesRefusal(bool refuse)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var supplied = new CancellingScope(budget.CreateScope(), cancellation, refuse);
        using (var script = new LythonEngine().Compile("""
            from __future__ import annotations
            def helper(value: list[str]): pass
            """, supplied, cancellation.Token))
        {
            Assert.True(cancellation.IsCancellationRequested);
            Assert.False(script.IsValid);
            Assert.Equal(refuse ? "LA0008" : "LA2011", Assert.Single(script.Diagnostics).Code);
            using var result = script.Run(new MockLythonHost());
            Assert.False(result.Success);
            Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                result.FailureAttribution?.Reason);
            if (refuse) Assert.True(result.FailureAttribution?.Requested > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var reused = new LythonEngine().Compile("print(1 + 2)", budget))
        using (var result = reused.Run(new MockLythonHost()))
        {
            Assert.True(result.Success);
            Assert.Equal("3\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    // A real compiler working-storage admission supplies the cancellation window,
    // after lexing/parsing, without timing or a new production checkpoint hook.
    private sealed class CancellingScope(ILythonMemoryScope inner, CancellationTokenSource cancellation, bool refuse) : ILythonMemoryScope
    {
        public ILythonMemoryScope CreateScope() => new CancellingScope(inner.CreateScope(), cancellation, refuse);
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var result)) { reservation = null; return false; }
            reservation = new CancellingReservation(result, cancellation, refuse);
            return true;
        }
        public void Dispose() => inner.Dispose();
    }

    private sealed class CancellingReservation(ILythonMemoryReservation inner, CancellationTokenSource cancellation, bool refuse) : ILythonMemoryReservation
    {
        public long Bytes => inner.Bytes;
        public bool TryResize(long bytes)
        {
            if (bytes > inner.Bytes)
            {
                cancellation.Cancel();
                if (refuse) return false;
            }
            return inner.TryResize(bytes);
        }
        public void Dispose() => inner.Dispose();
    }
}
