using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GovernedCursorAdmissionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusalAbortsBeforeGuestCursorRetry(bool zip, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var pressureOwner = budget.CreateScope();
        using var timing = new AdmissionPressure(budget, pressureOwner);
        using var script = new LythonEngine().Compile($$"""
            import time
            with open('/ready') as stream:
                stream.read()
            it = {{(zip ? "zip([10,20,30],[1,2,3])" : "enumerate([10,20,30])")}}
            first = next(it)
            denied = False
            assert time.monotonic_ns() == 0
            try:
                next(it)
            except MemoryError:
                denied = True
            assert time.monotonic_ns() == 0
            last = next(it)
            return [denied, last[0], last[1]]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        LythonExecutionResult result;
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            host.SetTiming(timing);
            host.SeedFile("/ready", "ready");
            result = await script.RunAsync(host);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        else
        {
            var host = new MockLythonHost();
            host.SetTiming(timing);
            host.SeedFile("/ready", "ready");
            result = script.Run(host);
        }
        using (result)
        {
            Assert.False(result.Success);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(1, timing.Reads);
        }
        timing.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class AdmissionPressure(LythonMemoryBudget budget, ILythonMemoryScope owner)
        : ILythonTiming, ILythonSynchronousHostCapability, IDisposable
    {
        private ILythonMemoryReservation? _pressure;
        public int Reads { get; private set; }
        public bool CompletesSynchronously => true;
        public long MonotonicResolutionNanoseconds => 1;
        public long MonotonicNanoseconds
        {
            get
            {
                if (++Reads == 1)
                    Assert.True(owner.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out _pressure));
                else Dispose();
                return 0;
            }
        }
        public void Dispose() { _pressure?.Dispose(); _pressure = null; }
        public ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
