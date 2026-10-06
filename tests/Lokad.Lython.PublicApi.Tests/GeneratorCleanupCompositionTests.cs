using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorCleanupCompositionTests
{
    [Fact]
    public async Task BranchingWhileConditionsReenterTheirFirstBlock()
    {
        await AssertOutput("""
            def generate():
             while (yield 'condition') and True:yield 'body'
             else:yield 'else'
            g=generate()
            print(next(g),g.send(True),next(g),g.send(False))
            """, "condition body condition else\n");
    }

    [Fact]
    public async Task ClosingRunsNestedFinallyTail()
    {
        await AssertOutput("""
            events=[]
            def generate():
             try:
              try:yield 'ready'
              finally:
               try:events.append('inner try')
               finally:events.append('inner finally')
               events.append('tail')
             finally:events.append('outer')
            g=generate()
            print(next(g))
            g.close()
            print(events)
            """, "ready\n['inner try', 'inner finally', 'tail', 'outer']\n");
    }

    [Fact]
    public async Task ThrownErrorsSurviveNestedSuspendingFinally()
    {
        await AssertOutput("""
            events=[]
            def generate():
             try:
              try:yield 'ready'
              finally:
               try:yield 'inner try'
               finally:yield 'inner finally'
               yield 'tail'
             finally:yield 'outer'
            g=generate()
            print(next(g),g.throw(ValueError('original')),next(g),next(g),next(g))
            try:next(g)
            except ValueError as e:print(str(e))
            """, "ready inner try inner finally tail outer\noriginal\n");
    }

    [Fact]
    public async Task ContinueReevaluatesBranchingWhileConditions()
    {
        await AssertOutput("""
            def generate():
             count=0
             while (yield 'condition') if count<2 else False:
              count+=1
              if count==1:continue
              yield count
             else:yield 'else'
            g=generate()
            print(next(g),g.send(True),g.send(True),next(g))
            """, "condition condition 2 else\n");
    }

    [Fact]
    public async Task ReturnSurvivesNestedNormalFinally()
    {
        await AssertOutput("""
            events=[]
            def generate():
             try:
              yield 'ready'
              return 7
             finally:
              try:events.append('nested')
              finally:events.append('nested cleanup')
              events.append('tail')
            g=generate()
            print(next(g))
            try:next(g)
            except StopIteration as e:print(e.value,events)
            """, "ready\n7 ['nested', 'nested cleanup', 'tail']\n");
    }

    [Fact]
    public async Task NestedNormalWithDoesNotReceiveOuterPendingException()
    {
        await AssertOutput("""
            events=[]
            class Manager:
             def __enter__(self):events.append('enter')
             def __exit__(self,t,v,b):
              events.append(t is None)
              return True
            def generate():
             try:yield 'ready'
             finally:
              with Manager():events.append('body')
              events.append('tail')
            g=generate()
            print(next(g))
            g.close()
            print(events)
            """, "ready\n['enter', 'body', True, 'tail']\n");
    }

    [Fact]
    public async Task CaughtErrorsPreserveOuterPendingReturn()
    {
        await AssertOutput("""
            def generate():
             try:
              yield 'ready'
              return 7
             finally:
              try:raise ValueError('inner')
              except ValueError:yield 'caught'
              yield 'tail'
            g=generate()
            print(next(g),next(g),next(g))
            try:next(g)
            except StopIteration as e:print(e.value)
            """, "ready caught tail\n7\n");
    }

    [Fact]
    public async Task SuppressedErrorsPreserveOuterPendingException()
    {
        await AssertOutput("""
            class Manager:
             def __enter__(self):pass
             def __exit__(self,*args):return True
            def generate():
             try:yield 'ready'
             finally:
              with Manager():raise KeyError('inner')
              yield 'tail'
            g=generate()
            print(next(g),g.throw(ValueError('outer')))
            try:next(g)
            except ValueError as e:print(str(e))
            """, "ready tail\nouter\n");
    }

    [Fact]
    public async Task SuppressedErrorsPreserveOuterPendingReturn()
    {
        await AssertOutput("""
            class Manager:
             def __enter__(self):pass
             def __exit__(self,*args):return True
            def generate():
             try:
              yield 'ready'
              return 7
             finally:
              with Manager():raise KeyError('inner')
              yield 'tail'
            g=generate()
            print(next(g),next(g))
            try:next(g)
            except StopIteration as e:print(e.value)
            """, "ready tail\n7\n");
    }

    [Fact]
    public async Task BreakInsideCleanupPreservesOuterReturn()
    {
        await AssertOutput("""
            def generate():
             try:
              yield 'ready'
              return 7
             finally:
              for i in range(2):
               try:break
               finally:yield 'nested cleanup'
              yield 'tail'
            g=generate()
            print(next(g),next(g),next(g))
            try:next(g)
            except StopIteration as e:print(e.value)
            """, "ready nested cleanup tail\n7\n");
    }

    [Fact]
    public async Task ContinueInsideCleanupPreservesOuterException()
    {
        await AssertOutput("""
            def generate():
             try:yield 'ready'
             finally:
              for i in range(2):
               try:continue
               finally:yield i
              yield 'tail'
            g=generate()
            print(next(g),g.throw(ValueError('outer')),next(g),next(g))
            try:next(g)
            except ValueError as e:print(str(e))
            """, "ready 0 1 tail\nouter\n");
    }

    [Fact]
    public async Task BreakOutOfCleanupOverridesThePendingException()
    {
        await AssertOutput("""
            def generate():
             for i in range(2):
              try:yield 'ready'
              finally:break
             yield 'tail'
            g=generate()
            print(next(g),g.throw(ValueError('discarded')))
            print(list(g))
            """, "ready tail\n[]\n");
    }

    [Fact]
    public async Task DelayedNestedWithCleanupPreservesTheOuterClose()
    {
        var compiled = new LythonEngine().Compile("""
            from pathlib import Path
            class Manager:
             def __enter__(self):Path('/data.txt').read_text()
             def __exit__(self,t,v,b):
              Path('/exit.txt').write_text(str(t is None))
              return True
            def generate():
             try:yield 'ready'
             finally:
              with Manager():pass
              Path('/closed.txt').write_text('tail')
            g=generate()
            print(next(g))
            g.close()
            print(list(g))
            """);
        Assert.True(compiled.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/data.txt", "!");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ready\n[]\n", result.StandardOutput);
        Assert.Equal("True", host.ReadText("/exit.txt"));
        Assert.Equal("tail", host.ReadText("/closed.txt"));
        Assert.True(host.CompletedAsynchronously >= 3);
    }

    [Theory]
    [InlineData("call")]
    [InlineData("function")]
    [InlineData("target")]
    public async Task CancellationInPreparedOperandsLeavesCompiledScriptReusable(string kind)
    {
        var source = "from pathlib import Path\n" + (kind switch
        {
            "call" => "def run(*args,**options):return args,options\ndef generate():\n yield run(0,*(yield 'items'),tail=Path('/data.txt').read_text())\ng=generate()\nprint(next(g),g.send([1]))\n",
            "function" => "def generate():\n def run(value=(yield 'default'),*,tail=Path('/data.txt').read_text()):return value,tail\n yield run()\ng=generate()\nprint(next(g),g.send(1))\n",
            _ => "class Box:\n @property\n def value(self):return int(Path('/data.txt').read_text())\n @value.setter\n def value(self,value):Path('/stored.txt').write_text(str(value))\nbox=Box()\ndef generate():\n (yield 'receiver').value+=(yield 'rhs')\n yield 'done'\ng=generate()\nprint(next(g),g.send(box),g.send(2))\n",
        });
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedFile("/data.txt", "1");
        using var cancellation = new CancellationTokenSource();
        var entered = host.PauseReadUntilCancellation("/data.txt");
        var running = compiled.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await entered.WaitAsync(TimeSpan.FromSeconds(20));
        cancellation.Cancel();
        var canceled = await running.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(canceled.Success);
        Assert.Equal("RuntimeError", canceled.Failure?.ExceptionType);
        Assert.Contains("execution canceled", canceled.Failure?.Message, StringComparison.Ordinal);
        var immediate = new MockLythonHost();
        immediate.SeedFile("/data.txt", "1");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/data.txt", "1");
        var rerun = await compiled.RunAsync(fresh);
        Assert.True(rerun.Success, rerun.Failure?.Message);
        Assert.Equal(sync.StandardOutput, rerun.StandardOutput);
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
