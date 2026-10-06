using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorStatementCompositionTests
{
    [Fact]
    public async Task AssertionsSuspendOnlyWhenNeeded()
    {
        await AssertOutput("""
            def generate():
             assert (yield 'condition'), (yield 'message')
             yield 'done'
            g=generate()
            print(next(g),g.send(True))
            g=generate()
            print(next(g),g.send(False))
            try:g.send('failed')
            except AssertionError as e:print(e.args)
            """, "condition done\ncondition message\n('failed',)\n");
    }

    [Fact]
    public async Task AssertionMessagesRetainActualObjects()
    {
        await AssertOutput("""
            message=['reason']
            def generate():
             try:assert False,(yield 'message')
             except AssertionError as e:yield e.args[0] is message,e.args
             finally:print('cleanup')
            g=generate()
            print(next(g),g.send(message))
            g.close()
            """, "message (True, (['reason'],))\ncleanup\n");
    }

    [Fact]
    public async Task RaiseExpressionsAndCausesSuspendInOrder()
    {
        await AssertOutput("""
            def generate():
             try:raise (yield 'raised') from (yield 'cause')
             except ValueError as e:yield str(e),str(e.__cause__),e.__suppress_context__
            g=generate()
            print(next(g),g.send(ValueError('outer')),g.send(TypeError('cause')))
            """, "raised cause ('outer', 'cause', True)\n");
    }

    [Fact]
    public async Task InvalidRaisedValuesStillEvaluateCause()
    {
        await AssertOutput("""
            events=[]
            def cause():
             events.append('cause')
             return None
            try:raise 0 from cause()
            except TypeError:print(events)
            def generate():
             try:raise 0 from (yield 'cause')
             except TypeError:yield 'invalid'
            g=generate()
            print(next(g),g.send(None))
            """, "['cause']\ncause invalid\n");
    }

    [Fact]
    public async Task RaisedIdentityAndContextSurviveSuspension()
    {
        await AssertOutput("""
            error=ValueError('new')
            def generate():
             try:raise KeyError('old')
             except KeyError as old:
              try:raise (yield 'error') from (yield 'cause')
              except ValueError as e:yield e is error,e.__context__ is old,e.__cause__ is None,e.__suppress_context__
            g=generate()
            print(next(g),g.send(error),g.send(None))
            """, "error cause (True, True, True, True)\n");
    }

    [Fact]
    public async Task DeletingAttributesSuspendsReceiver()
    {
        await AssertOutput("""
            class Box:pass
            box=Box()
            box.value=1
            def generate():
             del (yield 'receiver').value
             yield hasattr(box,'value')
            g=generate()
            print(next(g),g.send(box))
            """, "receiver False\n");
    }

    [Fact]
    public async Task DeletingItemsCapturesReceiverAndIndex()
    {
        await AssertOutput("""
            box=[1,2,3]
            def generate():
             del (yield 'receiver')[(yield 'index')]
             yield box
            g=generate()
            print(next(g),g.send(box),g.send(1))
            """, "receiver index [1, 3]\n");
    }

    [Fact]
    public async Task DeletingSlicesRetainsAllBounds()
    {
        await AssertOutput("""
            box=[0,1,2,3,4]
            def generate():
             del (yield 'receiver')[(yield 'start'):(yield 'end'):(yield 'step')]
             yield box
            g=generate()
            print(next(g),g.send(box),g.send(0),g.send(5),g.send(2))
            """, "receiver start end step [1, 3]\n");
    }

    [Fact]
    public async Task DeleteGroupsExecuteEachTargetBeforeNextSuspension()
    {
        await AssertOutput("""
            x=1
            box=[2,3]
            def generate():
             global x
             del [x,box[(yield 'index')]]
             yield box
            g=generate()
            print(next(g),'x' in globals(),g.send(0))
            """, "index False [3]\n");
    }

    [Fact]
    public async Task DeleteUserSlicesPreserveRawBounds()
    {
        await AssertOutput("""
            events=[]
            class Box:
             def __delitem__(self,key):events.append((key.start,key.stop,key.step))
            def generate():
             del (yield 'receiver')[(yield 'start'):4:2]
             yield events
            g=generate()
            print(next(g),g.send(Box()),g.send('raw'))
            """, "receiver start [('raw', 4, 2)]\n");
    }

    [Fact]
    public async Task ThrownErrorsAndCloseUnwindStatementOperands()
    {
        await AssertOutput("""
            events=[]
            def generate():
             try:
              assert False,(yield 'message')
             except ValueError as e:yield str(e)
             finally:events.append('cleanup')
            g=generate()
            print(next(g),g.throw(ValueError('injected')))
            g.close()
            print(events)
            def raised():
             try:raise ValueError from (yield 'cause')
             finally:events.append('raise cleanup')
            g=raised()
            print(next(g))
            g.close()
            print(events)
            """, "message injected\n['cleanup']\ncause\n['cleanup', 'raise cleanup']\n");
    }

    [Fact]
    public async Task BareRaiseKeepsOuterHandlerAfterSuspension()
    {
        await AssertOutput("""
            def generate():
             try:raise ValueError('old')
             except ValueError:
              yield 'pause'
              raise
            g=generate()
            print(next(g))
            try:next(g)
            except ValueError as e:print(str(e))
            """, "pause\nold\n");
    }

    [Theory]
    [InlineData("item")]
    [InlineData("slice")]
    [InlineData("attribute")]
    [InlineData("property")]
    [InlineData("descriptor")]
    public async Task SuspendedDeletesAwaitProtocolsAndCleanup(string kind)
    {
        var source = """
            from pathlib import Path
            class Descriptor:
             def __delete__(self,instance):Path('/deleted.txt').write_text('deleted')
            class Box:
             field=Descriptor()
             def __delitem__(self,key):Path('/deleted.txt').write_text('deleted')
             @property
             def value(self):return 1
             @value.deleter
             def value(self):Path('/deleted.txt').write_text('deleted')
            class Custom:
             def __delattr__(self,name):Path('/deleted.txt').write_text('deleted')
            """;
        var target = kind switch
        {
            "item" => "(yield 'receiver')[(yield 'index')]",
            "slice" => "(yield 'receiver')[(yield 'index'):3]",
            "attribute" => "(yield 'receiver').value",
            "property" => "(yield 'receiver').value",
            _ => "(yield 'receiver').field",
        };
        source += "\ndef generate():\n try:\n  del " + target + "\n  yield 'done'\n finally:Path('/closed.txt').write_text('closed')\ng=generate()\nprint(next(g))\nprint(g.send(" + (kind == "attribute" ? "Custom()" : "Box()") + "))\n";
        if (kind is "item" or "slice") source += "print(g.send(0))\n";
        source += "g.close()\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost();
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal("deleted", delayed.ReadText("/deleted.txt"));
        Assert.Equal("closed", delayed.ReadText("/closed.txt"));
        Assert.True(delayed.CompletedAsynchronously >= 2);
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
