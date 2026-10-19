using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionAttributeDeletionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def f(): pass\nf.tag=lambda:1\nsaved=f.tag\nfor phase in range(3):\n try: print(f.tag())\n except AttributeError: print('missing')\n if phase==0: f.tag=lambda:2\n elif phase==1: del f.tag\nprint(saved())", "1\n2\nmissing\n1\n" },
        { "def f(): return 1\nf.tag=[]\nsaved=f.tag\ndel f.tag\nprint(hasattr(f,'tag'),saved,f())\ntry: del f.tag\nexcept AttributeError: print('missing')", "False [] 1\nmissing\n" },
        { "f=lambda value=3:value\nsetattr(f,'tag',4)\ndelattr(f,'tag')\nf.tag=5\nprint(f.tag,f(),f.__defaults__)\ndel f.tag\ntry: print(f.tag)\nexcept AttributeError: print('missing')", "5 3 (3,)\nmissing\n" },
        { "def f(): yield 7\nf.__custom__=9\ndel f.__custom__\nprint(hasattr(f,'__custom__'),list(f()))", "False [7]\n" },
        { "class C:\n def f(self): return 2\n f.tag=8\n del f.tag\n print(hasattr(f,'tag'))\nprint(C().f())", "False\n2\n" },
        { "def f(): pass\nf.__name__='renamed'\nf.__qualname__='qualified'\nfor name in ('__name__','__qualname__','__dict__'):\n try: delattr(f,name)\n except TypeError: print(name,'protected')\nprint(f.__name__,f.__qualname__)", "__name__ protected\n__qualname__ protected\n__dict__ protected\nrenamed qualified\n" },
        { "def f(value:int):\n 'doc'\n return value\nold=f.__annotations__\nf.__module__='other'\nfor repeat in range(2):\n del f.__doc__\n del f.__module__\n del f.__annotations__\n print(f.__doc__,f.__module__,f.__annotations__)\nprint(old['value'] is int,f(4))", "None None {}\nNone None {}\nTrue 4\n" },
        { "f=lambda value=3,*,key=4:(value,key)\nf.tag=5\ndel f.tag\ndel f.__defaults__\ndel f.__kwdefaults__\nprint(f.__defaults__,f.__kwdefaults__,f(1,key=2))\ntry: f()\nexcept TypeError: print('missing')", "None None (1, 2)\nmissing\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task DeletionPreservesPythonMetadataBehavior(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletedAttributeAliasesRemainFunded(bool lambda)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile((lambda ? "f=lambda:None" : "def f(): pass") +
            "\nf.tag='x'*4096\nsaved=f.tag\ndel f.tag\nreturn saved", budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new string('x', 4096), Assert.IsType<string>(retained.ReturnValue));
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.True(held > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
