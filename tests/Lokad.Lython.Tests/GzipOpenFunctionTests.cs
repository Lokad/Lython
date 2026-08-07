using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class GzipOpenFunctionTests
{
    [Fact]
    public void BinaryHandlesSupportSequentialReadWriteFlushAndIteration()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip
from pathlib import Path

writer = gzip.open(Path("/repo/data.gz"), "wb", 6)
values = [str(writer.writable()), str(writer.readable()), str(writer.seekable())]
values.append(str(writer.write(b"alpha\nbeta\nlast")))
writer.flush()
writer.flush()
values.append(str(writer.tell()))
writer.close()
writer.close()
values.append(str(writer.closed))

with gzip.open("/repo/data.gz") as reader:
    values.append(reader.read(5).decode())
    values.append(reader.readline().decode().replace("\n", "<n>"))
    values.append(str([line.decode().rstrip() for line in reader]))
    values.append(str(reader.read() == b""))

return "|".join(values)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("True|False|False|15|15|True|alpha|<n>|['beta', 'last']|True", result.ReturnValue);
        Assert.Equal(0x1f, host.ReadBytes("/repo/data.gz")[0]);
    }

    [Fact]
    public void TextHandlesReuseCodecAndNewlineSemantics()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip

with gzip.open("/repo/text.gz", "wt", encoding="latin-1", newline="\r\n") as writer:
    count = writer.write("café\nline\n")
    encoding = writer.encoding
    errors = writer.errors

with gzip.open("/repo/text.gz", "rt", encoding="latin1") as reader:
    translated = reader.read().replace("\n", "<n>")

with gzip.open("/repo/text.gz", "rt", encoding="iso-8859-1", newline="") as reader:
    preserved = reader.read().replace("\r", "<r>").replace("\n", "<n>")

with gzip.open("/repo/text.gz", "rt", encoding="latin-1") as reader:
    lines = [line.rstrip() for line in reader]

with gzip.open("/repo/sig.gz", "wt", encoding="utf-8-sig") as writer:
    writer.write("a")
    writer.write("b")
sig_bytes = gzip.open("/repo/sig.gz", "rb").read()

