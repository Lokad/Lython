using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BulkReplacementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnchangedReplacementsKeepABoundedWorkingSetAndRetainedResults(bool binary)
    {
        var text = binary ? "b'a'" : "'a'";
        var old = binary ? "b'not-present'" : "'not-present'";
        var replacement = binary ? "b'b'" : "'b'";
        var source = $"value={text}*4096\noriginal=value\nfor _ in range(1000):\n value=value.replace({old},{replacement})\nreturn value" +
            (binary ? ".decode('ascii')" : "");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var expected = new string('a', 4096);
            foreach (var asynchronous in new[] { false, true })
            {
                using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(retained.Success, retained.Failure?.Message);
                    Assert.Equal(expected, Assert.IsType<string>(retained.ReturnValue));
                    var held = budget.ReservedBytes;
                    Assert.True(held > baseline);
                    using (var next = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                    {
                        Assert.True(next.Success, next.Failure?.Message);
                        Assert.Equal(expected, Assert.IsType<string>(next.ReturnValue));
                    }
                    Assert.Equal(held, budget.ReservedBytes);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
