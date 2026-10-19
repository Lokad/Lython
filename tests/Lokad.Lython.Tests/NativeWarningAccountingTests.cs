using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeWarningAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public async Task RepeatedNoticeAtOneLocationDoesNotGrowRegistryOrOutputOwnership()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        await LythonRuntime.EmitDefaultWarningAsync("FutureWarning", "notice", context, Span, true);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var length = context.State.StandardError.Length;
        for (var index = 0; index < 10000; index++)
            await LythonRuntime.EmitDefaultWarningAsync("FutureWarning", "notice", context, Span, true);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(length, context.State.StandardError.Length);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedRegistryGrowthPreservesPriorNoticesAndFunding()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024) },regexProvider:Utf8RegexProvider.Instance);
        Assert.True(context.State.MarkDefaultWarning("/file.py", 1, "FutureWarning", "notice", Span));
        Assert.True(context.State.MarkDefaultWarning("/file.py", 2, "FutureWarning", "notice", Span));
        using var pressure = context.MemoryGovernor.ReserveTemporary(384, Span);
        for (var index = 0; index < 20; index++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                context.State.MarkDefaultWarning("/file.py", 3, "FutureWarning", "notice", Span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(640, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(384, context.MemoryGovernor.CurrentReservedBytes);
            Assert.False(context.State.MarkDefaultWarning("/file.py", 1, "FutureWarning", "notice", Span));
        }
        pressure.Dispose();
        Assert.Throws<LythonRuntimeException>(() => context.State.MarkDefaultWarning("/file.py", 3, "FutureWarning", "notice", Span));
        Assert.Equal(640, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public async Task CapturedOutputDenialRefundsFormattingAndThePrivateUtf8Value()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(512) },regexProvider:Utf8RegexProvider.Instance);
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await LythonRuntime.EmitDefaultWarningAsync("FutureWarning", "notice", context, Span, true));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, context.State.StandardError.Length);
        Assert.Equal(384, context.MemoryGovernor.CurrentCommittedBytes); // Only the warning registry survives.
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
