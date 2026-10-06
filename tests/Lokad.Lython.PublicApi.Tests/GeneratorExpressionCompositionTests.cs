using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorExpressionCompositionTests
{
    [Fact]
    public async Task FormattedFieldsRetainSentValues()
    {
        await AssertOutput("""
            def generate():
             return f'left:{(yield "value")!r}:right'
            g=generate()
            print(next(g))
            try:g.send('a')
            except StopIteration as e:print(e.value)
            """, "value\nleft:'a':right\n");
    }

    [Fact]
    public async Task DynamicFormatSpecifierSuspendsInOrder()
    {
        await AssertOutput("""
            events=[]
            def mark(name,value):
             events.append(name)
             return value
            def generate():
             return f'{mark("value",(yield "value")):{mark("spec",(yield "width"))}}'
            g=generate()
            print(next(g),events)
            print(g.send(12),events)
            try:g.send('04d')
            except StopIteration as e:print(e.value,events)
            """, "value []\nwidth ['value']\n0012 ['value', 'spec']\n");
    }

    [Fact]
    public async Task NestedFormattedSpecifiersAndFields()
    {
        await AssertOutput("""
            def generate():
             return f'{(yield "number"):{(yield "width")}.{(yield "precision")}f}={(yield "text")!s}'
            g=generate()
            print(next(g),g.send(1.25),g.send(7),g.send(2))
            try:g.send('done')
            except StopIteration as e:print(e.value)
            """, "number width precision text\n   1.25=done\n");
    }

    [Fact]
    public async Task SolitaryStarExpansionWaitsForKeywords()
    {
        await AssertOutput("""
            events=[]
            def values():
             events.append('iterate')
             yield 1
             yield 2
            def run(*args,**kwargs):return args,kwargs
            def generate():
             return run(*(yield 'args'), next=(yield 'next'))
            g=generate()
            print(next(g),events)
            print(g.send(values()),events)
            try:g.send(3)
            except StopIteration as e:print(e.value,events)
            """, "args []\nnext []\n((1, 2), {'next': 3}) ['iterate']\n");
    }

    [Fact]
    public async Task StarredKeywordCallsAndCollisions()
    {
        await AssertOutput("""
            def run(**kwargs):return kwargs
            def generate():
             try:return run(a=1, **(yield 'mapping'), b=(yield 'later'))
             finally:print('cleanup')
            g=generate()
            print(next(g))
            try:g.send({'a':2})
            except TypeError:print('duplicate')
            g=generate()
            print(next(g),g.send({'c':3}))
            try:g.send(4)
            except StopIteration as e:print(e.value)
            """, "mapping\ncleanup\nduplicate\nmapping later\ncleanup\n{'a': 1, 'c': 3, 'b': 4}\n");
    }

    [Fact]
    public async Task ListsRetainExpandedPrefixAcrossSuspension()
    {
        await AssertOutput("""
            events=[]
            def values():
             events.append('unpack')
             yield 1
             yield 2
            def generate():return [0,*(yield 'items'),(yield 'tail')]
            g=generate()
            print(next(g),events)
            print(g.send(values()),events)
            try:g.send(3)
            except StopIteration as e:print(e.value,events)
            """, "items []\ntail ['unpack']\n[0, 1, 2, 3] ['unpack']\n");
    }

    [Fact]
    public async Task TupleAndSetDisplaysSuspend()
    {
        await AssertOutput("""
            def tuples():return (*(yield 'items'),(yield 'tail'))
            g=tuples()
            print(next(g),g.send([1,2]))
            try:g.send(3)
            except StopIteration as e:print(e.value)
            def sets():return {0,*(yield 'items'),(yield 'tail')}
            g=sets()
            print(next(g),g.send([1,2,2]))
            try:g.send(3)
            except StopIteration as e:print(sorted(e.value))
            """, "items tail\n(1, 2, 3)\nitems tail\n[0, 1, 2, 3]\n");
    }

    [Fact]
    public async Task DictionaryUnpackingSuspends()
    {
        await AssertOutput("""
            def generate():return {'a':0,**(yield 'first'),(yield 'key'):(yield 'value'),**(yield 'last')}
            g=generate()
            print(next(g),g.send({'a':1,'b':2}),g.send('c'),g.send(3))
            try:g.send({'b':4})
            except StopIteration as e:print(e.value)
            """, "first key value last\n{'a': 1, 'b': 4, 'c': 3}\n");
    }

    [Fact]
    public async Task ListComprehensionOutermostIterable()
    {
        await AssertOutput("""
            def generate():
             values=[i*2 for i in (yield 'values') if i>1]
             yield values
             yield 'done'
            g=generate()
            print(next(g),g.send([1,2,3]),next(g))
            """, "values [4, 6] done\n");
    }

    [Fact]
    public async Task SetAndDictionaryComprehensionOuterIterables()
    {
        await AssertOutput("""
            def generate():
             yield {i*2 for i in (yield 'set')}
             yield {i:i*2 for i in (yield 'dict')}
            g=generate()
            print(next(g),sorted(g.send([1,2,2])),next(g),g.send([2,3]))
            """, "set [2, 4] dict {2: 4, 3: 6}\n");
    }

    [Fact]
    public async Task GeneratorExpressionRemainsLazy()
    {
        await AssertOutput("""
            events=[]
            def mark(i):
             events.append(i)
             return i*2
            def generate():
             return (mark(i) for i in (yield 'values'))
            g=generate()
            print(next(g))
            try:g.send([1,2])
            except StopIteration as e:values=e.value
            print(events,next(values),events,list(values),events)
            """, "values\n[1, 2] 2 [1, 2] [4] [1, 2]\n");
    }

    [Fact]
    public async Task ComprehensionWalrusTargetsGeneratorScope()
    {
        await AssertOutput("""
            def generate():
             values=[(last:=i*2) for i in (yield 'values')]
             yield values,last
            g=generate()
            print(next(g),g.send([1,2,3]))
            """, "values ([2, 4, 6], 6)\n");
    }

    [Fact]
    public async Task MatchGuardsSuspendWithCapturesAndWalrus()
    {
        await AssertOutput("""
            def generate():
             match 2:
              case capture if (accepted := (yield capture)):
               yield capture,accepted
              case fallback:
               yield fallback,accepted
            g=generate()
            print(next(g),g.send(9))
            g=generate()
            print(next(g),g.send(False))
            """, "2 (2, 9)\n2 (2, False)\n");
    }

    [Fact]
    public async Task ExceptionHeaderCompositionsSuspend()
    {
        await AssertOutput("""
            def child():
             value=yield 'type'
             return value
            def generate():
             try:raise ValueError('original')
             except (*(yield from child()),) as e:
              yield str(e)
             finally:yield 'cleanup'
            g=generate()
            print(next(g),g.send([ValueError]),next(g))
            """, "type original cleanup\n");
    }

    [Fact]
    public async Task InjectedErrorsUnwindSuspendedCallArguments()
    {
        await AssertOutput("""
            events=[]
            def run(*args):events.append('wrong call')
            def generate():
             try:
              run(*(yield 'items'),(yield 'tail'))
             except ValueError as e:
              yield str(e)
             finally:events.append('cleanup')
            g=generate()
            print(next(g),g.send([1,2]),g.throw(ValueError('injected')))
            g.close()
            print(events)
            """, "items tail injected\n['cleanup']\n");
    }

    [Fact]
    public async Task ClosingSuspendedDisplaysRunsCleanup()
    {
        await AssertOutput("""
            events=[]
            def generate():
             try:return [*(yield 'items'),(yield 'tail')]
             finally:events.append('cleanup')
            g=generate()
            print(next(g),g.send([1,2]))
            print(g.close(),events)
            """, "items tail\nNone ['cleanup']\n");
    }

    [Fact]
    public async Task FormattedValueRunsBeforeSpecifier()
    {
        await AssertOutput("""
            events=[]
            def value():
             events.append('value')
             return 12
            def spec():
             events.append('spec')
             return '04d'
            print(f'{value():{spec()}}',events)
            """, "0012 ['value', 'spec']\n");
    }

    [Fact]
    public async Task SuspendingCallsRejectNonCallableAfterArguments()
    {
        await AssertOutput("""
            def generate():
                target=yield 'target'
                return target(*(yield 'values'))
            g=generate()
            print(next(g),g.send(1))
            try:g.send([1])
            except TypeError:print('not callable')
            """, "target values\nnot callable\n");
    }

    [Fact]
    public async Task MultiplePositionalStarsExpandBeforeKeywords()
    {
        await AssertOutput("""
            events=[]
            def values():
             events.append('iterate')
             yield 1
            def run(*args,**kwargs):return args,kwargs
            def generate():return run(0,*(yield 'items'),tail=(yield 'tail'))
            g=generate()
            print(next(g),g.send(values()),events)
            try:g.send(2)
            except StopIteration as e:print(e.value,events)
            """, "items tail ['iterate']\n((0, 1), {'tail': 2}) ['iterate']\n");
    }

    [Fact]
    public async Task DictionaryMappingsSnapshotKeysBeforeValues()
    {
        await AssertOutput("""
            events=[]
            class Mapping:
             def keys(self):
              events.append('keys')
              yield 'a'
              events.append('keys end')
              yield 'b'
             def __getitem__(self,key):
              events.append(key)
              return 1
            def generate():return {**(yield 'mapping'),'c':2}
            g=generate()
            print(next(g))
            try:g.send(Mapping())
            except StopIteration as e:print(e.value,events)
            """, "mapping\n{'a': 1, 'b': 1, 'c': 2} ['keys', 'keys end', 'a', 'b']\n");
    }

    [Theory]
    [InlineData("call", 5)]
    [InlineData("display", 3)]
    [InlineData("formatted", 2)]
    [InlineData("comprehension", 3)]
    public async Task SuspendedExpressionsAwaitHostEffectsAndCloseCleanup(string kind, int expectedSuspensions)
    {
        var expression = kind switch
        {
            "call" => "run(0, *(yield 'items'), **(yield 'options'), tail=int(Path('/number.txt').read_text()))",
            "display" => "[*(yield 'items'), int(Path('/number.txt').read_text())]",
            "formatted" => "f\"{(yield 'value'):{(yield 'spec')}}\"",
            _ => "[i + int(Path('/number.txt').read_text()) for i in (yield 'items')]",
        };
        var source = """
            from pathlib import Path
            def items():
                yield int(Path('/number.txt').read_text())
            class Mapping:
                def keys(self):
                    Path('/number.txt').read_text()
                    return ['flag']
                def __getitem__(self,key):
                    return int(Path('/number.txt').read_text())
            class Formatted:
                def __format__(self,spec):
                    return Path('/number.txt').read_text() + spec
            def run(*args,**kwargs):
                return args,kwargs
            """ + "\ndef generate():\n    try:\n        yield " + expression
                + "\n    finally:\n        Path('/closed.txt').write_text('closed')\ng=generate()\nprint(next(g))\n";
        source += kind switch
        {
            "call" => "print(g.send(items()))\nprint(g.send(Mapping()))\n",
            "formatted" => "print(g.send(Formatted()))\nprint(g.send('!'))\n",
            _ => "print(g.send(items()))\n",
        };
        source += "g.close()\n";
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
        Assert.True(delayed.CompletedAsynchronously >= expectedSuspensions);
    }

    [Theory]
    [InlineData("run(0, *(yield 'items'), (yield 'tail'))")]
    [InlineData("[*(yield 'items'), (yield 'tail')]")]
    [InlineData("(*(yield 'items'), (yield 'tail'))")]
    [InlineData("{*(yield 'items'), (yield 'tail')}")]
    public async Task SuspendedExpansionEnforcesCollectionAndMemoryLimits(string expression)
    {
        var compiled = new LythonEngine().Compile("def run(*args): return args\ndef generate():\n return " + expression
            + "\ng=generate()\nnext(g)\ng.send(range(1000000))\n");
        Assert.True(compiled.IsValid);
        foreach (var (options, error) in new[]
        {
            (new LythonRunOptions { MaxCollectionSize = 64 }, "RuntimeError"),
            (new LythonRunOptions { MaxExecutionMemoryBytes = 262144 }, "MemoryError"),
        })
        {
            foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
            {
                Assert.False(result.Success);
                Assert.Equal(error, result.Failure?.ExceptionType);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedCallBuffersFollowGeneratorLifetime(bool retain)
    {
        var source = "def run(*args): return args\ndef generate():\n run(0, *(yield 'items'), (yield 'tail'))\nkept=[]\nfor i in range(2000):\n g=generate()\n next(g)\n g.send(range(200))\n";
        if (retain) source += " kept.append(g)\n";
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
