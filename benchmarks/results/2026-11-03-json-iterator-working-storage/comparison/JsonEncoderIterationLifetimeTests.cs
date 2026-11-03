using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonEncoderIterationLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReentrantDefaultCannotAdvanceTheActiveIterator(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            import json
            seen = []
            def fallback(value):
                with open('/value') as source:
                    text = source.read()
                try:
                    next(chunks)
                except ValueError as error:
                    seen.append(str(error))
                return text
            chunks = json.JSONEncoder(default=fallback).iterencode([{1}, 2])
            parts = list(chunks)
            assert ''.join(parts) == '["v", 2]', repr((parts, seen))
            assert seen == ['generator already executing'], repr((parts, seen))
            return ''.join(parts)
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            host.SeedFile("/value", "v");
            delayed.SeedFile("/value", "v");
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("[\"v\", 2]", result.ReturnValue);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlierChunksRemainIndependentAcrossLargeAndSmallValues(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            import json
            values = ['a' * 4097, 'b', '\u03b1' * 4097]
            chunks = json.JSONEncoder(ensure_ascii=False).iterencode(values)
            parts = list(chunks)
            assert ''.join(parts) == '["' + values[0] + '", "b", "' + values[2] + '"]'
            assert json.loads(''.join(parts)) == values
            assert next(chunks, 'finished') == 'finished'
            return 'ok'
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok", result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
