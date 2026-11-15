using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormatTemplateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReusedMethodsKeepDeferredErrorsAndFreshProtocolValues(bool mapping)
    {
        const string source = """
            events=[]
            class Field:
             def __getitem__(self,key):
              events.append('get:'+key)
              return self
             def __format__(self,spec):
              with open('value') as stream:
               events.append('format:'+spec)
               return stream.read()
            cases=[('{0:>4}|{',['format:>4']),
                   ('{0[a]!z}',['get:a']),
                   ('{0[a]:{width:{width}}}',['get:a']),
                   ('{0[missing}',[]),
                   ('{0[é]🐍}',['get:é'])]
            for template,expected in cases:
             method=template.format
             for repeat in range(2):
              events.clear()
              try:method(Field(),width=4)
              except ValueError:pass
              else:raise AssertionError('deferred error missing')
              assert events==expected,(events,expected)
            events.clear()
            method='{0:9999999999999999999999999999d}'.format
            assert method(Field())=='ok'
            assert method(Field())=='ok'
            assert len(events)==2
            return 'ok'
            """;
        var actual = mapping ? source.Replace("{0", "{v")
            .Replace("method=template.format", "method=template.format_map")
            .Replace("d}'.format", "d}'.format_map")
            .Replace("method(Field(),width=4)", "method({'v':Field(),'width':4})")
            .Replace("method(Field())", "method({'v':Field()})") : source;
        await AssertReusable(actual);
    }

    [Fact]
    public async Task ReusedNativeSpecsRemainTypeIndependentAndResolveNestedWidthsEachTime()
    {
        await AssertReusable("""
            from datetime import date
            class Field:
             def __format__(self,spec):
              with open('value') as stream:return spec+':'+stream.read()
            method='[{0:>8}]'.format
            values=[7,'é',Field(),3.5,complex(1,2),date(2024,1,2)]
            expected=['[       7]','[       é]','[>8:ok]','[     3.5]','[  (1+2j)]','[>8]']
            for repeat in range(2):
             for value,text in zip(values,expected):assert method(value)==text
            nested='[{0:>{width}}]'.format
            assert nested('é',width=2)=='[ é]'
            assert nested('é',width=4)=='[   é]'
            assert nested(Field(),width=3)=='[>3:ok]'
            return 'ok'
            """);
    }

    internal static async Task AssertReusable(string source, bool requireAsyncIo = true)
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
                    Assert.True(budget.ReservedBytes > baseline);
                    if (asynchronous && requireAsyncIo) Assert.True(delayed.CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
