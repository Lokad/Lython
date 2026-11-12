using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorNamespaceMutationTests
{
    // Independent CPython 3.13 outputs. A lowered class updates the enclosing
    // namespace while its generator's executable slots are suspended.
    [Theory]
    [InlineData(false, "new\n")]
    [InlineData(true, "unbound\n")]
    public async Task SuspendedGeneratorObservesClassNonlocalMutation(bool delete, string expected)
    {
        var source = """
            def outer():
                value='old'
                def change():
                    class C:
                        nonlocal value
                        value='new'
                yield change
                try:print(value)
                except UnboundLocalError:print('unbound')
            generator=outer()
            next(generator)()
            try:next(generator)
            except StopIteration:pass
            """;
        if (delete) source = source.Replace("value='new'", "del value");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
