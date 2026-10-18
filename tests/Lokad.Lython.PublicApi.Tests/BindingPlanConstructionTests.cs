using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BindingPlanConstructionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return [false,"empty","def f():return []\nprint(f(),f.__defaults__,f.__kwdefaults__)","[] None None\n"];
        yield return [true,"empty","def f():return []\nprint(f(),f.__defaults__,f.__kwdefaults__)","[] None None\n"];
        yield return [false,"single","def f(x):return x\nprint(f(3),f(x=4))","3 4\n"];
        yield return [true,"single","def f(x):return x\nprint(f(3),f(x=4))","3 4\n"];
        yield return [false,"mixed-default-mutation","def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\nprint(f(1,d=4))\nprint(f(1,7,8,9,10,d=11,e=12,z=13))\nf.__defaults__=(20,30)\nf.__kwdefaults__['e']=50\nprint(f(1,d=40))\nprint(f(1,d=4,a=9,args=10))","[1, 2, 3, (), 4, 5, {}]\n[1, 7, 8, (9, 10), 11, 12, {'z': 13}]\n[1, 20, 30, (), 40, 50, {}]\n[1, 20, 30, (), 4, 50, {'a': 9, 'args': 10}]\n"];
        yield return [true,"mixed-default-mutation","def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\nprint(f(1,d=4))\nprint(f(1,7,8,9,10,d=11,e=12,z=13))\nf.__defaults__=(20,30)\nf.__kwdefaults__['e']=50\nprint(f(1,d=40))\nprint(f(1,d=4,a=9,args=10))","[1, 2, 3, (), 4, 5, {}]\n[1, 7, 8, (9, 10), 11, 12, {'z': 13}]\n[1, 20, 30, (), 40, 50, {}]\n[1, 20, 30, (), 4, 50, {'a': 9, 'args': 10}]\n"];
        yield return [false,"mixed-errors","def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\ntry:f()\nexcept TypeError as error:print(type(error).__name__)\ntry:f(1,2,3,**{'c':4,'d':5})\nexcept TypeError as error:print(type(error).__name__)","TypeError\nTypeError\n"];
        yield return [true,"mixed-errors","def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\ntry:f()\nexcept TypeError as error:print(type(error).__name__)\ntry:f(1,2,3,**{'c':4,'d':5})\nexcept TypeError as error:print(type(error).__name__)","TypeError\nTypeError\n"];
        yield return [false,"unicode-keyword","def f(caf\u00e9,*,\u03bb=2):return caf\u00e9+\u03bb\nprint(f(1),f(1,\u03bb=3))","3 4\n"];
        yield return [true,"unicode-keyword","def f(caf\u00e9,*,\u03bb=2):return caf\u00e9+\u03bb\nprint(f(1),f(1,\u03bb=3))","3 4\n"];
        yield return [false,"lambda","fn=lambda a=3,*,b=4:a+b\nprint(fn(),fn(5,b=6))","7 11\n"];
        yield return [true,"lambda","fn=lambda a=3,*,b=4:a+b\nprint(fn(),fn(5,b=6))","7 11\n"];
        yield return [false,"method","class C:\n def f(self,a=1,*,b=2):return a+b\nprint(C().f(),C().f(3,b=4))","3 7\n"];
        yield return [true,"method","class C:\n def f(self,a=1,*,b=2):return a+b\nprint(C().f(),C().f(3,b=4))","3 7\n"];
        yield return [false,"generator","def f(a=1,*,b=2):yield a+b\ng=f()\nf.__kwdefaults__['b']=9\nprint(list(g),list(f(3,b=4)))","[3] [7]\n"];
        yield return [true,"generator","def f(a=1,*,b=2):yield a+b\ng=f()\nf.__kwdefaults__['b']=9\nprint(list(g),list(f(3,b=4)))","[3] [7]\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ParameterShapesAndMutableDefaultsMatchPython(bool asynchronous, string name, string source, string expected)
        => await Check(asynchronous, name, source, expected);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingBindingErrorMessagesRemainStable(bool asynchronous)
        => await Check(asynchronous, "legacy messages", "def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\ntry:f()\nexcept TypeError as error:print(str(error))\ntry:f(1,2,3,**{'c':4,'d':5})\nexcept TypeError as error:print(str(error))", "Function 'f' is missing argument 'a'.\nFunction 'f' got multiple values for argument 'c'.\n");

    private static async Task Check(bool asynchronous, string name, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }
}
