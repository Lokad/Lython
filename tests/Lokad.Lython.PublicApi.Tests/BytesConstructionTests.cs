using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BytesConstructionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IdentityAndTypeProtocolsMatchPython(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            original=b'abcdef'
            print(bytes(original) is original)
            class C:
                def __bytes__(self): return b'ab'
                def __index__(self): raise ValueError('wrong route')
            print(bytes(C()), int.from_bytes(C()))
            class I:
                def __index__(self): return 2
            i=I()
            i.__index__=lambda:3
            print(len(bytes(i)))
            class Bad:
                def __bytes__(self): return 'wrong'
                def __index__(self): print('unreachable'); return 1
            try: bytes(Bad())
            except TypeError as error: print(str(error))
            class Fallback:
                def __index__(self): raise TypeError('fall back')
                def __iter__(self): return iter([65,66])
            print(bytes(Fallback()))
            print(b'a,b,,c'.rsplit(b',',2))
            print(b'aaaaa'.rsplit(b'aa'), b'abc'.rsplit(b',',0))
            print(bytes.fromhex('00 ff\t61'))
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True\nb'ab' 24930\n2\n__bytes__ returned non-bytes (type str)\nb'AB'\n[b'a,b', b'', b'c']\n[b'a', b'', b''] [b'abc']\nb'\\x00\\xffa'\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("bytes(parts())", "b'AB'")]
    [InlineData("bytes(Container())", "b'AB'")]
    [InlineData("bytes(Indexed())", "b'\\x00\\x00'")]
    [InlineData("bytes(AsBytes())", "b'ab'")]
    [InlineData("bytes([Indexed()])", "b'\\x02'")]
    [InlineData("int.from_bytes(parts())", "16706")]
    [InlineData("int.from_bytes(AsBytes())", "24930")]
    [InlineData("(1).from_bytes(Container())", "16706")]
    [InlineData("bool.from_bytes(parts())", "True")]
    public async Task AsyncConversionsAwaitUserProtocols(string expression, string expected)
    {
        foreach (var cancel in new[] { false, true })
        {
            using var script = new LythonEngine().Compile("""
                import time
                def wait(value):
                    try:
                        time.sleep(1)
                        return value
                    finally: print('closed')
                def parts():
                    yield 65
                    yield wait(66)
                class Container:
                    def __iter__(self): return parts()
                class Indexed:
                    def __index__(self): return wait(2)
                class AsBytes:
                    def __bytes__(self): return wait(b'ab')
                print(EXPRESSION)
                """.Replace("EXPRESSION", expression));
            Assert.True(script.IsValid);
            var budget = new LythonMemoryBudget(524288);
            var timing = new HeldTiming(budget);
            var host = new MockLythonHost();
            host.SetTiming(timing);
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            try
            {
                await timing.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                if (cancel) cancellation.Cancel();
                Assert.True(budget.ReservedBytes > 0);
                Assert.False(pending.IsCompleted);
                timing.Release.TrySetResult();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("closed\n" + expected + "\n", result.StandardOutput);
                }
                Assert.Equal(1, timing.Calls);
                Assert.True(timing.FundedUntilCompletion);
            }
            finally
            {
                timing.Release.TrySetResult();
                using var completed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IndependentBytesIOCopyFitsWithoutDuplicateAdmission(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import io\ns=io.BytesIO(b'" + new string('x', 12000) + "')\na=s.getvalue()\ns.close()\nreturn len(a)");
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(12000), result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RefusalStopsIterationBeforeTailEffectsAndGuestHandlers(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            def values():
                for i in range(32768): yield 65
                print('unreachable tail')
            try: bytes(values())
            except Exception: print('unreachable handler')
            print('unreachable continuation')
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConstructedResultsShareFundingUntilBothResultsEnd(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(524288);
        using var script = new LythonEngine().Compile("""
            import io,datetime
            a=bytes(range(16))
            b=bytes.fromhex('abcd')
            c=b'a,b'.rsplit(b',')
            with io.BytesIO(b'payload') as source: d=source.getvalue()
            return [a,b,c,d,datetime.date(2024,1,2).strftime('%Y'),bytes(a) is a]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var first = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        using var second = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(first.ReturnValue);
        Assert.Equal(Enumerable.Range(0,16).Select(static x => (byte)x), Assert.IsType<byte[]>(values[0]));
        Assert.Equal(new byte[] { 0xab,0xcd }, values[1]);
        Assert.Equal("2024", values[4]);
        Assert.Equal(true, values[5]);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class HeldTiming(LythonMemoryBudget budget) : ILythonTiming
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        internal bool FundedUntilCompletion;
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task; }
            finally { FundedUntilCompletion = budget.ReservedBytes > 0; }
        }
    }
}
