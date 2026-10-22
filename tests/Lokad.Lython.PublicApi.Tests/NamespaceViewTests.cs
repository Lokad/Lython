using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NamespaceViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModuleAndClassLocalsAreLiveButFunctionLocalsAreSnapshots(bool asynchronous)
    {
        using var script=new LythonEngine().Compile("""
            x=1
            module=locals()
            x=2
            print(module is globals(),module is vars(),module['x'])
            module['dynamic']=7
            print(dynamic)
            del module['dynamic']
            print('dynamic' in globals())
            class C:
                saved=locals()
                x=2
                print(saved is locals(),saved['x'])
                saved['y']=3
                print(y)
                del saved['y']
                print('y' in locals())
            C.saved['x']=9
            print(C.x)
            def f():
                x=1
                saved=locals()
                x=2
                print(saved['x'],locals()['x'],saved is locals())
            f()
            """);
        using var result=asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("True True 2\n7\nFalse\nTrue 2\n3\nFalse\n2\n1 2 False\n",result.StandardOutput);
    }
}
