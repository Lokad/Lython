using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FilecmpModuleFunctionTests
{
    [Fact]
    public void CmpSupportsShallowSignaturesExactBytesAndPathLikeValues()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/a.bin", [0x61, 0x62, 0x63]);
        host.SeedBytes("/repo/b.bin", [0x78, 0x79, 0x7a]);
        host.SeedBytes("/repo/c.bin", [0x61, 0x62, 0x63]);
        host.SeedBytes("/repo/short.bin", [0x61]);

        var result = new LythonEngine().Run(
            """
import filecmp
from pathlib import Path

class PathLike:
    def __init__(self, value):
        self.value = value
    def __fspath__(self):
        return self.value

checks = [
    filecmp.cmp("a.bin", "b.bin"),
    filecmp.cmp("a.bin", "b.bin", shallow=False),
    filecmp.cmp(Path("a.bin"), PathLike("c.bin"), False),
    filecmp.cmp("a.bin", "short.bin", False),
    filecmp.cmp("a.bin", "a.bin", False),
    filecmp.clear_cache() is None,
    "cmp" in dir(filecmp),
]
return str(checks)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("[True, False, True, False, True, True, True]", result.ReturnValue);
    }

    [Fact]
    public void DirectoriesReturnFalseAndMissingFilesRaiseFileNotFoundError()
    {
        var host = new MockLythonHost("/repo");
        host.MkDir("/repo/left");
        host.MkDir("/repo/right");

        var directories = new LythonEngine().Run(
            """
import filecmp
return filecmp.cmp("/repo/left", "/repo/right", False)
""",
            host);
        var missing = new LythonEngine().Run(
            """
import filecmp
filecmp.cmp("/repo/missing", "/repo/right")
""",
            host);

        Assert.True(directories.Success, Describe(directories));
        Assert.Equal(false, directories.ReturnValue);
        Assert.False(missing.Success);
        Assert.Equal("FileNotFoundError", missing.Failure?.ExceptionType);
        Assert.Contains("/repo/missing", missing.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncExactComparisonUsesAsyncBoundedBinaryReads()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedBytes("/repo/a.bin", [0x00, 0x7f, 0x80, 0xff]);
        host.SeedBytes("/repo/b.bin", [0x00, 0x7f, 0x80, 0xff]);

        var result = await new LythonEngine().RunAsync(
            """
import filecmp
return filecmp.cmp("a.bin", "b.bin", shallow=False)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal(true, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public void ExactComparisonHonorsTheHostReadLimit()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/a.bin", Enumerable.Repeat((byte)0x61, 32).ToArray());
        host.SeedBytes("/repo/b.bin", Enumerable.Repeat((byte)0x61, 32).ToArray());

        var result = new LythonEngine().Run(
            """
import filecmp
filecmp.cmp("a.bin", "b.bin", shallow=False)
""",
            host,
            new LythonRunOptions { MaxHostReadBytes = 8 });

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("host binary read exceeded maximum bytes (8)", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactComparisonAccountsForBothSimultaneousHostBuffers()
    {
        var host = new MockLythonHost("/repo");
        host.SeedBytes("/repo/a.bin", Enumerable.Repeat((byte)0x61, 40).ToArray());
        host.SeedBytes("/repo/b.bin", Enumerable.Repeat((byte)0x61, 40).ToArray());

        var result = new LythonEngine().Run(
            """
import filecmp
filecmp.cmp("a.bin", "b.bin", shallow=False)
""",
            host,
            new LythonRunOptions { MaxExecutionMemoryBytes = 120 });

        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("execution memory budget exceeded (120)", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionalBinaryCapabilityIsNeededOnlyWhenMetadataCannotAnswer()
    {
        var host = new TextOnlyHost();
        host.SeedFile("/repo/a.txt", "abc");

        var shallow = new LythonEngine().Run(
            """
import filecmp
return filecmp.cmp("/repo/a.txt", "/repo/a.txt")
""",
            host);
        var exact = new LythonEngine().Run(
            """
import filecmp
filecmp.cmp("/repo/a.txt", "/repo/a.txt", False)
""",
            host);

        Assert.True(shallow.Success, Describe(shallow));
        Assert.Equal(true, shallow.ReturnValue);
        Assert.False(exact.Success);
        Assert.Equal("RuntimeError", exact.Failure?.ExceptionType);
        Assert.Contains("host binary file I/O is not available", exact.Failure?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecursiveDirectoryComparisonFailsExplicitly()
    {
        var result = new LythonEngine().Run(
            """
import filecmp
filecmp.dircmp("/repo/a", "/repo/b")
""",
            new MockLythonHost("/repo"));

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains("recursive directory comparison", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticContractsRecognizeFilecmpAndItsCallShapes()
    {
        var valid = new LythonEngine().Compile(
            """
import filecmp
from filecmp import cmp, clear_cache

same = cmp("a", "b", shallow=False)
filecmp.cmp("a", "b")
clear_cache()
""");
        var invalid = new LythonEngine().Compile(
            """
import filecmp
filecmp.cmp("a")
filecmp.clear_cache(1)
filecmp.dircmp("a")
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Equal(3, invalid.Diagnostics.Count(d => d.Code == "LA3151"));
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
