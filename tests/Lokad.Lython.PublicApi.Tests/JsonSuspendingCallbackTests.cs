using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonSuspendingCallbackTests
{
    private const string Definitions = """
        import json
        def read(value):
            with open('/value') as f:
                return f.read()
        class D(json.JSONDecoder):
            def __init__(self, **kwargs):
                read(None)
                super().__init__(**kwargs)
            def decode(self, s):
                read(None)
                return super().decode(s)
        class E(json.JSONEncoder):
            def __init__(self, **kwargs):
                read(None)
                super().__init__(**kwargs)
            def encode(self, o):
                read(None)
                return super().encode(o)
            def default(self, o):
                return read(o)
        """;

    [Theory]
    [InlineData("json.loads('7', parse_int=read)", "v")]
    [InlineData("json.loads('1.5', parse_float=read)", "v")]
    [InlineData("json.loads('NaN', parse_constant=read)", "v")]
    [InlineData("json.loads('{}', object_hook=read)", "v")]
    [InlineData("json.loads('{\"x\":1}', object_pairs_hook=read)", "v")]
    [InlineData("json.JSONDecoder(parse_int=read).decode('[7]')", "['v']")]
    [InlineData("json.JSONDecoder(parse_int=read).raw_decode('7 tail')", "('v', 1)")]
    [InlineData("json.loads('7', cls=D)", "7")]
    [InlineData("json.dumps({1}, default=read)", "\"v\"")]
    [InlineData("json.JSONEncoder(default=read).encode([{1}])", "[\"v\"]")]
    [InlineData("''.join(json.JSONEncoder(default=read).iterencode([{1}]))", "[\"v\"]")]
    [InlineData("json.dumps({1}, cls=E)", "\"v\"")]
    [InlineData("json.loads('{\"x\":[true,false,null]}', object_hook=read)", "v")]
    [InlineData("json.loads('{\"x\":[true,false,null]}', object_pairs_hook=read)", "v")]
    public async Task HooksAndSelectedClassesAwaitHostEffects(string expression, string expected)
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(Definitions + "\nreturn str(" + expression + ")");
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value", "v");
        var sync = compiled.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "v");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task FileBoundariesAwaitHooksAndPublication()
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(Definitions + """

            with open('/input') as f:
                value = json.load(f, parse_int=read)
            with open('/output', 'w') as f:
                json.dump({1}, f, default=read)
            return value
            """);
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "v");
        host.SeedFile("/input", "[7]");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "v" }, result.ReturnValue);
        Assert.Equal("\"v\"", host.ReadText("/output"));
    }

    [Theory]
    [InlineData("json.loads('7', parse_int=read)")]
    [InlineData("json.loads('1.5', parse_float=read)")]
    [InlineData("json.loads('NaN', parse_constant=read)")]
    [InlineData("''.join(json.JSONEncoder(default=read).iterencode([{1}]))")]
    [InlineData("json.loads('{\"x\":[true,false,null]}', object_hook=read)")]
    public async Task SuspendedCallbacksCancelAndCompiledScriptsRemainReusable(string expression)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(Definitions + "\nreturn str(" + expression + ")", budget);
        Assert.True(compiled.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "v");
        var started = host.PauseReadUntilCancellation("/value");
        using var cancellation = new CancellationTokenSource();
        var run = compiled.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var canceled = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(canceled.Success);
            Assert.Equal(LythonFailureReason.Cancelled, canceled.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/value", "v");
        using (var reused = await compiled.RunAsync(fresh)) Assert.True(reused.Success, reused.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("7", "parse_int", false)]
    [InlineData("7", "parse_int", true)]
    [InlineData("1.5", "parse_float", false)]
    [InlineData("1.5", "parse_float", true)]
    [InlineData("NaN", "parse_constant", false)]
    [InlineData("NaN", "parse_constant", true)]
    public async Task ScalarHookFailuresAfterSuspensionAwaitCleanupAndReturnAllowance(string literal, string hook, bool refuse)
    {
        var source = Definitions + $$"""

            def convert(value):
                with open('/cleanup', 'w') as target:
                    target.write('done')
                    target.flush()
                    read(value)
                    if mode == 'memory':
                        return 'x' * 4194304
                    if mode == 'error':
                        raise ValueError('scalar hook failed')
                    return value
            try:
                json.loads('{{literal}}', {{hook}}=convert)
            except MemoryError:
                print('suppressed')
            print('after')
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/value", "v");
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["mode"] = refuse ? "memory" : "error" } };
        using (var result = await compiled.RunAsync(host, options))
        {
            Assert.False(result.Success);
            Assert.Equal(refuse ? "MemoryError" : "ValueError", result.Failure?.ExceptionType);
            if (refuse) Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal("done", host.ReadText("/cleanup"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["mode"] = "ok" } };
        using (var reused = await compiled.RunAsync(host, options))
        {
            Assert.True(reused.Success, reused.Failure?.Message);
            Assert.Equal("after\n", reused.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task IncrementalHookFailuresExhaustOnlyTheirIterator()
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(Definitions + """

            def fail(o):
                read(o)
                raise ValueError('stop')
            e = json.JSONEncoder(default=fail)
            it = e.iterencode([1, {1}])
            print(next(it))
            try:
                list(it)
            except ValueError:
                print('caught')
            print(list(it), e.encode([2]))
            """);
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "v");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[\ncaught\n[] [2]\n", result.StandardOutput);
    }
}
