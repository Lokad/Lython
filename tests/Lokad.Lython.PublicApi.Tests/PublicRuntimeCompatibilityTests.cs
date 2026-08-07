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
        var result = new LythonEngine().Run(
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
        var script = new LythonEngine().Compile(
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

        var result = new LythonEngine().Run(
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
        var compiled = new LythonEngine().Compile("if True print('missing colon')");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic =>
            diagnostic.Severity == LythonDiagnosticSeverity.Error);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LythonEngine().Run(
                "return 1",
                new PublicTestHost(),
                new LythonRunOptions { MaxExecutionSteps = -1 }));
    }

    [Fact]
    public void PublicBoundaryProjectsNestedPythonValuesToClrShapes()
    {
        var result = new LythonEngine().Run(
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
        var result = new LythonEngine().Run(
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
        var script = new LythonEngine().Compile("return 42");

        var result = await script.RunAsync(
            new PublicTestHost(),
            new LythonRunOptions { MaxExecutionSteps = 10 },
            cancellation.Token);

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultHostBinaryCapabilityFailsExplicitly()
    {
        ILythonHost host = new PublicTestHost();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => host.ReadBytesAsync("/payload.bin", CancellationToken.None).AsTask());

        Assert.Contains("binary file I/O is not available", exception.Message, StringComparison.Ordinal);
    }

    private sealed class PublicTestHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private static readonly DateTimeOffset Timestamp = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
        private readonly BufferOutput _standardOutput = new();

        public string Cwd => "/";

        public bool CompletesSynchronously => true;

        public DateTimeOffset LocalNow => Timestamp;

        public DateTimeOffset UtcNow => Timestamp;

        public ILythonTextOutput StandardOutput => _standardOutput;

        public string StandardOutputText => _standardOutput.Text;

        public void SeedText(string path, string text) => _files[path] = Encoding.UTF8.GetBytes(text);

        public string ReadText(string path) => Encoding.UTF8.GetString(_files[path]);

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_files[path]);
        }

        public ValueTask WriteTextUtf8Async(
            string path,
            ReadOnlyMemory<byte> utf8,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files[path] = utf8.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendTextUtf8Async(
            string path,
            ReadOnlyMemory<byte> utf8,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefix = _files.TryGetValue(path, out var existing) ? existing : [];
            var appended = new byte[prefix.Length + utf8.Length];
            prefix.CopyTo(appended, 0);
            utf8.CopyTo(appended.AsMemory(prefix.Length));
            _files[path] = appended;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(path == "/" || _files.ContainsKey(path));
        }

        public ValueTask<IReadOnlyList<string>> ListDirAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (path != "/")
            {
                throw new DirectoryNotFoundException(path);
            }

            IReadOnlyList<string> names = _files.Keys
                .Select(static name => name[1..])
                .Order(StringComparer.Ordinal)
                .ToArray();
            return ValueTask.FromResult(names);
        }

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException($"Public test host cannot create directory {path}.");

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Remove(path);
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyAsync(
            string source,
            string destination,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Add(destination, _files[source].ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask MoveAsync(
            string source,
            string destination,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Add(destination, _files[source]);
            _files.Remove(source);
            return ValueTask.CompletedTask;
        }

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stat = path == "/"
                ? new LythonPathStat(LythonPathKind.Directory, 0, Timestamp)
                : _files.TryGetValue(path, out var bytes)
                    ? new LythonPathStat(LythonPathKind.File, bytes.Length, Timestamp)
                    : new LythonPathStat(LythonPathKind.Missing, 0, null);
            return ValueTask.FromResult(stat);
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