return "|".join([str(count), encoding, errors, translated, preserved, str(lines), str(sig_bytes == b"\xef\xbb\xbfab")])
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("10|iso8859-1|strict|café<n>line<n>|café<r><n>line<r><n>|['café', 'line']|True", result.ReturnValue);
    }

    [Fact]
    public void AppendWritesOneStableAdditionalMemberWithoutFlushDuplication()
    {
        var host = new MockLythonHost("/repo");
        var result = new LythonEngine().Run(
            """
import gzip

with gzip.open("/repo/data.gz", "wb") as writer:
    writer.write(b"alpha")

appender = gzip.open("/repo/data.gz", "ab")
appender.write(b"beta")
appender.flush()
appender.flush()
appender.write(b"gamma")
appender.close()

with gzip.open("/repo/data.gz", "rb") as reader:
    return reader.read()
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("alphabetagamma"u8.ToArray(), Assert.IsType<byte[]>(result.ReturnValue));
        Assert.Equal(2, CountMagicHeaders(host.ReadBytes("/repo/data.gz")));
    }

    [Fact]
    public async Task AsyncHandlesUseAsyncHostReadsAndWrites()
    {
        var host = new DelayedLythonHost("/repo");
        var result = await new LythonEngine().RunAsync(
            """
import gzip

with gzip.open("/repo/data.gz", "wt") as writer:
    writer.write("alpha\nbeta")
with gzip.open("/repo/data.gz", "rt") as reader:
    return reader.read()
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("alpha\nbeta", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task AsyncOpenHonorsRunCancellation()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedBytes("/repo/data.gz", PythonHelloGzip);
        using var cancellation = new CancellationTokenSource();

        var task = new LythonEngine().RunAsync(
            """
import gzip
gzip.open("/repo/data.gz", "rb").read()
""",
            host,
            cancellationToken: cancellation.Token);
        cancellation.Cancel();
        var result = await task;

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingBinaryCapabilityAndReadLimitsFailExplicitly()
    {
        var textOnly = new TextOnlyHost();
        textOnly.SeedFile("/repo/data.gz", "not binary");
        var unavailable = new LythonEngine().Run(
            """
import gzip
gzip.open("/repo/data.gz", "rb")
""",
            textOnly);
        var unavailableWrite = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/out.gz", "wb") as writer:
    writer.write(b"alpha")
""",
            textOnly);

        var bounded = new MockLythonHost("/repo");
        bounded.SeedBytes("/repo/data.gz", PythonHelloGzip);
        var limited = new LythonEngine().Run(
            """
import gzip
gzip.open("/repo/data.gz", "rb")
""",
            bounded,
            new LythonRunOptions { MaxHostReadBytes = 8 });

        Assert.Equal("RuntimeError", unavailable.Failure?.ExceptionType);
        Assert.Contains("host binary file I/O is not available", unavailable.Failure?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("RuntimeError", unavailableWrite.Failure?.ExceptionType);
        Assert.Contains("host binary file I/O is not available", unavailableWrite.Failure?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("RuntimeError", limited.Failure?.ExceptionType);
        Assert.Contains("host binary read exceeded maximum bytes (8)", limited.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationFailureDoesNotPartiallyOverwriteTheHostFile()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/data.gz", PythonHelloGzip);

        var result = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/data.gz", "wt", encoding="latin-1") as writer:
    writer.write("€")
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("UnicodeEncodeError", result.Failure?.ExceptionType);
        Assert.Equal(PythonHelloGzip, host.ReadBytes("/repo/data.gz"));
    }

    [Fact]
    public void UnsupportedModesTypesAndBinaryTextOptionsFailPrecisely()
    {
        var result = new LythonEngine().Run(
            """
import gzip

values = []
for mode in ["r+", "xb"]:
    try:
        gzip.open("/repo/data.gz", mode)
    except NotImplementedError as ex:
        values.append(ex.type)
try:
    gzip.open("/repo/data.gz", "rb", encoding="utf-8")
except ValueError as ex:
    values.append(ex.type)
try:
    gzip.open(None)
except TypeError as ex:
    values.append(ex.type)
return "|".join(values)
""",
            new MockLythonHost("/repo"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("NotImplementedError|NotImplementedError|ValueError|TypeError", result.ReturnValue);
    }

    [Fact]
    public void SeekAndArbitraryFileObjectsRemainExplicitlyUnsupported()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/data.gz", PythonHelloGzip);
        var seek = new LythonEngine().Run(
            """
import gzip
with gzip.open("/repo/data.gz", "rb") as reader:
    reader.seek(0)
""",
            host);
        var fileObject = new LythonEngine().Run(
            """
import gzip
gzip.open(gzip.open("/repo/data.gz", "rb"), "rb")
""",
            host);

        Assert.Equal("NotImplementedError", seek.Failure?.ExceptionType);
        Assert.Contains("seek/random access", seek.Failure?.Message, StringComparison.Ordinal);
        Assert.Equal("TypeError", fileObject.Failure?.ExceptionType);
        Assert.Contains("path-like", fileObject.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CorruptHostPayloadRaisesBadGzipFileBeforeReturningAHandle()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/bad.gz", [0x1f, 0x8b, 0x08]);

        var result = new LythonEngine().Run(
            """
import gzip
gzip.open("/repo/bad.gz", "rb")
""",
            host);

        Assert.False(result.Success);
        Assert.Equal("BadGzipFile", result.Failure?.ExceptionType);
    }

    [Fact]
    public void StaticContractsRecognizeGzipOpenCallShape()
    {
        var valid = new LythonEngine().Compile(
            """
import gzip
from gzip import open as gzip_open

gzip.open("a.gz")
gzip_open("b.gz", "wt", 6, "utf-8", "strict", "")
""");
        var invalid = new LythonEngine().Compile(
            """
import gzip
gzip.open()
gzip.open("a", "rb", 9, None, None, None, "extra")
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Equal(2, invalid.Diagnostics.Count(d => d.Code == "LA3151"));
    }

    private static readonly byte[] PythonHelloGzip =
    [
        0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0xff,
        0xcb, 0x48, 0xcd, 0xc9, 0xc9, 0x07, 0x00, 0x86, 0xa6, 0x10,
        0x36, 0x05, 0x00, 0x00, 0x00,
    ];

    private static int CountMagicHeaders(byte[] payload)
    {
        var count = 0;
        for (var i = 0; i + 1 < payload.Length; i++)
        {
            if (payload[i] == 0x1f && payload[i + 1] == 0x8b)
            {
                count++;
            }
        }

        return count;
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));

    private sealed class TextOnlyHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => true;

        private readonly MockLythonHost _inner = new("/repo");

        public string Cwd => _inner.Cwd;
        public DateTimeOffset LocalNow => _inner.LocalNow;
        public DateTimeOffset UtcNow => _inner.UtcNow;

        public void SeedFile(string path, string text) => _inner.SeedFile(path, text);

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
            => _inner.ReadTextUtf8Async(path, cancellationToken);

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.WriteTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
            => _inner.ExistsAsync(path, cancellationToken);

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirAsync(path, cancellationToken);

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
            => _inner.MkDirAsync(path, cancellationToken);

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
            => _inner.RemoveAsync(path, cancellationToken);

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.CopyAsync(source, destination, cancellationToken);

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.MoveAsync(source, destination, cancellationToken);

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
            => _inner.StatAsync(path, cancellationToken);
    }
}
