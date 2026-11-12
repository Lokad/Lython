using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeReceiverOperationTests
{
    // Independent isolated CPython 3.13 output. Ordinary guarded helpers retain
    // their dynamic cases, while canonical arguments need no guest callbacks.
    private const string Source = """
        def affixes(text,prefix,suffix):
            if not text:return False,False
            return text.startswith(prefix),text.endswith(suffix)
        def parts(text,separator=None,count=-1):
            if not text:return []
            return text.split(separator,count)
        def change(text,old,new,count=-1):
            if not text:return text
            return text.replace(old,new,count)
        print(affixes('é😀end','é','end'))
        print(affixes('ab',('x','a'),('z','b')))
        print(parts('A😀B😀C','😀',1))
        print(parts('α β γ',None,2**40))
        print(change('İΣΣİ','Σ','!',True))
        print(change('ab','a','x',2**40))
        class Count:
            def __index__(self):
                print('index')
                return 1
        print(parts('a,b,c',',',Count()))
        print(change('aaa','a','x',Count()))
        for operation,arguments in [(parts,('a b',None,None)),(parts,('ab','',1)),
                                    (change,('ab','a','x',None)),(affixes,('ab',1,'b')),
                                    (change,('ab',None,'x',Count())),
                                    (parts,('a b',None,2**80)),(change,('ab','a','x',2**80))]:
            try:operation(*arguments)
            except (TypeError,ValueError,OverflowError) as error:print(type(error).__name__)
        """;

    private const string Expected = "(True, True)\n(True, True)\n['A', 'B😀C']\n['α', 'β', 'γ']\nİ!Σİ\nxb\nindex\n['a', 'b,c']\nindex\nxaa\nTypeError\nValueError\nTypeError\nTypeError\nTypeError\nOverflowError\nOverflowError\n";

    [Fact]
    public async Task GuardedTextHelpersPreserveUnicodeDefaultsErrorsAndProtocols()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(Expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task GuardedReplacementRefusalCannotBeSuppressedAndReturnsFunding(bool asynchronous)
    {
        const string source = """
            def change(value):
                if not value:return value
                return value.replace('x','y'*64)
            try:change(payload)
            except MemoryError:pass
            print('after')
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["payload"] = new string('x', 65536) } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

[Collection("RuntimeStress")]
public sealed class NativeReceiverOperationProtocolTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task GuardedCountProtocolStillSuspendsCancelsAndReusesItsBudget(bool split)
    {
        var source = """
            class Count:
                def __index__(self):
                    with open('/count') as reader:return int(reader.read())
            def change(value,count):
                if not value:return value
                return value.replace('a','x',count)
            print(change('aaa',Count()))
            """;
        if (split) source = source.Replace("value.replace('a','x',count)", "value.split('a',count)");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        host.SeedFile("/count", "1");
        var started = host.PauseReadUntilCancellation("/count");
        var run = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            var first = await Task.WhenAny(started, run).WaitAsync(TimeSpan.FromSeconds(10));
            if (first == run)
            {
                using var early = await run;
                Assert.Fail("Count protocol completed before reaching host I/O: " + early.Failure?.Message);
            }
            await started;
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/count", "1");
        using (var result = await script.RunAsync(fresh))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(split ? "['', 'aa']\n" : "xaa\n", result.StandardOutput);
            Assert.True(fresh.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
