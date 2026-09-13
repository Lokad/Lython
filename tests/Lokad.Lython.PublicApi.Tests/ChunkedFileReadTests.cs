using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01/MG21: open() streams fixed windows through ranged host reads instead of
/// loading the whole file, so full scans no longer scale with discarded content.
/// Values, line boundaries, positions and per-value charges match buffered
/// reads; windows that split UTF-8 sequences, CRLF pairs or quoted records
/// reassemble exactly, and bounded acquisition trips small budgets only when
/// retained content genuinely exceeds them.
/// </summary>
public sealed class ChunkedFileReadTests
{
    private const string TrickyText =
        "l1\nl2 with \u00e9\nl3\r\nl4\rl5\n\nastral \U0001F600 end\nnoeol";

    private static readonly List<object?> ExpectedTrickyLines = new()
    {
        "l1\n", "l2 with \u00e9\n", "l3\n", "l4\n", "l5\n", "\n", "astral \U0001F600 end\n", "noeol",
    };

    [Fact]
    public async Task LineIterationMatchesExpectedLines()
    {
        var script = new LythonEngine().Compile(
            """
            lines = []
            with open("/a.txt") as f:
                for line in f:
                    lines.append(line)
            return lines
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedFile("/a.txt", TrickyText);
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(ExpectedTrickyLines, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/a.txt", TrickyText);
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(ExpectedTrickyLines, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ReadlineSizesAndTellTrackTranslatedBytes()
    {
        var script = new LythonEngine().Compile(
            """
            with open("/a.txt") as f:
                a = f.readline()
                t1 = f.tell()
                b = f.read(4)
                t2 = f.tell()
                c = f.readline()
                t3 = f.tell()
                d = f.readline(2)
                assert a == "l1\n", repr(a)
                assert t1 == 3, repr(t1)
                assert b == "l2 w", repr(b)
                assert t2 == 7, repr(t2)
                assert c == "ith \u00e9\n", repr(c)
                assert t3 == 14, repr(t3)
                assert d == "l3", repr(d)
            return t3
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedFile("/a.txt", TrickyText);
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(14), sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/a.txt", TrickyText);
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(14), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ReadSizesSpanWindows()
    {
        var script = new LythonEngine().Compile(
            """
            with open("/a.txt") as f:
                assert f.read(0) == "", "read(0)"
                assert f.read(1) == "l", "read(1)"
                rest = f.read()
                assert rest == "1\nl2 with \u00e9\nl3\nl4\nl5\n\nastral \U0001F600 end\nnoeol", repr(rest)
                assert f.read(10) == "", "read past end"
                assert f.readline() == "", "readline past end"
            return len(rest)
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedFile("/a.txt", TrickyText);
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);

        var host2 = new MockLythonHost();
        host2.SeedFile("/a.txt", TrickyText);
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task WindowsSplitUtf8AndCrlfExactly()
    {
        // 16383 padding bytes put a two-byte character and a CRLF pair across
        // the 16KB window edge: the character's lead byte closes the first
        // window and the carriage return waits for its line feed.
        var content = new string('a', 16383) + "\u00e9\ntail\n";
        var script = new LythonEngine().Compile(
            """
            with open("/w.txt") as f:
                first = f.readline()
                second = f.readline()
                third = f.readline()
                assert first == "a" * 16383 + "\u00e9\n", repr(first[:10])
                assert second == "tail\n", repr(second)
                assert third == "", repr(third)
            return len(first)
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedFile("/w.txt", content);
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(16385), sync.ReturnValue);
        Assert.True(host.MaxRangeBytesServed <= 16 * 1024);

        var host2 = new MockLythonHost();
        host2.SeedFile("/w.txt", content);
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(16385), asyncResult.ReturnValue);

        var crlf = new string('b', 16383) + "\r\ntail\n";
        var crlfScript = new LythonEngine().Compile(
            """
            with open("/c.txt") as f:
                lines = [line for line in f]
                assert lines == ["b" * 16383 + "\n", "tail\n"], repr(len(lines))
            return len(lines)
            """);
        Assert.True(crlfScript.IsValid);

        var host3 = new MockLythonHost();
        host3.SeedFile("/c.txt", crlf);
        var sync3 = crlfScript.Run(host3);
        Assert.True(sync3.Success, sync3.Failure?.Message);
        Assert.Equal(new BigInteger(2), sync3.ReturnValue);

        var host4 = new MockLythonHost();
        host4.SeedFile("/c.txt", crlf);
        var async4 = await crlfScript.RunAsync(host4);
        Assert.True(async4.Success, async4.Failure?.Message);
        Assert.Equal(new BigInteger(2), async4.ReturnValue);
    }

    [Fact]
    public async Task TinyFilesBehave()
    {
        var script = new LythonEngine().Compile(
            """
            with open("/e.txt") as f:
                assert f.read() == "", "empty read"
                assert f.readline() == "", "empty readline"
                assert [line for line in f] == [], "empty iteration"
            with open("/o.txt") as f:
                assert f.read() == "x", "one byte"
            with open("/t.txt") as f:
                assert [line for line in f] == ["ab"], "two bytes"
            with open("/r.txt") as f:
                assert [line for line in f] == ["a\n"], "lone CR"
            with open("/n.txt") as f:
                assert [line for line in f] == ["a"], "no trailing newline"
            return 1
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedFile("/e.txt", string.Empty);
        host.SeedFile("/o.txt", "x");
        host.SeedFile("/t.txt", "ab");
        host.SeedFile("/r.txt", "a\r");
        host.SeedFile("/n.txt", "a");
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(1), sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/e.txt", string.Empty);
        host2.SeedFile("/o.txt", "x");
        host2.SeedFile("/t.txt", "ab");
        host2.SeedFile("/r.txt", "a\r");
        host2.SeedFile("/n.txt", "a");
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task Utf8SigStripsPreambleAndLatin1Decodes()
    {
        var script = new LythonEngine().Compile(
            """
            with open("/s.txt", encoding="utf-8-sig") as f:
                assert f.read() == "hi", repr(f.read())
            with open("/l.txt", encoding="latin-1") as f:
                assert f.read() == "caf\u00e9", "latin-1"
            with open("/e.txt", encoding="latin-1", errors="replace") as f:
                assert f.read() == "AB", "latin-1 maps every byte"
            return 1
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedRawTextUtf8("/s.txt", new byte[] { 0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i' });
        host.SeedBytes("/l.txt", new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 });
        host.SeedBytes("/e.txt", new byte[] { (byte)'A', (byte)'B' });
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(1), sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedRawTextUtf8("/s.txt", new byte[] { 0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i' });
        host2.SeedBytes("/l.txt", new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 });
        host2.SeedBytes("/e.txt", new byte[] { (byte)'A', (byte)'B' });
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NewlineModesSplitIdentically()
    {
        const string content = "a\r\nb\rc\nd";
        var script = new LythonEngine().Compile(
            """
            def lines_for(newline):
                with open("/m.txt", newline=newline) as f:
                    return [line for line in f]
            assert lines_for(None) == ["a\n", "b\n", "c\n", "d"], "universal"
            assert lines_for("") == ["a\r\n", "b\r", "c\n", "d"], "preserve universal"
            assert lines_for("\n") == ["a\r\n", "b\rc\n", "d"], "preserve LF"
            assert lines_for("\r") == ["a\r", "\nb\r", "c\nd"], "preserve CR"
            assert lines_for("\r\n") == ["a\r\n", "b\rc\nd"], "preserve CRLF"
            return 1
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedFile("/m.txt", content);
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(1), sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/m.txt", content);
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StrictFailureSurfacesOnFirstTouch()
    {
        // The damage sits past the first window, so open() succeeds and the
        // failure arrives when iteration first touches the bad bytes, like
        // CPython, with the same UnicodeDecodeError buffered reads raised.
        // The bad byte is assembled raw: a U+00FF character would encode as
        // valid two-byte UTF-8 instead of failing.
        var prefix = Encoding.UTF8.GetBytes("ok\n" + new string('z', 20000));
        var suffix = Encoding.UTF8.GetBytes("\nmore\n");
        var payload = new byte[prefix.Length + 1 + suffix.Length];
        Buffer.BlockCopy(prefix, 0, payload, 0, prefix.Length);
        payload[prefix.Length] = 0xFF;
        Buffer.BlockCopy(suffix, 0, payload, prefix.Length + 1, suffix.Length);
        var script = new LythonEngine().Compile(
            """
            with open("/bad.txt") as f:
                first = f.readline()
                assert first == "ok\n", repr(first)
                try:
                    rest = f.read()
                except UnicodeDecodeError as e:
                    return str(e)
                return "no failure: " + repr(rest)
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedRawTextUtf8("/bad.txt", payload);
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Contains("invalid UTF-8 text", sync.ReturnValue?.ToString(), StringComparison.Ordinal);

        var host2 = new MockLythonHost();
        host2.SeedRawTextUtf8("/bad.txt", payload);
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Contains("invalid UTF-8 text", asyncResult.ReturnValue?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CsvScanStreamsRows()
    {
        var content = new StringBuilder("id,pad\n");
        for (var i = 0; i < 2000; i++)
        {
            content.Append(i).Append(',').Append('y', 50).Append('\n');
        }

        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/r.csv") as f:
                rows = list(csv.reader(f))
                assert len(rows) == 2001, repr(len(rows))
                assert rows[0] == ["id", "pad"], repr(rows[0])
                assert rows[1] == ["0", "y" * 50], repr(rows[1][:1])
                assert rows[2000][0] == "1999", repr(rows[2000][:1])
                total = 0
                for row in rows[1:]:
                    total = total + int(row[0])
                assert total == 1999000, repr(total)
            with open("/r.csv") as f:
                dicts = list(csv.DictReader(f))
                first = dicts[0]
                assert first == {"id": "0", "pad": "y" * 50}, repr(first)
            return total
            """);
        Assert.True(script.IsValid);

        var host = new MockLythonHost();
        host.SeedFile("/r.csv", content.ToString());
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(1999000), sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/r.csv", content.ToString());
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1999000), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task LargeScanStaysBounded()
    {
        // The milestone shape: 100k rows (~5.7MB) aggregate under an 8MB budget.
        // Whole-file acquisition could never fit that budget (~5.7MB of content
        // plus ~18MB of retained line slices alone); windows stream through
        // ranged reads instead, and dropped lines release through the handle
        // pool like CSV fields do. Per-row int() parsing carries its own
        // arithmetic pressure (needs ~16MB here) and stays out of this pin.
        var content = new StringBuilder();
        for (var i = 0; i < 100000; i++)
        {
            content.Append(i).Append(',').Append('y', 50).Append('\n');
        }

        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/big.csv") as f:
                n = 0
                for row in csv.reader(f):
                    n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 8388608 };

        // The 10x pair under one budget: ~10k leading rows complete with room
        // to spare and the full 100k complete under the same budget with a
        // documented peak.
        var smallHost = new MockLythonHost();
        smallHost.SeedFile("/big.csv", content.ToString()[..560000]);
        var small = script.Run(smallHost, options);
        Assert.True(small.Success, small.Failure?.Message);
        Assert.True(small.PeakExecutionMemoryBytes <= 8388608);

        var host = new MockLythonHost();
        host.SeedFile("/big.csv", content.ToString());
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(100000), sync.ReturnValue);
        Assert.True(sync.PeakExecutionMemoryBytes <= 8388608);
        Assert.True(host.MaxRangeBytesServed <= 16 * 1024, "no single ranged call exceeds the engine window");

        var host2 = new MockLythonHost();
        host2.SeedFile("/big.csv", content.ToString());
        var asyncResult = await script.RunAsync(host2, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(100000), asyncResult.ReturnValue);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= 8388608);
        Assert.True(host2.MaxRangeBytesServed <= 16 * 1024, "no single ranged call exceeds the engine window");
    }

    [Fact]
    public async Task DelayedAsyncIterationStreamsLines()
    {
        var script = new LythonEngine().Compile(
            """
            with open("/d.txt") as f:
                lines = [line for line in f]
                assert lines == ["one\n", "two\n", "three"], repr(lines)
                first = open("/d.txt").readline()
                assert first == "one\n", repr(first)
            return len(lines)
            """);
        Assert.True(script.IsValid);

        var host = new DelayedLythonHost();
        host.SeedFile("/d.txt", "one\ntwo\nthree");
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(3), asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}
