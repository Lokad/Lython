using System.Numerics;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardeningPublicTests
{
    [Fact]
    public void CopyAndHashlibStateIsIsolatedAcrossPublicRuns()
    {
        var engine = new LythonEngine();
        var first = engine.Run(
            """
import copy
import hashlib
copy.dispatch_table["review_marker"] = 123
hashlib.algorithms_available.clear()
return copy.dispatch_table.get("review_marker", "absent")
""",
            new MinimalHost());
        Assert.True(first.Success, first.Failure?.Message);
        var second = engine.Run(
            """
import copy
import hashlib
return [copy.dispatch_table.get("review_marker", "absent"), len(hashlib.algorithms_available) > 0]
""",
            new MinimalHost());
        Assert.True(second.Success, second.Failure?.Message);
        var list = Assert.IsType<List<object?>>(second.ReturnValue);
        Assert.Equal("absent", list[0]);
        Assert.Equal(true, list[1]);
    }

    [Fact]
    public async Task ContextManagerExitIsInvokedExactlyOncePublicAsync()
    {
        var result = await new LythonEngine().RunAsync(
            """
calls = []
class Manager:
    def __enter__(self):
        return self
    def __exit__(self, exc_type, exc, tb):
        calls.append("exit")
        raise ValueError("exit boom")
try:
    with Manager():
        pass
except ValueError:
    pass
return len(calls)
""",
            new MinimalHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(1), result.ReturnValue);
    }

    [Fact]
    public async Task SequenceMatcherRatioParityThroughPublicAssembly()
    {
        const string Source = """
import difflib
matcher = difflib.SequenceMatcher(None, "abcd", "abXcd")
return [matcher.ratio(), matcher.quick_ratio()]
""";
        var sync = new LythonEngine().Run(Source, new MinimalHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await new LythonEngine().RunAsync(Source, new MinimalHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public void CustomWriteTextMethodCompilesCleanThroughPublicAssembly()
    {
        var compiled = new LythonEngine().Compile(
            """
class Writer:
    def write_text(self, value):
        return len(value)
print(Writer().write_text(b"abc"))
""");
        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Code == "LA3046");
    }

    [Fact]
    public async Task CustomIterationParityThroughPublicAssembly()
    {
        const string Source = """
class C:
    def __iter__(self):
        return iter([2, 1])
v = C()
result = [list(v), sorted(v), min(v)]
def f(*args):
    return list(args)
result.append(f(*v))
return result
""";
        var sync = new LythonEngine().Run(Source, new MinimalHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(4, Assert.IsType<List<object?>>(sync.ReturnValue).Count);
        var asyncResult = await new LythonEngine().RunAsync(Source, new MinimalHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var outer = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal(4, outer.Count);
        Assert.Equal(new BigInteger(1), outer[2]);
    }

    [Fact]
    public void DeepNestingIsRejectedThroughPublicAssembly()
    {
        foreach (var source in new[]
        {
            "x = 1" + string.Concat(Enumerable.Repeat("**1", 100)) + "\n",
            "x = " + string.Concat(Enumerable.Repeat("1+", 100)) + "1\n",
        })
        {
            var compiled = new LythonEngine().Compile(source);
            Assert.False(compiled.IsValid);
            Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
        }
    }

    [Fact]
    public async Task SortedOrderingParityThroughPublicAssembly()
    {
        const string Source = """
calls = []
class C:
    def __iter__(self):
        calls.append("iter")
        return iter([2, 1])
try:
    sorted(C(), key=1)
except TypeError:
    calls.append("caught")
return calls
""";
        var sync = new LythonEngine().Run(Source, new MinimalHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?> { "iter", "caught" },
            Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await new LythonEngine().RunAsync(Source, new MinimalHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(
            new List<object?> { "iter", "caught" },
            Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public void ByteLimitsRejectNegativeValuesPublic()

    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LythonEngine().Run(
                "return 42",
                new MinimalHost(),
                new LythonRunOptions { MaxHostReadBytes = -4294967296 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LythonEngine().Run(
                "return 42",
                new MinimalHost(),
                new LythonRunOptions { MaxStandardErrorBytes = long.MinValue }));
    }

    private sealed class MinimalHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public string Cwd => "/";
        public bool CompletesSynchronously => true;
        public DateTimeOffset LocalNow => new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
            => throw new DirectoryNotFoundException(path);
        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => throw new NotSupportedException(path);
        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => throw new NotSupportedException(path);
        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
            => ValueTask.FromResult(false);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
            => throw new DirectoryNotFoundException(path);
        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
            => throw new NotSupportedException(path);
        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
            => throw new NotSupportedException(path);
        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
            => throw new NotSupportedException(source);
        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
            => throw new NotSupportedException(source);
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
            => ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
    }
}
