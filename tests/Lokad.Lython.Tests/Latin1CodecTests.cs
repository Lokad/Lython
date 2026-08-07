using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class Latin1CodecTests
{
    [Fact]
    public void CodecAliasesRoundTripEveryByteAcrossBuiltinAndMemberForms()
    {
        var result = new LythonEngine().Run(
            """
payload = bytes("Aéÿ", "latin-1")
decoded = bytes([0, 127, 128, 255]).decode("iso-8859-1")
checks = [
    payload == b"A\xe9\xff",
    str(payload, "latin1") == "Aéÿ",
    "Aéÿ".encode("iso-8859-1") == payload,
    ord(decoded[0]) == 0,
    ord(decoded[1]) == 127,
    ord(decoded[2]) == 128,
    ord(decoded[3]) == 255,
]
return str(checks)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal("[True, True, True, True, True, True, True]", result.ReturnValue);
    }

    [Fact]
    public void EncodeErrorHandlersMatchPythonLatin1Behavior()
    {
        var result = new LythonEngine().Run(
            """
source = "A€😀B"
values = [
    source.encode("latin-1", "ignore").decode("latin-1"),
    source.encode("latin1", "replace").decode("latin1"),
    source.encode("iso-8859-1", "backslashreplace").decode("iso-8859-1"),
    bytes(source, "latin-1", "ignore").decode("latin-1"),
]
try:
    source.encode("latin-1")
except UnicodeEncodeError as ex:
    values.append(ex.type)
    values.append(ex.message)
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "AB|A??B|A\\u20ac\\U0001f600B|AB|UnicodeEncodeError|'latin-1' codec can't encode character '\\u20ac' in position 1: ordinal not in range(256)",
            result.ReturnValue);
    }

    [Fact]
    public void OpenAndPathMethodsUseBoundedBinaryHostCapabilitiesForLatin1()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/input.txt", [0x63, 0x61, 0x66, 0xe9, 0x0d, 0x0a]);
        host.SeedBytes("/repo/append.txt", [0x62, 0x61, 0x73, 0xe9]);

        var result = new LythonEngine().Run(
            """
from pathlib import Path

values = []
values.append(Path("/repo/input.txt").read_text(encoding="latin-1").replace("\n", "<n>"))
with open("/repo/input.txt", "r", encoding="latin1", newline="") as reader:
    values.append(reader.read().replace("\r", "<r>").replace("\n", "<n>"))

values.append(str(Path("/repo/path.txt").write_text("café\n", encoding="iso-8859-1", newline="\r\n")))
with Path("/repo/open.txt").open("w", encoding="latin-1", errors="replace") as writer:
    values.append(str(writer.write("A€B")))

with open("/repo/append.txt", "a", encoding="latin-1") as appender:
    values.append(str(appender.tell()))
    values.append(str(appender.write("!ÿ")))
    values.append(str(appender.tell()))

return "|".join(values)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("café<n>|café<r><n>|5|3|4|2|6", result.ReturnValue);
        Assert.Equal([0x63, 0x61, 0x66, 0xe9, 0x0d, 0x0a], host.ReadBytes("/repo/path.txt"));
        Assert.Equal([0x41, 0x3f, 0x42], host.ReadBytes("/repo/open.txt"));
        Assert.Equal([0x62, 0x61, 0x73, 0xe9, 0x21, 0xff], host.ReadBytes("/repo/append.txt"));
    }

    [Fact]
    public async Task AsyncOpenAndPathMethodsHaveLatin1Parity()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedBytes("/repo/input.txt", [0x63, 0x61, 0x66, 0xe9]);

        var result = await new LythonEngine().RunAsync(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text(encoding="latin1")
with open("/repo/out.txt", "w", encoding="iso-8859-1") as writer:
    writer.write(text + "ÿ")
return text
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("café", result.ReturnValue);
        Assert.Equal([0x63, 0x61, 0x66, 0xe9, 0xff], host.ReadBytes("/repo/out.txt"));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public void StrictPathEncodingFailureDoesNotPartiallyOverwriteHostFile()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/out.txt", [0x6b, 0x65, 0x65, 0x70]);

        var result = new LythonEngine().Run(
            """
from pathlib import Path
Path("/repo/out.txt").write_text("€", encoding="latin-1")
""",
            host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("UnicodeEncodeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Equal([0x6b, 0x65, 0x65, 0x70], host.ReadBytes("/repo/out.txt"));
    }

    [Fact]
    public void Latin1HostReadsHonorTheConfiguredByteLimit()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/input.txt", Enumerable.Repeat((byte)0x61, 32).ToArray());

        var result = new LythonEngine().Run(
            """
from pathlib import Path
Path("/repo/input.txt").read_text(encoding="latin-1")
""",
            host,
            new LythonRunOptions { MaxHostReadBytes = 8 });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("host binary read exceeded maximum bytes (8)", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Latin1HostAccessFailsExplicitlyWhenOptionalBinaryCapabilityIsMissing()
    {
        var host = new TextOnlyHost();
        host.SeedFile("/repo/input.txt", "alpha");

        var read = new LythonEngine().Run(
            """
from pathlib import Path
Path("/repo/input.txt").read_text(encoding="latin-1")
""",
            host);
        var write = new LythonEngine().Run(
            """
from pathlib import Path
Path("/repo/out.txt").write_text("alpha", encoding="latin-1")
""",
            host);

        Assert.Equal("RuntimeError", read.Failure?.ExceptionType);
        Assert.Contains("host binary file I/O is not available", read.Failure?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("RuntimeError", write.Failure?.ExceptionType);
        Assert.Contains("host binary file I/O is not available", write.Failure?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Latin1AliasesRemainAcceptedByStaticTextContracts()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path
import argparse

open("/repo/a.txt", "r", encoding="latin-1")
Path("/repo/b.txt").read_text(encoding="latin1")
Path("/repo/c.txt").write_text("x", encoding="iso-8859-1")
Path("/repo/d.txt").open("w", encoding="latin-1")
argparse.FileType("r", encoding="latin1")
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
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
