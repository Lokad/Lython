using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CompilerCancellationTests
{
    [Theory]
    [InlineData("def helper(value):return value*3+1\nreturn helper(14)")]
    [InlineData("class Helper:\n def apply(self,value):return value*3+1\nreturn Helper().apply(14)")]
    [InlineData("helper=lambda value:value*3+1\nreturn helper(14)")]
    public async Task CompilationTokenIsNotRetainedByPreparedCallables(string source)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var compilation = new CancellationTokenSource();
        using (var script = new LythonEngine().Compile(source, budget, compilation.Token))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            compilation.Cancel();
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new System.Numerics.BigInteger(43), result.ReturnValue);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(AnnotationSource, false)]
    [InlineData(AnnotationSource, true)]
    [InlineData(ClassFormatSource, false)]
    [InlineData(ClassFormatSource, true)]
    [InlineData(ClassLambdaSource, false)]
    [InlineData(ClassLambdaSource, true)]
    public void CancellationDuringPreparedStorageUnwindsCompilationAndPreservesRefusal(string source, bool refuse)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        // Class names use a temporary frontend workspace. Wait for its refund
        // so these cases interrupt code preparation rather than name binding.
        using var supplied = new CancellingScope(budget.CreateScope(), cancellation, refuse, afterRefund: source != AnnotationSource);
        using (var script = new LythonEngine().Compile(source, supplied, cancellation.Token))
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

    private const string AnnotationSource = "from __future__ import annotations\ndef helper(value: list[str]): pass";
    private const string ClassFormatSource = "class Helper:\n def apply(self, value): return '{value}'.format(value=value)";
    private const string ClassLambdaSource = "class Helper:\n apply = lambda self, value: '{value}'.format(value=value)";

    // A real compiler working-storage admission supplies the cancellation window,
    // after lexing/parsing, without timing or a new production checkpoint hook.
    private sealed class CancellingScope(ILythonMemoryScope inner, CancellationTokenSource cancellation, bool refuse,
        bool afterRefund) : ILythonMemoryScope
    {
        public ILythonMemoryScope CreateScope() => new CancellingScope(inner.CreateScope(), cancellation, refuse, afterRefund);
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var result)) { reservation = null; return false; }
            reservation = new CancellingReservation(result, cancellation, refuse, afterRefund);
            return true;
        }
        public void Dispose() => inner.Dispose();
    }

    private sealed class CancellingReservation(ILythonMemoryReservation inner, CancellationTokenSource cancellation, bool refuse,
        bool afterRefund) : ILythonMemoryReservation
    {
        private bool _refunded;

        public long Bytes => inner.Bytes;
        public bool TryResize(long bytes)
        {
            if (bytes < inner.Bytes) _refunded = true;
            if (bytes > inner.Bytes && (!afterRefund || _refunded))
            {
                cancellation.Cancel();
                if (refuse) return false;
            }
            return inner.TryResize(bytes);
        }
        public void Dispose() => inner.Dispose();
    }
}
