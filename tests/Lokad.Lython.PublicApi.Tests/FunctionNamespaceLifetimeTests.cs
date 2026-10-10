using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionNamespaceLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecursiveClosuresRetainSeparateMutableNamespaces(bool asynchronous)
    {
        const string source = """
def make(n):
    value = n
    def read(delta=0):
        nonlocal value
        value += delta
        return value
    if n:
        child = make(n - 1)
        return read, child[0]
    return read, read
first, second = make(2)
print(first(3), second(7), first(), second())
""";
        var host = new MockLythonHost();
        var engine = new LythonEngine();
        var result = asynchronous ? await engine.RunAsync(source, host) : engine.Run(source, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("5 8 5 8\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedClassCellsAndLocalShadowingPreserveImplicitSuper(bool asynchronous)
    {
        const string source = """
class Base:
    def identify(self):
        return 'base'
class Derived(Base):
    def identify(self):
        def name():
            return __class__.__name__
        return super().identify() + ':' + name()
    def shadow(self, __class__):
        try:
            return super().identify()
        except RuntimeError:
            return 'shadowed'
value = Derived()
print(value.identify())
print(value.shadow(3))
""";
        var host = new MockLythonHost();
        var engine = new LythonEngine();
        var result = asynchronous ? await engine.RunAsync(source, host) : engine.Run(source, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("base:Derived\nshadowed\n", result.StandardOutput);
    }
}
