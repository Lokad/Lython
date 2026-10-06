using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class YieldFormSyntaxTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[]
        {
            "generator_method_return_summary",
            "class C:\n def generate(self):\n  yield 1,\n  return {'done':True}\ng=C().generate()\nprint(next(g))\ntry:g.send(None)\nexcept StopIteration as e:print(e.value)",
            "(1,)\n{'done': True}\n"
        };
        yield return new object[]
        {
            "fstring_yield",
            "def generate():\n yield f\"{yield 1}\"\ng=generate()\nprint(next(g),g.send(2))",
            "1 2\n"
        };
        yield return new object[]
        {
            "yield_star",
            "def run():yield *[1,2],\nprint(next(run()))",
            "(1, 2)\n"
        };
        yield return new object[]
        {
            "fstring_yield_parenthesized",
            "def generate():\n yield f\"{(yield 1)}\"\ng=generate()\nprint(next(g),g.send(2))",
            "1 2\n"
        };
        yield return new object[]
        {
            "fstring_yield_from",
            "def generate():\n yield f\"{yield from [1]}\"\ng=generate()\nprint(next(g),g.send(None))",
            "1 None\n"
        };
        yield return new object[]
        {
            "tuple_and_starred_send",
            "def generate():\n first=(yield 1,)\n second=(yield *[2,3],)\n third=(yield 4,*[5,6],)\n return first,second,third\ng=generate()\nprint(next(g),g.send('one'),g.send('two'))\ntry:g.send('three')\nexcept StopIteration as e:print(e.value)",
            "(1,) (2, 3) (4, 5, 6)\n('one', 'two', 'three')\n"
        };
        yield return new object[]
        {
            "bare_yield_field",
            "def generate():\n yield f\"{yield}\"\ng=generate()\nprint(next(g),g.send('sent'))",
            "None sent\n"
        };
        yield return new object[]
        {
            "yielded_tuple_field",
            "def generate():\n yield f\"{yield 1,}\"\ng=generate()\nprint(next(g),g.send('sent'))",
            "(1,) sent\n"
        };
        yield return new object[]
        {
            "yielded_starred_field",
            "def generate():\n yield f\"{yield *[1,2],}\"\ng=generate()\nprint(next(g),g.send('sent'))",
            "(1, 2) sent\n"
        };
        yield return new object[]
        {
            "nested_format_yield",
            "def generate():\n return f\"{yield 'value':{yield 'width'}}\"\ng=generate()\nprint(next(g),g.send(7))\ntry:g.send('04d')\nexcept StopIteration as e:print(e.value)",
            "value width\n0007\n"
        };
        yield return new object[]
        {
            "delegation_return_field",
            "def inner():\n yield 1\n return 7\ndef generate():\n return f\"{yield from inner()}\"\ng=generate()\nprint(next(g))\ntry:next(g)\nexcept StopIteration as e:print(e.value)",
            "1\n7\n"
        };
        yield return new object[]
        {
            "commented_yield_fields",
            "def generate():\n return f\"{yield # } : ! '\n1,}\"\ng=generate()\nprint(next(g))\ntry:g.send(7)\nexcept StopIteration as e:print(e.value)",
            "(1,)\n7\n"
        };
        yield return new object[]
        {
            "debug_yield_field",
            "def generate():\n return f\"{yield 1=}\"\ng=generate()\nprint(next(g))\ntry:g.send(7)\nexcept StopIteration as e:print(e.value)",
            "1\nyield 1=7\n"
        };
        yield return new object[]
        {
            "tuple_key_and_class_cell_composition",
            "class Box:\n def __getitem__(self,key):return key\nclass C:\n def generate(self):\n  yield Box()[(yield 1,):(yield 3,),f\"{yield 'index'}\"]\n  yield __class__ is C\ng=C().generate()\nprint(next(g),g.send(1),g.send(3),g.send(0),next(g))",
            "(1,) (3,) index (slice(1, 3, None), '0') True\n"
        };
        yield return new object[]
        {
            "close_cleans_field_generator",
            "def generate():\n try:return f\"{yield 1}\"\n finally:print('cleanup')\ng=generate()\nprint(next(g))\ng.close()",
            "1\ncleanup\n"
        };
        yield return new object[]
        {
            "outer_comprehension_iterable_field",
            "def generate():\n return [x for x in f\"{yield 1}\"]\ng=generate()\nprint(next(g))\ntry:g.send(23)\nexcept StopIteration as e:print(e.value)",
            "1\n['2', '3']\n"
        };
        yield return new object[]
        {
            "lambda_field_generator",
            "fn=lambda:f\"{yield 1}\"\ng=fn()\nprint(next(g))\ntry:g.send(7)\nexcept StopIteration as e:print(e.value)",
            "1\n7\n"
        };
        yield return new object[]
        {
            "definition_inputs_with_field_yields",
            "def outer():\n def inner(x=f\"{yield 'default'}\",y:f\"{yield 'annotation'}\"=0):return x\n return inner\ng=outer()\nprint(next(g),g.send('first'))\ntry:g.send('second')\nexcept StopIteration as e:print(e.value(),e.value.__annotations__['y'])",
            "default annotation\nfirst second\n"
        };
    }

    [Fact]
    public async Task FieldSuspensionRetainsValuesAcrossDelayedHostEffects()
    {
        var compiled = new LythonEngine().Compile("""
            def load():
             with open('/value.txt') as f:return int(f.read())
            def generate():
             try:return f"{yield load(), # } : ! '
            }:{load()}"
             finally:print('cleanup')
            g=generate()
            print(next(g))
            try:g.send('sent')
            except StopIteration as e:print(e.value)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value.txt", "7");
        var sync = compiled.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("(7,)\ncleanup\nsent:7\n", sync.StandardOutput);
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "7");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 2);
    }

    [Fact]
    public async Task OversizedStarredYieldValuesCleanUpBeforeEmission()
    {
        var compiled = new LythonEngine().Compile("""
            def generate():
             try:return (yield *range(1000000),)
             finally:print('cleanup')
            list(generate())
            """);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxCollectionSize = 64 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Equal("cleanup\n", result.StandardOutput);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task YieldFormsFollowPython(string name, string source, string expected)
    {
        _ = name;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("print(f\"{yield 1}\")")]
    [InlineData("def f():\n return [f\"{yield 1}\" for x in [1]]")]
    [InlineData("def f():\n return [f\"{yield from [1]}\" for x in [1]]")]
    [InlineData("class C:\n value=f\"{yield 1}\"")]
    [InlineData("from __future__ import annotations\ndef outer():\n def f(x:f\"{yield 1}\"):pass")]
    [InlineData("def f[T: f\"{yield 1}\"]():pass")]
    public void ForbiddenYieldContextsFailBeforeEffects(string source)
        => Assert.False(new LythonEngine().Compile("print('effect')\n" + source).IsValid);
}
