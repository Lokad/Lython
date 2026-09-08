using System.IO.Compression;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Zip;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Differential guard for the strict DEFLATE inflater: every accepted payload must
/// expand to the same bytes as the platform decompressor, and valid platform output
/// must always be accepted. Inputs are fixed (no randomness) so failures reproduce.
/// </summary>
public sealed class StrictDeflateDifferentialTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    private static LythonRuntime.ExecutionContext Ctx(MockLythonHost host)
        => new(host, new LythonRunOptions());

    private static byte[] Sample(int kind, int length)
    {
        var data = new byte[length];
        if (kind == 1) for (var i = 0; i < length; i++) data[i] = (byte)(65 + (i % 26));
        if (kind == 2)
        {
            var state = 123456789u;
            for (var i = 0; i < length; i++)
            {
                state = state * 1664525u + 1013904223u;
                data[i] = (byte)(state >> 24);
            }
        }

        return data;
    }

    private static byte[] BclCompress(byte[] data, int level)
    {
        using var buffer = new MemoryStream();
        using (var compressor = new DeflateStream(
            buffer,
            new ZLibCompressionOptions { CompressionLevel = level },
            leaveOpen: true))
        {
            compressor.Write(data, 0, data.Length);
        }

        return buffer.ToArray();
    }

    private static byte[] BclExpand(byte[] payload)
    {
        using var source = new MemoryStream(payload, writable: false);
        using var deflate = new DeflateStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static uint Crc(byte[] data)
    {
        var host = new MockLythonHost();
        return Crc32.Compute(data, Ctx(host), Span);
    }

    [Fact]
    public void ValidPayloadsMatchPlatformOutput()
    {
        var sizes = new[] { 1, 2, 3, 7, 8, 9, 64, 100, 300, 1000, 5000, 40000 };
        var failures = new List<string>();
        foreach (var size in sizes)
        foreach (var kind in new[] { 0, 1, 2 })
        foreach (var level in new[] { -1, 0, 1, 6, 9 })
        {
            byte[] payload = [];
            try
            {
                var data = Sample(kind, size);
                payload = BclCompress(data, level);
                var host = new MockLythonHost();
                var strict = StrictDeflateInflater.Inflate(payload, "s", (ulong)data.Length, Crc(data), Ctx(host), Span);
                if (!strict.SequenceEqual(data)) failures.Add($"mismatch size={size} kind={kind} level={level} plen={payload.Length}");
            }
            catch (Exception e)
            {
                failures.Add($"throw size={size} kind={kind} level={level} plen={payload.Length}: {e.GetType().Name} {e.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void SingleBitCorruptionNeverProducesFalseAccepts()
    {
        // The strict reader deliberately rejects inputs the platform accepts
        // (missing final block, trailing garbage), but it must never accept an input
        // the platform rejects, and shared accepts must expand identically.
        var data = Sample(1, 100);
        var payload = BclCompress(data, 6);
        var divergences = new List<string>();
        for (var i = 0; i < payload.Length; i++)
        for (var b = 0; b < 8; b++)
        {
            var mutated = (byte[])payload.Clone();
            mutated[i] ^= (byte)(1 << b);
            byte[]? bclOut = null;
            string? bclErr = null;
            try { bclOut = BclExpand(mutated); }
            catch (Exception e) { bclErr = e.GetType().Name; }
            byte[]? strictOut = null;
            string? strictErr = null;
            try
            {
                var host = new MockLythonHost();
                strictOut = StrictDeflateInflater.Inflate(mutated, "s", (ulong)data.Length, Crc(data), Ctx(host), Span);
            }
            catch (LythonRuntimeException e) { strictErr = e.ExceptionType; }
            var bclOk = bclErr is null && bclOut is not null && bclOut.SequenceEqual(data);
            var strictOk = strictErr is null && strictOut is not null && strictOut.SequenceEqual(data);
            if (strictOk && !bclOk) divergences.Add($"FALSE-ACCEPT byte {i} bit {b} bcl={bclErr}");
            if (bclOk && strictOk && !bclOut!.SequenceEqual(strictOut!)) divergences.Add($"output-mismatch byte {i} bit {b}");
        }

        Assert.True(divergences.Count == 0, string.Join(" | ", divergences.Take(10)));
    }
}
