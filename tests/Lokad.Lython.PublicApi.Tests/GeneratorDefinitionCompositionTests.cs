using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorDefinitionCompositionTests
{
    [Fact]
    public async Task DefaultsSuspendInParameterOrder()
    {
        await AssertOutput("""
            def generate():
             def run(a=(yield 'a'),/,b=(yield 'b'),*,c=(yield 'c')):return a,b,c
             yield run()
            g=generate()
            print(next(g),g.send(1),g.send(2),g.send(3))
            """, "a b c (1, 2, 3)\n");
    }

    [Fact]
    public async Task DecoratorsDefaultsAndAnnotationsHavePythonOrder()
    {
        await AssertOutput("""
            events=[]
            def decorate(name):
             events.append(('evaluate',name))
             def apply(fn):
              events.append(('apply',name,fn.__annotations__))
              return fn
             return apply
            def generate():
             @decorate((yield 'first decorator'))
             @decorate((yield 'second decorator'))
             def run(a:(yield 'a annotation')=(yield 'a default'),*,b:(yield 'b annotation')=(yield 'b default'))->(yield 'return annotation'):return a,b
             yield run(),events
            g=generate()
            print(next(g),g.send('first'),g.send('second'))
            print(g.send(1),g.send(2),g.send('A'),g.send('B'))
            print(g.send('R'))
            """, "first decorator second decorator a default\nb default a annotation b annotation return annotation\n((1, 2), [('evaluate', 'first'), ('evaluate', 'second'), ('apply', 'second', {'a': 'A', 'b': 'B', 'return': 'R'}), ('apply', 'first', {'a': 'A', 'b': 'B', 'return': 'R'})])\n");
    }

    [Fact]
    public async Task OrdinaryAnnotationsExecuteBeforeDecoration()
    {
        await AssertOutput("""
            events=[]
            def mark(name):
             events.append(name)
             return name
            def decorate(fn):
             events.append(fn.__annotations__)
             return fn
            @decorate
            def run(a:mark('annotation')=mark('default'))->mark('return'):return a
            print(run(),events)
            """, "default ['default', 'annotation', 'return', {'a': 'annotation', 'return': 'return'}]\n");
    }

    [Fact]
    public async Task LambdaDefaultsSuspendOutsideTheLambdaBody()
    {
        await AssertOutput("""
            def generate():
             run=lambda a=(yield 'a'),*,b=(yield 'b'):(a,b)
             yield run()
            g=generate()
            print(next(g),g.send(1),g.send(2))
            """, "a b (1, 2)\n");
    }

    [Fact]
    public async Task GeneratorLambdaKeepsItsOwnYieldScope()
    {
        await AssertOutput("""
            def generate():
             run=lambda a=(yield 'default'):(yield a)
             yield run()
            g=generate()
            print(next(g))
            inner=g.send(7)
            print(next(inner))
            print(inner.send(8) if False else list(inner))
            """, "default\n7\n[]\n");
    }

    [Fact]
    public async Task LambdaDefaultWalrusBindsTheEnclosingFunction()
    {
        await AssertOutput("""
            def generate():
             run=lambda value=(assigned:=(yield 'default')):value
             yield assigned,run()
            g=generate()
            print(next(g),g.send(7))
            """, "default (7, 7)\n");
    }

    [Fact]
    public async Task ClassDecoratorsAndHeaderArgumentsSuspendInOrder()
    {
        await AssertOutput("""
            events=[]
            class Base:
             def __init_subclass__(cls,flag):events.append(('init',flag))
            def bases():
             events.append('expand')
             yield Base
            def decorate(cls):
             events.append('decorate')
             return cls
            def generate():
             @(yield 'decorator')
             class Child(*(yield 'bases'),flag=(yield 'flag')):
              events.append('body')
             yield issubclass(Child,Base),events
            g=generate()
            print(next(g),g.send(decorate),g.send(bases()),events)
            print(g.send(7))
            """, "decorator bases flag ['expand']\n(True, ['expand', 'body', ('init', 7), 'decorate'])\n");
    }

    [Fact]
    public async Task ClassMappingHeadersExpandBeforeTheNextOperand()
    {
        await AssertOutput("""
            events=[]
            class Base:
             def __init_subclass__(cls,**options):events.append(options)
            class Mapping:
             def keys(self):
              events.append('keys')
              return ['flag']
             def __getitem__(self,key):
              events.append('get')
              return 1
            def generate():
             class Child((yield 'base'),**(yield 'mapping'),tail=(yield 'tail')):pass
             yield events
            g=generate()
            print(next(g),g.send(Base),g.send(Mapping()),events)
            print(g.send(2))
            """, "base mapping tail ['keys', 'get']\n['keys', 'get', {'flag': 1, 'tail': 2}]\n");
    }

    [Fact]
    public async Task ClassHeaderExpansionFailuresOccurBeforeTheBody()
    {
        await AssertOutput("""
            events=[]
            def generate():
             try:
              class Child(*(yield 'base')):events.append('wrong body')
             except TypeError:yield 'invalid'
            g=generate()
            print(next(g),g.send(1),events)
            """, "base invalid []\n");
    }

    [Fact]
    public async Task NestedDefinitionsRespectGlobalAndNonlocalBindings()
    {
        await AssertOutput("""
            def run():return 0
            class Child:pass
            def outer():
             nested=None
             def generate():
              global run
              nonlocal nested
              def run(value=(yield 'function')):return value
              class nested((yield 'base')):pass
              yield run(),issubclass(nested,Child)
             return generate()
            g=outer()
            print(next(g),g.send(7),g.send(Child),run())
            """, "function base (7, True) 7\n");
    }

    [Fact]
    public async Task FutureAnnotationsRemainUnevaluatedForSuspendingDecorators()
    {
        await AssertOutput("""
            from __future__ import annotations
            def decorate(fn):return fn
            def generate():
             @(yield 'decorator')
             def run(value:Unknown=7)->OtherUnknown:return value
             yield run()
            g=generate()
            print(next(g),g.send(decorate))
            """, "decorator 7\n");
    }

    [Fact]
    public async Task GenericFunctionDefaultsAndDecoratorsSuspendOutsideAnnotationScope()
    {
        await AssertOutput("""
            def decorate(fn):return fn
            def generate():
             @(yield 'decorator')
             def run[T](value:T=(yield 'default'))->T:return value
             yield run(),len(run.__type_params__),run.__annotations__['value'] is run.__type_params__[0]
            g=generate()
            print(next(g),g.send(decorate),g.send(7))
            """, "decorator default (7, 1, True)\n");
    }

    [Fact]
    public async Task GenericClassDecoratorsSuspendOutsideAnnotationScope()
    {
        await AssertOutput("""
            def decorate(cls):return cls
            def generate():
             @(yield 'decorator')
             class Child[T]:pass
             yield len(Child.__type_params__)
            g=generate()
            print(next(g),g.send(decorate))
            """, "decorator 1\n");
    }

    [Fact]
    public async Task CloseAndThrowAbandonPartiallyPreparedDefinitions()
    {
        await AssertOutput("""
            events=[]
            def decorate(fn):
             events.append('wrong decoration')
             return fn
            def generate():
             try:
              @decorate
              def run(value=(yield 'default')):return value
             except ValueError:yield 'caught'
             finally:events.append('cleanup')
            g=generate()
            print(next(g),g.throw(ValueError()))
            g.close()
            g=generate()
            print(next(g))
            g.close()
            print(events)
            """, "default caught\ndefault\n['cleanup', 'cleanup']\n");
    }

    [Fact]
    public async Task NestedGeneratorDefaultsRetainSharedClosureCells()
    {
        await AssertOutput("""
            def outer():
             value=1
             def generate():
              def inner(default=(yield 'default')):
               yield value,default
              yield inner
             g=generate()
             print(next(g))
             inner=g.send(7)
             value=2
             return inner
            print(list(outer()()))
            """, "default\n[(2, 7)]\n");
    }

    [Fact]
    public async Task ClosingExpandedClassHeadersRunsCleanup()
    {
        await AssertOutput("""
            events=[]
            class Base:pass
            def generate():
             try:
              class Child(*(yield 'bases'),flag=(yield 'flag')):events.append('wrong body')
             finally:events.append('cleanup')
            g=generate()
            print(next(g),g.send([Base]))
            g.close()
            print(events)
            """, "bases flag\n['cleanup']\n");
    }

    [Theory]
    [InlineData("function", 3)]
    [InlineData("class", 5)]
    [InlineData("lambda", 2)]
    public async Task SuspendingDefinitionsAwaitHostEffectsAndCleanup(string kind, int suspensions)
    {
        var source = """
            from pathlib import Path
            def decorate(value):
             Path('/number.txt').read_text()
             return value
            class Base:
             def __init_subclass__(cls,flag):Path('/number.txt').read_text()
            class Mapping:
             def keys(self):
              Path('/number.txt').read_text()
              return ['flag']
             def __getitem__(self,key):return int(Path('/number.txt').read_text())
            """;
        source += kind switch
        {
            "function" => "\ndef generate():\n try:\n  @(yield 'decorator')\n  def run(value:Path('/number.txt').read_text()=(yield 'default')):return value\n  yield run(),run.__annotations__\n finally:Path('/closed.txt').write_text('closed')\ng=generate()\nprint(next(g),g.send(decorate),g.send(7))\ng.close()\n",
            "class" => "\ndef generate():\n try:\n  @(yield 'decorator')\n  class Child((yield 'base'),**(yield 'mapping')):\n   value=Path('/number.txt').read_text()\n  yield Child.value\n finally:Path('/closed.txt').write_text('closed')\ng=generate()\nprint(next(g),g.send(decorate),g.send(Base),g.send(Mapping()))\ng.close()\n",
            _ => "\ndef generate():\n try:\n  run=lambda value=(yield 'default'),*,tail=int(Path('/number.txt').read_text()):value+tail\n  yield run()\n finally:Path('/closed.txt').write_text('closed')\ng=generate()\nprint(next(g),g.send(7))\ng.close()\n",
        };
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        immediate.SeedFile("/number.txt", "2");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/number.txt", "2");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal("closed", delayed.ReadText("/closed.txt"));
        Assert.True(delayed.CompletedAsynchronously >= suspensions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedFunctionStorageFollowsFunctionLifetime(bool retain)
    {
        var source = "def generate():\n def run(value=(yield 'default')):return value\n yield run\nkept=[]\nfor i in range(2000):\n g=generate()\n next(g)\n run=g.send(i)\n g.close()\n";
        if (retain) source += " kept.append(run)\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            if (retain)
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            }
            else Assert.True(result.Success, result.Failure?.Message);
        }
    }

    [Theory]
    [InlineData("from __future__ import annotations\ndef outer():\n def inner(value:(yield 1)):pass")]
    [InlineData("from __future__ import annotations\ndef outer():\n value:(yield 1)")]
    [InlineData("def outer():\n def inner[T](value:(yield 1)):pass")]
    [InlineData("def outer():\n class Child[T]((yield 1)):pass")]
    public void AnnotationScopesRejectSuspensionBeforeEffects(string source)
    {
        var compiled = new LythonEngine().Compile("print('must not execute')\n" + source);
        Assert.False(compiled.IsValid);
        Assert.Equal("", compiled.Run(new MockLythonHost()).StandardOutput);
    }

    private static async Task AssertOutput(string source, string expected)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
