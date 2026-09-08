using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// The shared CRC-32 component (reuse-map extraction of the previously
/// duplicated gzip and archive checksum paths) matches trusted CPython
/// vectors, keeps budgeted and incremental accumulation identical across chunk
/// boundaries, and enforces step budgets and cancellation.
/// </summary>
public sealed class Crc32SubsystemTests
{
    private static uint FinalizeUpdate(byte[] data)
        => ~Crc32.Update(uint.MaxValue, data);

    [Fact]
    public void KnownVectorsMatchTrustedValues()
    {
        Assert.Equal(0u, FinalizeUpdate([]));
        Assert.Equal(0xCBF43926u, FinalizeUpdate(Encoding.ASCII.GetBytes("123456789")));
        Assert.Equal(0x414FA339u, FinalizeUpdate(Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog")));
        Assert.Equal(0x38F3DB17u, FinalizeUpdate(Encoding.ASCII.GetBytes("hello stored\n")));
    }

    [Fact]
    public void BudgetedComputeMatchesIncrementalUpdatesAcrossChunkEdges()
    {
        foreach (var length in new[] { 0, 1, 4095, 4096, 4097, 9000 })
        {
            var data = new byte[length];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)((i * 31 + 7) & 0xFF);
            }

            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
            Assert.Equal(FinalizeUpdate(data), Crc32.Compute(data, context, span: null));

            // Incremental one-byte-at-a-time accumulation (the streaming
            // pattern) must agree with whole-buffer checksums.
            var incremental = uint.MaxValue;
            foreach (var value in data)
            {
                incremental = Crc32.Update(incremental, [value]);
            }

            Assert.Equal(FinalizeUpdate(data), ~incremental);
        }
    }

    [Fact]
    public void ComputeEnforcesStepBudget()
    {
        var data = new byte[5000];
        var context = new LythonRuntime.ExecutionContext(
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 1 });
        var failure = Assert.Throws<LythonRuntimeException>(() => Crc32.Compute(data, context, span: null));
        Assert.Equal("RuntimeError", failure.ExceptionType);
        Assert.Contains("maximum execution step count exceeded", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputeHonorsCancellation()
    {
        var data = new byte[5000];
        using var source = new CancellationTokenSource();
        source.Cancel();
        var context = new LythonRuntime.ExecutionContext(
            new MockLythonHost(),
            new LythonRunOptions { CancellationToken = source.Token });
        var failure = Assert.Throws<LythonRuntimeException>(() => Crc32.Compute(data, context, span: null));
        Assert.Equal("RuntimeError", failure.ExceptionType);
        Assert.Contains("execution canceled", failure.Message, StringComparison.Ordinal);
    }
}
