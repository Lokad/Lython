using System.Text;
using System.Numerics;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PublicRuntimeCompatibilityTests
{
    [Fact]
    public void TestsExecuteAgainstTheReferencedProductionAssembly()
    {
        var productionAssembly = typeof(LythonEngine).Assembly;
        var testAssembly = typeof(PublicRuntimeCompatibilityTests).Assembly;

        Assert.Equal("Lokad.Lython", productionAssembly.GetName().Name);
        Assert.NotSame(testAssembly, productionAssembly);
        Assert.Equal("Lokad.Lython.PublicApi.Tests", testAssembly.GetName().Name);
    }

    [Fact]
    public void PublicEngineRunsAgentFacingCoreAndStandardLibraryBehavior()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
import gzip
import hashlib
import shlex
import time

digest = hashlib.sha256(b"abc").hexdigest()
roundtrip = gzip.decompress(gzip.compress(b"payload", mtime=0)) == b"payload"
words = shlex.split("one 'two three'")
return digest[:8] + "|" + str(roundtrip) + "|" + str(words) + "|" + str(time.gmtime(0).tm_year)
""",
            new PublicTestHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ba7816bf|True|['one', 'two three']|1970", result.ReturnValue);
    }

    [Fact]
    public async Task PublicCompiledScriptHasSyncAndAsyncParity()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "return '|'.join(str(value * value) for value in range(6))");

        var sync = script.Run(new PublicTestHost());
        var asyncResult = await script.RunAsync(new PublicTestHost());

        Assert.True(script.IsValid);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("0|1|4|9|16|25", sync.ReturnValue);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public void PublicHostMediatesFilesAndStandardOutput()
    {
        var host = new PublicTestHost();
        host.SeedText("/input.txt", "hello");

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
with open("/input.txt") as source:
    value = source.read()
with open("/output.txt", "w") as output:
    output.write(value.upper())
print(value, end="!")
return value
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello", result.ReturnValue);
        Assert.Equal("HELLO", host.ReadText("/output.txt"));
        Assert.Equal("hello!", result.StandardOutput);
        Assert.Equal("hello!", host.StandardOutputText);
    }

    [Fact]
    public void PublicDiagnosticsAndLimitValidationRemainExplicit()
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile("if True print('missing colon')");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic =>
            diagnostic.Severity == LythonDiagnosticSeverity.Error);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LythonEngine(Utf8RegexProvider.Instance).Run(
                "return 1",
                new PublicTestHost(),
                new LythonRunOptions { MaxRecursionDepth = -1 }));

        var diagnostic = new LythonDiagnostic("LA1234", "message", LythonDiagnosticSeverity.Error, null);
        var (code, message, severity, span) = diagnostic;
        Assert.Equal("LA1234", code);
        Assert.Equal("message", message);
        Assert.Equal(LythonDiagnosticSeverity.Error, severity);
        Assert.Null(span);
        Assert.Throws<ArgumentException>(() =>
            new LythonDiagnostic("invalid", "message", LythonDiagnosticSeverity.Error, null));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(8, false)]
    [InlineData(8, true)]
    public async Task NullableIntegerRecursionOptionPreservesDefaultAndZero(int? depth, bool asynchronous)
    {
        using var script = new LythonEngine().Compile("def helper():\n    return 42\nreturn helper()\n");
        var options = new LythonRunOptions { MaxRecursionDepth = depth };
        int? configured = options.MaxRecursionDepth;
        Assert.Equal(depth, configured);
        using var result = asynchronous ? await script.RunAsync(new PublicTestHost(), options)
            : script.Run(new PublicTestHost(), options);
        if (depth == 0)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.RecursionDepthLimit, result.FailureAttribution?.Reason);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(42), result.ReturnValue);
        }
    }

    [Fact]
    public void PublicBoundaryProjectsNestedPythonValuesToClrShapes()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "return [seed, (2, None), {'name': 'lython', 'ready': True}]",
            new PublicTestHost(),
            new Dictionary<string, object?> { ["seed"] = 41 });

        Assert.True(result.Success, result.Failure?.Message);
        var list = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(41, list[0]);

        var tuple = Assert.IsType<object?[]>(list[1]);
        Assert.Equal(new BigInteger(2), tuple[0]);
        Assert.Null(tuple[1]);

        var dictionary = Assert.IsType<Dictionary<object, object?>>(list[2]);
        Assert.Equal("lython", dictionary["name"]);
        Assert.Equal(true, dictionary["ready"]);
    }

    [Fact]
    public void PublicRuntimeFailuresPreserveSourceAndPythonFrames()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
def outer():
    inner()

def inner():
    raise ValueError("boom")

outer()
""",
            new PublicTestHost(),
            new LythonRunOptions { SourcePath = "/agent/task.py" });

        Assert.False(result.Success);
        var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
        Assert.Equal("ValueError", failure.ExceptionType);
        Assert.Equal("/agent/task.py", failure.SourcePath);
        Assert.Collection(
            failure.StackTrace,
            frame =>
            {
                Assert.Equal("outer", frame.FunctionName);
                Assert.Equal("/agent/task.py", frame.SourcePath);
            },
            frame =>
            {
                Assert.Equal("inner", frame.FunctionName);
                Assert.Equal("/agent/task.py", frame.SourcePath);
            });
    }

    [Fact]
    public async Task PublicAsyncOverloadsMergeExternalCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return 42");

        var result = await script.RunAsync(
            new PublicTestHost(),
            new LythonRunOptions { },
            cancellation.Token);

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultHostFileCapabilityFailsExplicitly()
    {
        ILythonHost host = new NoFileHost();

        var exception = await Assert.ThrowsAsync<LythonHostCapabilityUnavailableException>(
            () => host.OpenFileAsync(new("/payload.bin",host.Cwd),new(LythonFileAccess.Read),new LythonMemoryBudget(),CancellationToken.None).AsTask());

        Assert.Equal("owned byte-file I/O", exception.Capability);
        Assert.Contains("owned byte-file I/O is not available", exception.Message, StringComparison.Ordinal);
    }

    private sealed class NoFileHost : ILythonHost
    {
        public string Cwd => "/";
        public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask MkDirAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask RemoveAsync(string path, CancellationToken token) => throw new NotSupportedException();
        public ValueTask CopyAsync(string source, string target, CancellationToken token) => throw new NotSupportedException();
        public ValueTask MoveAsync(string source, string target, CancellationToken token) => throw new NotSupportedException();
        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class PublicTestHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private static readonly DateTimeOffset Timestamp = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        private readonly Lokad.Lython.Tests.Harness.MockLythonHost _files = new();
        private readonly BufferOutput _standardOutput = new();
        public string Cwd => "/";
        public bool CompletesSynchronously => true;
        public DateTimeOffset LocalNow => Timestamp;
        public DateTimeOffset UtcNow => Timestamp;
        public ILythonTextOutput StandardOutput => _standardOutput;
        public string StandardOutputText => _standardOutput.Text;
        public void SeedText(string path, string text) => _files.SeedFile(path, text);
        public string ReadText(string path) => _files.ReadText(path);
        public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token) => _files.OpenFileAsync(path, options, budget, token);
        public ValueTask<bool> ExistsAsync(string p, CancellationToken t) => _files.ExistsAsync(p, t);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string p, CancellationToken t) => _files.ListDirAsync(p, t);
        public ValueTask MkDirAsync(string p, CancellationToken t) => _files.MkDirAsync(p, t);
        public ValueTask RemoveAsync(string p, CancellationToken t) => _files.RemoveAsync(p, t);
        public ValueTask CopyAsync(string p, string q, CancellationToken t) => _files.CopyAsync(p, q, t);
        public ValueTask MoveAsync(string p, string q, CancellationToken t) => _files.MoveAsync(p, q, t);
        public async ValueTask<LythonPathStat> StatAsync(string p, CancellationToken t)
        {
            var stat = await _files.StatAsync(p, t);
            return new LythonPathStat(stat.Kind, stat.Size, stat.Exists ? Timestamp : null);
        }
    }

    private sealed class BufferOutput : ILythonTextOutput, ILythonSynchronousHostCapability
    {
        private readonly StringBuilder _text = new();

        public string Text => _text.ToString();

        public bool CompletesSynchronously => true;

        public ValueTask WriteUtf8Async(
            ReadOnlyMemory<byte> utf8,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text.Append(Encoding.UTF8.GetString(utf8.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
