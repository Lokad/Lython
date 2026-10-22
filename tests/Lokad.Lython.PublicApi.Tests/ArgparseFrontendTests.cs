using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ArgparseFrontendTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutableParserCompositionUsesRuntimeNamespaceFields(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import argparse
            def configure(parser):parser.add_argument('--configured')
            p=argparse.ArgumentParser(add_help=False)
            alias=p
            alias.add_argument('--aliased')
            configure(p)
            p.set_defaults(config={'value':1})
            n=p.parse_args(['--aliased','a','--configured','b'])
            print(n.aliased,n.configured,n.config['value'])
            n.dynamic=3
            print(n.dynamic)
            try:print(n.absent)
            except AttributeError:print('runtime missing')
            """);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("a b 1\n3\nruntime missing\n", result.StandardOutput);
    }
}
