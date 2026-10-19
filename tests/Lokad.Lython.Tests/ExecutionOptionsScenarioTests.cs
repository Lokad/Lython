using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutionOptionsScenarioTests
{
    [Fact]
    public void CancelledRun_FailsDeterministically()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
value = "hello"
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                CancellationToken = cts.Token
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveCancellation_DuringPureInterpreterLoop_FailsDeterministically()
    {
        using var cts = new CancellationTokenSource();

        var runTask = Task.Run(() => new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
while True:
    pass
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                CancellationToken = cts.Token
            }));

        await Task.Delay(25);
        cts.Cancel();

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_UsesCancellationTokenAndPreservesResultContract()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(
            """
value = "hello"
""",
            new MockLythonHost(),
            cancellationToken: cts.Token);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SucceedsForNormalExecution()
    {
        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(
            """
value = 40 + 2
return_value = value
""",
            new MockLythonHost(),
            new LythonRunOptions());

        Assert.True(result.Success);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task RunAsync_ActiveCancellation_FailsDeterministically()
    {
        using var cts = new CancellationTokenSource();

        var runTask = new LythonEngine(Utf8RegexProvider.Instance).RunAsync(
            """
while True:
    pass
""",
            new MockLythonHost(),
            new LythonRunOptions(),
            cts.Token);

        await Task.Delay(25);
        cts.Cancel();

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_HostCancellationToken_CancelsAsyncHostOperation()
    {
        using var cts = new CancellationTokenSource();

        var runTask = new LythonEngine(Utf8RegexProvider.Instance).RunAsync(
            """
value = open("/input.txt").read()
""",
            new BlockingReadHost(),
            cancellationToken: cts.Token);

        await Task.Delay(25);
        cts.Cancel();

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("execution canceled", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecursionLimit_IsEnforced()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
def loop():
    loop()

loop()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxRecursionDepth = 5
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RecursionError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("maximum recursion depth exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("recursion")]
    public void NegativeLimits_AreRejectedExplicitly(string limit)
    {
        var options = limit switch
        {
            "recursion" => new LythonRunOptions { MaxRecursionDepth = -1 },
            _ => throw new InvalidOperationException($"unknown test limit: {limit}")
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LythonEngine(Utf8RegexProvider.Instance).Run("return 1", new MockLythonHost(), options));
    }

    [Fact]
    public void HostGlobals_RejectArbitraryClrObjectsWithoutInvokingCallbacks()
    {
        var value = new CallbackGlobalValue();

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "text = str(value)",
            new MockLythonHost(),
            new LythonRunOptions
            {
                Globals = new Dictionary<string, object?> { ["value"] = value }
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("initial global values", result.Failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, value.ToStringCallCount);
        Assert.Equal(0, value.GetEnumeratorCallCount);
    }

    [Fact]
    public void HostGlobals_RejectReferenceCycles()
    {
        var cycle = new List<object?>();
        cycle.Add(cycle);

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "value = payload",
            new MockLythonHost(),
            new LythonRunOptions
            {
                Globals = new Dictionary<string, object?> { ["payload"] = cycle }
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("reference cycles", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_AwaitsAsynchronousHostReadsWithoutThreadPoolWrapper()
    {
        var host = new DelayedReadHost();
        host.SeedFile("/input.txt", "hello");

        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(
            """
return open("/input.txt").read()
""",
            host,
            new LythonRunOptions());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously);
    }

    [Fact]
    public async Task RunAsync_AwaitsAsynchronousHostReadsInsideBoundMethods()
    {
        var host = new DelayedReadHost();
        host.SeedFile("/input.txt", "hello");

        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(
            """
class Reader:
    def read(self):
        return open("/input.txt").read()

reader = Reader()
return reader.read()
""",
            host,
            new LythonRunOptions());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously);
    }

    [Fact]
    public void Run_WithAsynchronousHostRead_FailsFastWithRunAsyncGuidance()
    {
        var host = new DelayedReadHost();
        host.SeedFile("/input.txt", "hello");

        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
return open("/input.txt").read()
""",
            host,
            new LythonRunOptions());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("use RunAsync", result.Failure.Message, StringComparison.Ordinal);
        Assert.False(host.CompletedAsynchronously);
    }

    [Fact]
    public void ExecutionMemoryBudget_RangeSliceDoesNotMaterializeAList()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
values = range(1000000000)
tail = values[1:]
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MemoryBudget = new LythonMemoryBudget(65536)
            });

        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void SuppliedResultOwnerRejectsUnfundedProjection()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
return ["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccccccccccccc"]
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MemoryBudget = new LythonMemoryBudget(65536),
                ResultMemoryBudget = new LythonMemoryBudget(128)
            });

        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        Assert.Null(result.ReturnValue);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void ProjectionBudget_CountsAstralResultsAtUtf16Width()
    {
        // 100 astral runes project to 200 UTF-16 code units (432 bytes with
        // the object header); the old rune-based estimate charged only 232.
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
return chr(128512) * 100
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                ResultMemoryBudget = new LythonMemoryBudget(300)
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ProjectionError", result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains("shared memory budget exceeded during projection", result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(700, false)]
    public void DeeplyNestedGlobalRendering_IsStackSafe(int depth, bool succeeds)
    {
        var nested = CreateDeeplyNestedList(depth);

        using var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            """
text = str(data)
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                Globals = new Dictionary<string, object?>
                {
                    ["data"] = nested
                }
            });

        Assert.Equal(succeeds, result.Success);
        if (succeeds) Assert.Null(result.Failure);
        else
        {
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.InterpreterStackLimit, result.FailureAttribution?.Reason);
            Assert.Equal("maximum interpreter stack depth exceeded", result.Failure?.Message);
        }
    }

    [Fact]
    public void RecursionCounterDoesNotWrapAtIntMaxValue()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null,regexProvider:Utf8RegexProvider.Instance);
        context.Limits.CurrentRecursionDepth = int.MaxValue;
        var failure = Assert.Throws<LythonRuntimeException>(() => context.EnterFunctionCall(null));
        Assert.Equal("RecursionError", failure.ExceptionType);
        Assert.Contains("maximum recursion depth exceeded", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InterpreterCounterDoesNotWrapAtIntMaxValue()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null,regexProvider:Utf8RegexProvider.Instance);
        context.Limits.CurrentInterpreterDepth = int.MaxValue;
        var failure = Assert.Throws<LythonRuntimeException>(() => context.EnterInterpreterFrame(null));
        Assert.Equal("RuntimeError", failure.ExceptionType);
        Assert.Contains("maximum interpreter stack depth exceeded", failure.Message, StringComparison.Ordinal);
    }

    private static List<object?> CreateDeeplyNestedList(int depth)
    {
        object? current = "leaf";
        for (var i = 0; i < depth; i++)
        {
            current = new List<object?> { current };
        }

        return (List<object?>)current;
    }

    [Fact]
    public void LinkedCancellation_PreservesEveryOption()
    {
        using var cancellation = new CancellationTokenSource();
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["greeting"] = "hello" },
            Args = new[] { "first", "second" },
            Environment = new Dictionary<string, string> { ["MODE"] = "test" },
            SourcePath = "/entry.py",
            CancellationToken = cancellation.Token,
            DisableLocalModuleImports = true,
            AllowedLocalModules = new HashSet<string> { "helper" },
            AllowedBuiltinModules = new HashSet<string> { "math" },
            MaxRecursionDepth = 12,
            MemoryBudget = new LythonMemoryBudget(100),
            ResultMemoryBudget = new LythonMemoryBudget(200),
        };
        var properties = typeof(LythonRunOptions).GetProperties();
        foreach (var property in properties)
        {
            var configured = property.GetValue(options);
            var blank = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
            Assert.False(Equals(configured, blank), $"option {property.Name} must be non-default in this fixture.");
        }

        var merged = options.WithCancellation(CancellationToken.None);
        Assert.Equal(CancellationToken.None, merged.CancellationToken);
        foreach (var property in properties)
        {
            if (property.Name == nameof(LythonRunOptions.CancellationToken))
            {
                continue;
            }

            var expected = property.GetValue(options);
            var actual = property.GetValue(merged);
            if (property.PropertyType.IsInterface)
            {
                Assert.Same(expected, actual);
            }
            else
            {
                Assert.Equal(expected, actual);
            }
        }
    }

    private sealed class CallbackGlobalValue : IEnumerable<object>
    {
        public int ToStringCallCount { get; private set; }

        public int GetEnumeratorCallCount { get; private set; }

        public override string ToString()
        {
            ToStringCallCount++;
            return Environment.MachineName;
        }

        public IEnumerator<object> GetEnumerator()
        {
            GetEnumeratorCallCount++;
            return Array.Empty<object>().AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class BlockingReadHost : ILythonHost
    {
        private readonly MockLythonHost _inner = new();

        public string Cwd => _inner.Cwd;

        public DateTimeOffset LocalNow => _inner.LocalNow;

        public DateTimeOffset UtcNow => _inner.UtcNow;

        public ILythonTextInput? StandardInput => _inner.StandardInput;

        public ILythonTextOutput? StandardOutput => _inner.StandardOutput;

        public ILythonTextOutput? StandardError => _inner.StandardError;

        public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;

        public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ReadOnlyMemory<byte>.Empty;
        }

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

    private sealed class DelayedReadHost : ILythonHost
    {
        private readonly MockLythonHost _inner = new();

        public bool CompletedAsynchronously { get; private set; }

        public string Cwd => _inner.Cwd;

        public DateTimeOffset LocalNow => _inner.LocalNow;

        public DateTimeOffset UtcNow => _inner.UtcNow;

        public ILythonTextInput? StandardInput => _inner.StandardInput;

        public ILythonTextOutput? StandardOutput => _inner.StandardOutput;

        public ILythonTextOutput? StandardError => _inner.StandardError;

        public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;

        public void SeedFile(string path, string text) => _inner.SeedFile(path, text);

        public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
        {
            await Task.Delay(25, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            CompletedAsynchronously = true;
            return await _inner.ReadTextUtf8Async(path, cancellationToken);
        }

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
