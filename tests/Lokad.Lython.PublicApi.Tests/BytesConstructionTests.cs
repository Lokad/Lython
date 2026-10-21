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
    [InlineData("(1).to_bytes(Indexed())", "b'\\x00\\x01'")]
    [InlineData("int.to_bytes(1,Indexed())", "b'\\x00\\x01'")]
    [InlineData("(-1).to_bytes(2,signed=Signed())", "b'\\xff\\xff'")]
    [InlineData("int.from_bytes(b'\\xff',signed=Signed())", "-1")]
    [InlineData("decimal.DecimalTuple(0,digits(),0)", "DecimalTuple(sign=0, digits=(1, 2), exponent=0)")]
    [InlineData("json.loads('\"a\\tb\"',strict=Relaxed())", "a\tb")]
    [InlineData("json.JSONDecoder(strict=Relaxed()).decode('\"a\\tb\"')", "a\tb")]
    [InlineData("json.loads('\"a\\tb\"',cls=Child,strict=Relaxed())", "a\tb")]
    [InlineData("json.loads('\"a\\tb\"',cls=SuperChild,strict=Relaxed())", "a\tb")]
    [InlineData("0 <= random.getrandbits(Indexed()) < 4", "True")]
    [InlineData("0 <= random.Random(1).getrandbits(Indexed()) < 4", "True")]
    [InlineData("len(random.randbytes(Multiplied()))", "2")]
    [InlineData("len(random.Random(1).randbytes(Multiplied()))", "2")]
    [InlineData("len(hashlib.sha256(usedforsecurity=Relaxed()).digest())", "32")]
    [InlineData("len(hashlib.new('sha256',usedforsecurity=Relaxed()).digest())", "32")]
    [InlineData("len(hashlib.new('sha256',usedforsecurity=ValueFlag()).digest())", "32")]
    [InlineData("checked_hash()", "flag failure")]
    [InlineData("difflib.SequenceMatcher(None,'a','a',autojunk=Relaxed()).ratio()", "1.0")]
    [InlineData("difflib.SequenceMatcher(lambda c:Relaxed(),'a','a').ratio()", "1.0")]
    [InlineData("reindex(False)", "1.0")]
    [InlineData("reindex(True)", "1.0")]
    public async Task AsyncConversionsAwaitUserProtocols(string expression, string expected)
    {
        foreach (var cancel in new[] { false, true })
        {
            using var script = new LythonEngine().Compile("""
                import time,decimal,json,random,hashlib,difflib
                active_hash=hashlib.sha256(b'abc')
                def wait(value):
                    try:
                        time.sleep(1)
                        assert active_hash.hexdigest() == 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad'
                        return value
                    finally: print('closed')
                def parts():
                    yield 65
                    yield wait(66)
                class Container:
                    def __iter__(self): return parts()
                class Indexed:
                    def __index__(self): return wait(2)
                class Multiplied:
                    def __mul__(self,other): return wait(16)
                    def __index__(self): return 2
                class AsBytes:
                    def __bytes__(self): return wait(b'ab')
                class Signed:
                    def __bool__(self): return wait(True)
                class Relaxed:
                    def __bool__(self): return wait(False)
                class ValueFlag:
                    def __bool__(self):
                        wait(False)
                        raise ValueError('flag failure')
                def checked_hash():
                    try: hashlib.sha256(usedforsecurity=ValueFlag())
                    except ValueError as error: return str(error)
                def reindex(both):
                    matcher=difflib.SequenceMatcher(lambda c:Relaxed(),'a','')
                    if both: matcher.set_seqs('a','a')
                    else: matcher.set_seq2('a')
                    return matcher.ratio()
                class Child(json.JSONDecoder): pass
                class SuperChild(json.JSONDecoder):
                    def __init__(self,**kwargs): super().__init__(**kwargs)
                def digits():
                    yield 1
                    yield wait(2)
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
    [InlineData(false, "bytes(values())")] [InlineData(true, "bytes(values())")]
    [InlineData(false, "decimal.DecimalTuple(0,values(),0)")] [InlineData(true, "decimal.DecimalTuple(0,values(),0)")]
    public async Task RefusalStopsIterationBeforeTailEffectsAndGuestHandlers(bool asynchronous, string expression)
    {
        using var script = new LythonEngine().Compile("""
            import decimal
            def values():
                for i in range(32768): yield 1
                print('unreachable tail')
            try: CONSTRUCTION
            except Exception: print('unreachable handler')
            print('unreachable continuation')
            """.Replace("CONSTRUCTION", expression));
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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IntegerByteRangesFlagsAndEmptyDecimalsMatchPython(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import decimal
            for value in [0,1,127,128,255,256,-1,-128,-129,-256]:
                for order in ['big','little']:
                    raw=value.to_bytes(2,order,signed=True)
                    assert int.from_bytes(raw,order,signed=True)==value
            print((-1).to_bytes(0,signed=True),(0).to_bytes(0))
            class Flag:
                def __bool__(self): print('flag'); return False
            print(int.from_bytes(b'\xff',signed=Flag()))
            try: (-1).to_bytes(1,signed=Flag())
            except OverflowError: print('unsigned')
            class Bad:
                def __bool__(self): raise ValueError('flag refused')
            try: (1).to_bytes(1,signed=Bad())
            except ValueError as error: print(error)
            for value,signed in [(1,False),(-1,False),(-2,True),(128,True),(-129,True)]:
                try: value.to_bytes(0 if abs(value)<3 else 1,signed=signed)
                except OverflowError: print('overflow')
            empty=decimal.DecimalTuple(0,(),0)
            print(empty,decimal.Decimal(empty))
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("b'' b''\nflag\n255\nflag\nunsigned\nflag refused\noverflow\noverflow\noverflow\noverflow\noverflow\nDecimalTuple(sign=0, digits=(), exponent=0) 0\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ByteOrderAndSignedValidationPreserveCallbackOrder(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            class Flag:
                def __bool__(self): print('flag'); return False
            for length,order in [(-1,'big'),(1,'wrong'),(-1,'wrong')]:
                try: (1).to_bytes(length,order,signed=Flag())
                except ValueError as error: print(error)
            try: int.from_bytes(b'x','wrong',signed=Flag())
            except ValueError as error: print(error)
            try: (1).to_bytes(1,None,signed=Flag())
            except TypeError: print('type before flag')
            try: (1).to_bytes(2**80,signed=Flag())
            except OverflowError: print('size before flag')
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("flag\nlength argument must be non-negative\n" +
            string.Concat(Enumerable.Repeat("flag\nbyteorder must be either 'little' or 'big'\n", 3)) +
            "type before flag\nsize before flag\n", result.StandardOutput);
    }
}
