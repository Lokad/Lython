using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RenderingStackAdmissionTests
{
    [Theory]
    [InlineData("list", false)] [InlineData("list", true)]
    [InlineData("tuple", false)] [InlineData("tuple", true)]
    [InlineData("dict", false)] [InlineData("dict", true)]
    public async Task DeepReprFailsSafelyAndTheNextExecutionRecovers(string kind, bool afterIo)
    {
        var next = kind switch { "list" => "[value]", "tuple" => "(value,)", _ => "{'child':value}" };
        var source = (afterIo ? "with open('/input.txt') as stream:stream.read()\n" : "")
            + "value=[]\nfor index in range(depth):value=" + next + "\nreturn repr(value)";
        var expected = kind switch { "list" => "[[[]]]", "tuple" => "(([],),)", _ => "{'child': {'child': []}}" };
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (afterIo) { if (delayed is not null) delayed.SeedFile("/input.txt", "ready"); else mock!.SeedFile("/input.txt", "ready"); }
            foreach (var depth in new[] { 1024, 2 })
            {
                var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["depth"] = depth } };
                using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(mock!, options))
                {
                    if (depth == 1024)
                    {
                        Assert.False(result.Success);
                        Assert.Equal(LythonFailureReason.InterpreterStackLimit, result.FailureAttribution?.Reason);
                        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
                    }
                    else { Assert.True(result.Success, result.Failure?.Message); Assert.Equal(expected, result.ReturnValue); }
                    Assert.Equal("", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            if (afterIo && delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        script.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }
}
