using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecoratorEvaluationOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoriesPrecedeDefaultsBasesAndBodies(bool delayed)
    {
        const string script = """
            events = []
            def mark(n):
                events.append(n)
                with open('/value') as f:
                    f.read()
                return n
            def deco(n):
                mark(n)
                def apply(value):
                    mark(n + 10)
                    return value
                return apply
            @deco(1)
            @deco(2)
            def f(x=mark(3)):
                return x
            print(events, f())
            events.clear()
            class Base:
                pass
            def base():
                mark(3)
                return Base
            @deco(1)
            @deco(2)
            class C(base()):
                mark(4)
            print(events)
            """;
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(script);
        Assert.True(compiled.IsValid);
        var host = new MockLythonHost();
        host.SeedFile("/value", "v");
        var delayedHost = new DelayedLythonHost();
        delayedHost.SeedFile("/value", "v");
        var result = delayed ? await compiled.RunAsync(delayedHost) : compiled.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[1, 2, 3, 12, 11] 3\n[1, 2, 3, 4, 12, 11]\n", result.StandardOutput);
        if (delayed) Assert.True(delayedHost.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("@register\ndef answer():\n    return 42")]
    [InlineData("def answer() -> register():\n    return 42")]
    [InlineData("def answer(value: register()):\n    return 42")]
    public async Task DefinitionCallbacksCanMutateCollectionsBeforeLaterReads(string definition)
    {
        const string source = """
            events = []
            labels = {}
            def register(func=None):
                events.append('registered')
                labels['answer'] = 42
                return int if func is None else func
            DEFINITION
            print(events[0], labels['answer'])
            """;
        using var script = new LythonEngine().Compile(source.Replace("DEFINITION", definition));
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        using var sync = script.Run(new MockLythonHost());
        using var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("registered 42\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, asyncResult.StandardOutput);
    }

    [Fact]
    public void FailedDecoratorExpressionPreventsDefinitionEffects()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run("""
            def fail():
                print('factory')
                raise ValueError('stop')
            @fail()
            class C:
                print('body')
            """, new MockLythonHost());
        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Equal("factory\n", result.StandardOutput);
    }
}
