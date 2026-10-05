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
        var compiled = new LythonEngine().Compile(script);
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

    [Fact]
    public void FailedDecoratorExpressionPreventsDefinitionEffects()
    {
        var result = new LythonEngine().Run("""
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
