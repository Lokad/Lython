using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SetConstructionAbortTests
{
    private const long OneMib = 1048576;

    [Theory]
    [InlineData("set(failed())")]
    [InlineData("{0, *failed()}")]
    [InlineData("{item for item in failed()}")]
    [InlineData("retained.union(failed())")]
    [InlineData("retained.intersection(failed())")]
    [InlineData("retained.difference(failed())")]
    [InlineData("retained.symmetric_difference(failed())")]
    public async Task RepeatedIteratorFailuresKeepLiveStorageBounded(string expression)
    {
        var script = Compile($$"""
            retained = {1, 2}
            side_effects = [0]
            def failed():
                yield 1
                side_effects[0] += 1
                raise ValueError('stop')
            caught = 0
            for i in range(5000):
                try:
                    {{expression}}
                except ValueError:
                    caught += 1
            return [caught, side_effects[0], sorted(retained)]
            """);
        await AssertBothModes(script, [new BigInteger(5000), new BigInteger(5000), new List<object?> { new BigInteger(1), new BigInteger(2) }]);
    }

    [Theory]
    [InlineData("set([1, []])")]
    [InlineData("{1, []}")]
    [InlineData("{item for item in [1, []]}")]
    [InlineData("set(42)")]
    public async Task RepeatedValidationFailuresRefundOnlyPrivateResults(string expression)
    {
        var script = Compile($$"""
            retained = {1, 2}
            caught = 0
            for i in range(5000):
                try:
                    {{expression}}
                except TypeError:
                    caught += 1
            return [caught, sorted(retained)]
            """);
        await AssertBothModes(script, [new BigInteger(5000), new List<object?> { new BigInteger(1), new BigInteger(2) }]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedIteratorFailureOrCancellationKeepsHostEffects(bool cancel)
    {
        var timing = new HeldFirstDelay();
        var host = new MockLythonHost();
        host.SetTiming(timing);
        using var cancellation = new CancellationTokenSource();
        var script = Compile("""
            import time
            def failed():
                yield 1
                time.sleep(0)
                raise ValueError('stop')
            caught = 0
            for i in range(5000):
                try:
                    set(failed())
                except ValueError:
                    caught += 1
            return caught
            """);
        var run = script.RunAsync(host, new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, CancellationToken = cancellation.Token });
        await timing.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(run.IsCompleted);
        if (cancel) cancellation.Cancel();
        else timing.Release.TrySetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, OneMib);
        if (cancel)
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
            Assert.Equal(1, timing.Calls);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(5000), result.ReturnValue);
            Assert.Equal(5000, timing.Calls);
        }
    }

    [Fact]
    public async Task RepeatedCollectionDenialsKeepPrivateConstructionBounded()
    {
        var script = Compile("""
            caught = 0
            for i in range(5000):
                try:
                    set(range(5))
                except RuntimeError:
                    caught += 1
            return caught
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib, MaxCollectionSize = 3 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(5000), sync.ReturnValue);
        var asynchronous = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(new BigInteger(5000), asynchronous.ReturnValue);
    }

    [Fact]
    public async Task FailedLiveUpdateKeepsItsPartialMutationAndCharge()
    {
        var script = Compile("""
            retained = {0}
            def failed():
                yield 1
                raise ValueError('stop')
            for i in range(5000):
                try:
                    retained.update(failed())
                except ValueError:
                    pass
            return sorted(retained)
            """);
        await AssertBothModes(script, [new BigInteger(0), new BigInteger(1)]);
    }

    [Theory]
    [InlineData("retained.intersection([1, 2])")]
    [InlineData("retained.intersection_update([1, 2])")]
    [InlineData("retained.issubset([1, 2])")]
    public async Task SuccessfulScratchWorkKeepsLiveStorageBounded(string expression)
    {
        var script = Compile($$"""
            retained = {1, 2}
            for i in range(5000):
                {{expression}}
            return sorted(retained)
            """);
        await AssertBothModes(script, [new BigInteger(1), new BigInteger(2)]);
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        return script;
    }

    private static async Task AssertBothModes(LythonCompiledScript script, List<object?> expected)
    {
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        Assert.InRange(sync.PeakExecutionMemoryBytes, 1, OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.InRange(asyncResult.PeakExecutionMemoryBytes, 1, OneMib);
    }

    private sealed class HeldFirstDelay : ILythonTiming
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        public long MonotonicNanoseconds => 0;

        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref Calls) == 1)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
