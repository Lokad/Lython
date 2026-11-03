using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ConstantFormatLayoutTests
{
    [Fact]
    public async Task ReusedLiteralSpecsKeepValueSemanticsAndSuspendingProtocols()
    {
        await AssertReusable("""
            from datetime import date
            class Own:
                def __format__(self, spec):
                    with open('value') as source:
                        return spec + ':' + source.read()
            def render(value):
                return f'[{value:>8}]'
            def single(value):
                return f'{value:>8}'
            values = [7, 'é', Own(), 3.5, complex(1, 2), date(2024, 1, 2)]
            expected = ['[       7]', '[       é]', '[>8:ok]', '[     3.5]', '[  (1+2j)]', '[>8]']
            for value, text in zip(values, expected):
                assert render(value) == text
                assert single(value) == text[1:-1]
            assert f'{7!s:>8s}' == '       7'
            return 'ok'
            """);
    }

    [Fact]
    public async Task NativeGrammarErrorsStayDeferredUntilTheValueIsFormatted()
    {
        await AssertReusable("""
            class Own:
                def __format__(self, spec):
                    with open('value') as source:
                        return spec + ':' + source.read()
            def missing_precision(value):
                return f'{value:9.}'
            def huge_width(value):
                return f'[{value:99999999999999999999999999999999999}]'
            def string_only(value):
                return f'{value:>8s}'
            assert missing_precision(Own()) == '9.:ok'
            assert huge_width(Own()) == '[99999999999999999999999999999999999:ok]'
            assert string_only(Own()) == '>8s:ok'
            try:
                missing_precision('text')
            except ValueError as error:
                assert 'missing precision' in str(error)
            else:
                raise AssertionError('missing native precision was accepted')
            try:
                string_only(7)
            except ValueError:
                pass
            else:
                raise AssertionError('integer accepted string format code')
            assert string_only('é') == '       é'
            assert missing_precision(Own()) == '9.:ok'
            return 'ok'
            """);
    }

    private static async Task AssertReusable(string source)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            host.SeedFile("/value", "ok");
            delayed.SeedFile("/value", "ok");
            foreach (var asynchronous in new[] { false, true, false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("ok", result.ReturnValue);
                    if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
