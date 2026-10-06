using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorTargetCompositionTests
{
    [Fact]
    public async Task AttributeReceiverSuspendsAfterValue()
    {
        await AssertOutput("""
            events=[]
            class Box:pass
            box=Box()
            def mark():
             events.append('value')
             return 7
            def generate():
             (yield 'receiver').value=mark()
             yield box.value
            g=generate()
            print(next(g),events)
            print(g.send(box),events)
            """, "receiver ['value']\n7 ['value']\n");
    }

    [Fact]
    public async Task SubscriptReceiversAndIndicesSuspend()
    {
        await AssertOutput("""
            box=[0,0]
            def generate():
             (yield 'receiver')[(yield 'index')]=7
             yield box
            g=generate()
            print(next(g),g.send(box),g.send(1))
            """, "receiver index [0, 7]\n");
    }

    [Fact]
    public async Task SlicesRetainAllOperands()
    {
        await AssertOutput("""
            box=[0,1,2,3,4]
            def generate():
             (yield 'receiver')[(yield 'start'):(yield 'end'):(yield 'step')]=[8,9]
             yield box
            g=generate()
            print(next(g),g.send(box),g.send(1),g.send(5),g.send(2))
            """, "receiver start end step [0, 8, 2, 9, 4]\n");
    }

    [Fact]
    public async Task ChainedStoresRetainValueAndOrder()
    {
        await AssertOutput("""
            x=0
            class Box:pass
            box=Box()
            def generate():
             global x
             x=(yield 'receiver').value=7
             yield x,box.value
            g=generate()
            print(next(g),x)
            print(g.send(box),x)
            """, "receiver 7\n(7, 7) 7\n");
    }

    [Fact]
    public async Task NestedStarredUnpackingSuspendsInStoreOrder()
    {
        await AssertOutput("""
            class Box:pass
            box=Box()
            def generate():
             a,*(yield a).rest=[1,2,3]
             yield a,box.rest
            g=generate()
            print(next(g),g.send(box))
            """, "1 (1, [2, 3])\n");
    }

    [Fact]
    public async Task UnpackingLaterIndicesSeeEarlierStores()
    {
        await AssertOutput("""
            box=[0,0]
            def generate():
             index,box[(yield index)]=[1,9]
             yield box,index
            g=generate()
            print(next(g),g.send(1))
            """, "1 ([0, 9], 1)\n");
    }

    [Fact]
    public async Task LoopTargetsSuspendOnEachAssignment()
    {
        await AssertOutput("""
            class Box:pass
            box=Box()
            def generate():
             for (yield 'receiver').value in [1,2]:
              yield box.value
             else:yield 'else'
            g=generate()
            print(next(g),g.send(box),next(g),g.send(box),next(g))
            """, "receiver 1 receiver 2 else\n");
    }

    [Fact]
    public async Task NestedLoopTargetsPreservePartialBindings()
    {
        await AssertOutput("""
            box=[0,0]
            def generate():
             for i,box[(yield i)] in [(0,8),(1,9)]:
              yield i,box
              if i:break
             yield 'done'
            g=generate()
            print(next(g),g.send(0),next(g),g.send(1),next(g))
            """, "0 (0, [8, 9]) 1 (1, [8, 9]) done\n");
    }

    [Fact]
    public async Task WithTargetsSuspendInsideProtectedSuite()
    {
        await AssertOutput("""
            events=[]
            class Box:pass
            box=Box()
            class Manager:
             def __enter__(self):
              events.append('enter')
              return 7
             def __exit__(self,*args):events.append('exit')
            def generate():
             with Manager() as (yield 'receiver').value:
              yield box.value
            g=generate()
            print(next(g),events)
            print(g.send(box),events)
            g.close()
            print(events)
            g=generate()
            print(next(g))
            g.close()
            print(events)
            """, "receiver ['enter']\n7 ['enter']\n['enter', 'exit']\nreceiver\n['enter', 'exit', 'enter', 'exit']\n");
    }

    [Fact]
    public async Task AugmentedMemberReadsBeforeRightHandSuspension()
    {
        await AssertOutput("""
            class Box:pass
            box=Box()
            box.value=[1]
            original=box.value
            def generate():
             box.value+=(yield 'rhs')
             yield box.value is original,box.value
            g=generate()
            print(next(g))
            box.value=[]
            print(g.send([2]))
            """, "rhs\n(True, [1, 2])\n");
    }

    [Fact]
    public async Task AugmentedSubscriptCapturesReceiverAndIndex()
    {
        await AssertOutput("""
            box=[1,2]
            other=[7,8]
            def generate():
             (yield 'receiver')[(yield 'index')]+=(yield 'rhs')
             yield box,other
            g=generate()
            print(next(g),g.send(box),g.send(0))
            box[0]=99
            print(g.send(3))
            """, "receiver index rhs\n([4, 2], [7, 8])\n");
    }

    [Fact]
    public async Task AugmentedSlicesHandleUserProtocols()
    {
        await AssertOutput("""
            events=[]
            class Box:
             def __getitem__(self,key):
              events.append(('get',key.start,key.stop,key.step))
              return [1]
             def __setitem__(self,key,value):events.append(('set',key.start,key.stop,key.step,value))
            box=Box()
            def generate():
             (yield 'receiver')[(yield 'start'):2]+=(yield 'rhs')
             yield events
            g=generate()
            print(next(g),g.send(box),g.send(0),events)
            print(g.send([2]))
            """, "receiver start rhs [('get', 0, 2, None)]\n[('get', 0, 2, None), ('set', 0, 2, None, [1, 2])]\n");
    }

    [Fact]
    public async Task LocalAnnotationsDoNotExecuteYieldExpressions()
    {
        await AssertOutput("""
            def generate():
             x:(yield 'wrong')
             y:(yield 'wrong')=2
             print(y)
            print(list(generate()))
            """, "2\n[]\n");
    }

    [Fact]
    public async Task AnnotationOnlyTargetsStillEvaluateReads()
    {
        await AssertOutput("""
            class Box:pass
            box=Box()
            def generate():
             (yield 'receiver').value:(yield 'wrong')
             yield hasattr(box,'value')
            g=generate()
            print(next(g),g.send(box))
            """, "receiver False\n");
    }

    [Theory]
    [InlineData("attribute", 3)]
    [InlineData("item", 3)]
    [InlineData("with", 2)]
    public async Task SuspendedTargetsAwaitUserProtocolsAndCleanup(string kind, int suspensions)
    {
        var source = """
            from pathlib import Path
            class Box:
             @property
             def value(self):return int(Path('/number.txt').read_text())
             @value.setter
             def value(self,value):Path('/stored.txt').write_text(str(value))
             def __getitem__(self,key):return int(Path('/number.txt').read_text())
             def __setitem__(self,key,value):Path('/stored.txt').write_text(str(value))
            class Manager:
             def __enter__(self):return int(Path('/number.txt').read_text())
             def __exit__(self,*args):Path('/closed.txt').write_text('closed')
            box=Box()
            """;
        source += kind switch
        {
            "attribute" => "\ndef generate():\n try:\n  (yield 'receiver').value+=(yield 'rhs')\n  yield 'done'\n finally:Path('/closed.txt').write_text('closed')\ng=generate()\nprint(next(g),g.send(box),g.send(3))\ng.close()\n",
            "item" => "\ndef generate():\n try:\n  (yield 'receiver')[(yield 'index')]+=(yield 'rhs')\n  yield 'done'\n finally:Path('/closed.txt').write_text('closed')\ng=generate()\nprint(next(g),g.send(box),g.send(0),g.send(3))\ng.close()\n",
            _ => "\ndef generate():\n with Manager() as (yield 'receiver').value:yield 'done'\ng=generate()\nprint(next(g))\ng.close()\n",
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
        if (kind != "with") Assert.Equal("5", delayed.ReadText("/stored.txt"));
        Assert.True(delayed.CompletedAsynchronously >= suspensions);
    }

    [Fact]
    public async Task SuspendedUnpackingHonorsCollectionLimitsBeforeStores()
    {
        var compiled = new LythonEngine().Compile("def generate():\n a,*(yield 'receiver').rest=range(1000000)\nlist(generate())\n");
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxCollectionSize = 64 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
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
