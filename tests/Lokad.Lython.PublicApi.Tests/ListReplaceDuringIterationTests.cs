using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// List iteration follows the live list by index: same-length replacement
// during enumeration must iterate (not throw a CLR collection-modified
// error), appends are visited when reached even across the small-list
// storage promotion, and removals shift like CPython. A small differential
// driver pins stdout parity with CPython in both execution modes.
public sealed class ListReplaceDuringIterationTests
{
    private const string Driver = """
        lines8 = ["v" + str(i) for i in range(8)]
        for i, line in enumerate(lines8):
            lines8[i] = line.upper()
        print(",".join(lines8))
        lines9 = ["v" + str(i) for i in range(9)]
        for i, line in enumerate(lines9):
            lines9[i] = line.upper()
        print(",".join(lines9))
        lines = ("alpha\n" * 9).splitlines()
        for i, line in enumerate(lines):
            lines[i] = line.upper()
        print("|".join(lines))
        grown = ["s" + str(i) for i in range(8)]
        for line in grown:
            if len(grown) < 12:
                grown.append(line + "!")
        print(",".join(grown))
        items = [1, 2, 3, 4]
        seen = []
        for x in items:
            seen.append(x)
            items.remove(x)
        print(",".join([str(v) for v in seen]) + "/" + ",".join([str(v) for v in items]))
        """;

    [Fact]
    public async Task ReplaceNineViaEnumerate_UppercasesJoinedValues()
    {
        const string code = """
            lines = ('alpha\n' * 9).splitlines()
            for i, line in enumerate(lines):
                lines[i] = line.upper()
            return '|'.join(lines)
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var expected = string.Join("|", Enumerable.Repeat("ALPHA", 9));

        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<string>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<string>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task IterationMutations_MatchCpython()
    {
        // The repo compares against CPython as a matter of course; a
        // differential pin without it would prove nothing, so a missing
        // interpreter fails loudly instead of skipping silently.
        var (python, version) = ResolveCpython();

        var driverPath = Path.Combine(Path.GetTempPath(), "lython-iter-" + Guid.NewGuid().ToString("N") + ".py");
        await File.WriteAllTextAsync(driverPath, Driver);
        try
        {
            var run = SubprocessProbeRunner.Run(python, [driverPath], timeout: TimeSpan.FromMinutes(2));
            Assert.True(run.ExitCode == 0, "CPython " + version + " exited with " + run.ExitCode + ": " + run.StandardError);
            var expected = run.StandardOutput.Replace("\r", string.Empty);

            var script = new LythonEngine().Compile(Driver);
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

    // Reports the actual executable and version: differential failures name
    // the interpreter they ran against instead of a bare candidate string.
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
