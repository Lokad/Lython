using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG07: non-strict decoding builds whole StringBuilder scratch (backslash
/// escapes expand each byte to four chars) before the governed result charge.
/// The entry preflight bounds the worst case first.
/// </summary>
public sealed class TextCodecAccountingTests
{
    [Fact]
    public void BackslashDecodePeakStaysBounded()
    {
        var governor = new MemoryGovernor(65536);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var payload = new byte[200000];
        Array.Fill(payload, (byte)0xFF);
        _ = LythonRuntime.DecodeUtf8Text(
            new ReadOnlyMemory<byte>(new byte[4] { 0xFF, 0xFF, 0xFF, 0xFF }),
            new MemoryGovernor(null),
            span,
            LythonRuntime.TextErrorMode.BackslashReplace,
            LythonRuntime.TextNewlineMode.TranslateUniversal);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            LythonRuntime.DecodeUtf8Text(
                new ReadOnlyMemory<byte>(payload),
                governor,
                span,
                LythonRuntime.TextErrorMode.BackslashReplace,
                LythonRuntime.TextNewlineMode.TranslateUniversal));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.True(allocated < 1048576, $"codec scratch allocated {allocated} bytes before failing");
    }
}
