using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG01/MG21: chunked windows reassemble exactly at every byte alignment, so a
/// split multi-byte character, CRLF pair or preamble can never corrupt or shift
/// content. Tiny windows force every split shape deterministically.
/// </summary>
public sealed class ChunkedReadAlignmentTests
{
    private const string Content = "a\u00e9\nb\r\nc\rd\U0001F600e\nf";

    private static readonly List<object?> ExpectedLines = new()
    {
        "a\u00e9\n", "b\n", "c\n", "d\U0001F600e\n", "f",
    };

    private static LythonRuntime.ExecutionContext.ChunkedTextFileReadState OpenState(
        MockLythonHost host, string path, int window)
    {
        return OpenState(host, path, window, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.TranslateUniversal);
    }

    private static LythonRuntime.ExecutionContext.ChunkedTextFileReadState OpenState(
        MockLythonHost host, string path, int window,
        LythonRuntime.TextEncodingMode encoding, LythonRuntime.TextErrorMode errors,
        LythonRuntime.TextNewlineMode newline)
    {
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(
            path, context, encoding, errors, newline, window);
        state.Prime();
        return state;
    }

    [Fact]
    public void TinyWindowsReassembleLinesExactly()
    {
        foreach (var window in new[] { 1, 2, 3, 4, 5, 7, 9, 16 })
        {
            var host = new MockLythonHost();
            host.SeedFile("/a.txt", Content);
            var state = OpenState(host, "/a.txt", window);

            var lines = new List<object?>();
            while (true)
            {
                var line = state.ReadLine(-1);
                if (line.Length == 0)
                {
                    break;
                }

                lines.Add(line.AsString());
            }

            Assert.Equal(ExpectedLines, lines);
            Assert.Equal(new System.Numerics.BigInteger(16), state.Position);
        }
    }

    [Fact]
    public void TinyWindowsServeSizedReadsExactly()
    {
        foreach (var window in new[] { 1, 2, 3, 5, 8 })
        {
            var host = new MockLythonHost();
            host.SeedFile("/a.txt", Content);
            var state = OpenState(host, "/a.txt", window);

            Assert.Equal("a\u00e9\n", state.Read(3).AsString());
            Assert.Equal("b\n", state.Read(2).AsString());
            Assert.Equal(new System.Numerics.BigInteger(6), state.Position);
            Assert.Equal("c\n", state.ReadLine(-1).AsString());
            Assert.Equal("d\U0001F600", state.Read(2).AsString());
            Assert.Equal("e\nf", state.Read(10).AsString());
            Assert.Equal(string.Empty, state.Read(1).AsString());
            Assert.Equal(new System.Numerics.BigInteger(16), state.Position);
        }
    }

    [Fact]
    public void TinyWindowsDecodeLatin1Exactly()
    {
        foreach (var window in new[] { 1, 2, 3, 5 })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/l.txt", new byte[] { 0xE9, (byte)'\r', (byte)'\n', (byte)'A' });
            var state = OpenState(host, "/l.txt", window,
                LythonRuntime.TextEncodingMode.Latin1, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal);

            var lines = new List<object?>();
            while (true)
            {
                var line = state.ReadLine(-1);
                if (line.Length == 0)
                {
                    break;
                }

                lines.Add(line.AsString());
            }

            Assert.Equal(new List<object?> { "\u00e9\n", "A" }, lines);
            Assert.Equal(new System.Numerics.BigInteger(4), state.Position);
        }
    }

    [Fact]
    public void TinyWindowsSplitNewlineModesExactly()
    {
        var cases = new (string Name, LythonRuntime.TextNewlineMode Mode, List<object?> Lines)[]
        {
            ("universal", LythonRuntime.TextNewlineMode.TranslateUniversal,
                new List<object?> { "a\n", "b\n", "c\n", "d" }),
            ("preserve", LythonRuntime.TextNewlineMode.PreserveUniversal,
                new List<object?> { "a\r\n", "b\r", "c\n", "d" }),
            ("lf", LythonRuntime.TextNewlineMode.PreserveLineFeed,
                new List<object?> { "a\r\n", "b\rc\n", "d" }),
            ("cr", LythonRuntime.TextNewlineMode.PreserveCarriageReturn,
                new List<object?> { "a\r", "\nb\r", "c\nd" }),
            ("crlf", LythonRuntime.TextNewlineMode.PreserveCarriageReturnLineFeed,
                new List<object?> { "a\r\n", "b\rc\nd" }),
        };

        foreach (var window in new[] { 1, 2, 3, 4 })
        {
            foreach (var (name, mode, expected) in cases)
            {
                var host = new MockLythonHost();
                host.SeedFile("/m.txt", "a\r\nb\rc\nd");
                var state = OpenState(host, "/m.txt", window,
                    LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, mode);

                var lines = new List<object?>();
                while (true)
                {
                    var line = state.ReadLine(-1);
                    if (line.Length == 0)
                    {
                        break;
                    }

                    lines.Add(line.AsString());
                }

                Assert.Equal(expected, lines);
            }
        }
    }

    [Fact]
    public void TinyWindowsStripPartialPreambles()
    {
        foreach (var window in new[] { 1, 2, 3, 4 })
        {
            var host = new MockLythonHost();
            host.SeedRawTextUtf8("/s.txt", new byte[] { 0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i' });
            var state = OpenState(host, "/s.txt", window,
                LythonRuntime.TextEncodingMode.Utf8Bom, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal);
            Assert.Equal("hi", state.Read(-1).AsString());

            // A short file never holds the full preamble, so nothing strips and
            // the primed first window rejects the truncated bytes at open like
            // whole reads.
            var host2 = new MockLythonHost();
            host2.SeedRawTextUtf8("/s.txt", new byte[] { 0xEF, 0xBB });
            var failure = Assert.Throws<LythonRuntimeException>(() => OpenState(host2, "/s.txt", window,
                LythonRuntime.TextEncodingMode.Utf8Bom, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal));
            Assert.Equal("UnicodeDecodeError", failure.ExceptionType);
        }
    }
}
