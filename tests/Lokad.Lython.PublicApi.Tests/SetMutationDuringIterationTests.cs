using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Structural mutation of a set during iteration raises a Python error
// instead of leaking a CLR collection-modified exception (add path) or
// silently iterating stale storage (remove/discard rebuild paths). The
// version guard lives on the set like PyDict, so every structural change
// trips it in both execution modes; no-op mutations stay silent like CPython.
public sealed class SetMutationDuringIterationTests
{
    private const string ExpectedMessage = "Set changed size during iteration";

    [Fact]
    public async Task AddDuringIteration_Raises()
    {
        const string code = """
            s = set(range(9))
            for x in s:
                s.add(100)
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);

        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Equal(ExpectedMessage, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(ExpectedMessage, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task RemoveDuringIteration_Raises()
    {
        const string code = """
            s = set(range(9))
            for x in s:
                s.remove(x)
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);

        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Equal(ExpectedMessage, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(ExpectedMessage, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task DiscardDuringIteration_Raises()
    {
        const string code = """
            s = set(range(9))
            it = iter(s)
            first = next(it)
            s.discard(first)
            second = next(it)
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);

        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Equal(ExpectedMessage, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(ExpectedMessage, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task ClearAndPopAndUpdateDuringIteration_Raise()
    {
        var cases = new[]
        {
            "s = set(range(5))\nfor x in s:\n    s.clear()",
            "s = set(range(5))\nfor x in s:\n    s.pop()",
            "s = set(range(5))\nfor x in s:\n    s.update([100])",
        };
        foreach (var code in cases)
        {
            var script = new LythonEngine().Compile(code);
            Assert.True(script.IsValid, code);

            var sync = script.Run(new MockLythonHost());
            Assert.False(sync.Success, code);
            Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
            Assert.Equal(ExpectedMessage, sync.Failure?.Message);

            var asyncResult = await script.RunAsync(new MockLythonHost());
            Assert.False(asyncResult.Success, code);
            Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
            Assert.Equal(ExpectedMessage, asyncResult.Failure?.Message);
        }
    }

    [Fact]
    public async Task NoOpMutations_DoNotRaise()
    {
        const string code = """
            s = set(range(5))
            seen = []
            for x in s:
                seen.append(x)
                s.add(1)
                s.discard(999)
            return [len(seen), len(s)]
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);

        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new System.Numerics.BigInteger(5), new System.Numerics.BigInteger(5) }, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new System.Numerics.BigInteger(5), new System.Numerics.BigInteger(5) }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task IterationMutations_MatchCpython()
    {
        // One CPython process covers every failure scenario so the differential
        // costs a single interpreter launch; each scenario reports its own
        // outcome line, and Lython must print byte-identical lines in both modes.
        const string driver = """
            def check(fn, name):
                try:
                    fn()
                    print(name + ":completed")
                except RuntimeError as e:
                    print(name + ":RuntimeError:" + str(e))

            def scenario_add():
                s = set(range(9))
                for x in s:
                    s.add(100)

            def scenario_discard():
                s = set(range(9))
                for x in s:
                    s.discard(x)

            check(scenario_add, "add")
            check(scenario_discard, "discard")
            """;

        // Same fail-loud policy as the other differential pins: a missing
        // interpreter fails instead of skipping silently.
        var (python, version) = ResolveCpython();

        var driverPath = Path.Combine(Path.GetTempPath(), "lython-set-" + Guid.NewGuid().ToString("N") + ".py");
        await File.WriteAllTextAsync(driverPath, driver);
        try
        {
            var run = SubprocessProbeRunner.Run(python, [driverPath], timeout: TimeSpan.FromMinutes(2));
            Assert.True(run.ExitCode == 0, "CPython " + version + " exited with " + run.ExitCode + ": " + run.StandardError);
            var expected = run.StandardOutput.Replace("\r", string.Empty);

            var script = new LythonEngine().Compile(driver);
            Assert.True(script.IsValid);

            var sync = script.Run(new MockLythonHost());
            Assert.True(sync.Success, sync.Failure?.Message);
            Assert.Equal(expected, sync.StandardOutput.Replace("\r", string.Empty));

            var asyncResult = await script.RunAsync(new MockLythonHost());
            Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
            Assert.Equal(expected, asyncResult.StandardOutput.Replace("\r", string.Empty));
        }
        finally
        {
            File.Delete(driverPath);
        }
    }

    private static (string Python, string Version) ResolveCpython()
    {
        var configured = Environment.GetEnvironmentVariable("LYTHON_DIFFTEST_PYTHON");
        var candidates = configured is null
            ? new[] { "python", "python3" }
            : new[] { configured };
        foreach (var candidate in candidates)
        {
            try
            {
                var run = SubprocessProbeRunner.Run(candidate, ["--version"], timeout: TimeSpan.FromSeconds(15));
                if (run.ExitCode == 0)
                {
                    return (candidate, (run.StandardOutput + run.StandardError).Trim());
                }
            }
            catch (Exception)
            {
            }
        }

        throw new InvalidOperationException(
            "No CPython interpreter found (tried: " + string.Join(", ", candidates) +
            "). Set LYTHON_DIFFTEST_PYTHON to a Python executable to run this differential test.");
    }
}

