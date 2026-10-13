using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class CollectedMemoryRecoveryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HostCollectionAfterWithdrawalReconcilesStorageWithoutAnotherCollection(bool asynchronous, bool retain)
    {
        var script = new LythonEngine().Compile($$"""
import time
held = (('x' * 16000,),)
kept = held if {{(retain ? "True" : "False")}} else None
try:
    candidate = 'y' * 16000
except MemoryError:
    pass
del held
assert time.monotonic_ns() == 0
attempts = 0
while {{(retain ? "True" : "False")}} and attempts < 127:
    try:
        candidate = 'z' * 16000
    except MemoryError:
        pass
    attempts += 1
candidate = 'z' * 16000
return len(candidate)
""");
        Assert.True(script.IsValid);
        var timing = new CollectingTiming();
        var host = new MockLythonHost();
        host.SetTiming(timing);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 28000 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.Equal(1, timing.Reads);
        Assert.Equal(timing.CollectionsAfterHostCollection,
            Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount));
        Assert.InRange(result.PeakExecutionMemoryBytes, 16000, 28000);
        if (retain)
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.Failure?.Attribution?.Reason);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(16000), result.ReturnValue);
        }
    }

    private sealed class CollectingTiming : ILythonTiming, ILythonSynchronousHostCapability
    {
        public int Reads { get; private set; }
        public int[] CollectionsAfterHostCollection { get; private set; } = [];
        public long MonotonicNanoseconds
        {
            get
            {
                Reads++;
                GC.Collect(GC.MaxGeneration);
                CollectionsAfterHostCollection = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
                return 0;
            }
        }
        public bool CompletesSynchronously => true;
        public long MonotonicResolutionNanoseconds => 1;
        public ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
