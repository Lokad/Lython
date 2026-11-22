using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AugmentedIntegerCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["promotion and mixed operands", """
            for x,y in [(127,1),(-128,-1),(2**64-1,1),(-2**64+1,-1),(2**128-1,1)]:
                x += y
                print(x,type(x).__name__)
            x=True
            x += True
            x += 1.25
            print(x)
            x=7
            x -= 2
            x *= 3
            print(x)
            """, "128 int\n-129 int\n18446744073709551616 int\n-18446744073709551616 int\n340282366920938463463374607431768211456 int\n3.25\n15\n"];
        yield return ["rich fallback and order", """
            events=[]
            class Left:
                def __iadd__(self,other):
                    events.append('iadd')
                    return NotImplemented
                def __add__(self,other):
                    events.append('add')
                    return NotImplemented
            class Right:
                def __radd__(self,other):
                    events.append('radd')
                    return 41
            def rhs():
                events.append('rhs')
                return Right()
            value=Left()
            value += rhs()
            value += 1
            print(events,value)
            items=[1]
            alias=items
            items += [2]
            print(items is alias,items)
            """, "['rhs', 'iadd', 'add', 'radd'] 42\nTrue [1, 2]\n"];
        yield return ["local, captured and global operands across a call", """
            def identity(value):return value
            def arguments_rebind_local():
                value=4
                value+=identity(value:=10)
                return value
            def captured():
                value=5
                def replace():
                    nonlocal value
                    value=90
                    return 7
                value+=replace()
                return value
            value=6
            def update_global():
                globals()['value']=100
                return 9
            value+=update_global()
            def private_container():
                value=[3]
                alias=value
                value+=identity([9])
                return value is alias,value
            print(arguments_rebind_local(),captured(),value,private_container())
            """, "14 12 15 (True, [3, 9])\n"];
        yield return ["failure preserves the target", """
            value=17
            old=value
            try:
                value += 'bad'
            except TypeError:
                print(value is old,value)
            """, "True 17\n"];
        yield return ["generator suspension and captured target", """
            def outer():
                total=2**64-1
                def read():return total
                def generate():
                    nonlocal total
                    total += 1
                    yield read()
                    total += -2**64
                    yield read()
                return generate(),read
            gen,read=outer()
            print(next(gen),read())
            print(next(gen),read())
            print(list(gen))
            """, "18446744073709551616 18446744073709551616\n0 0\n[]\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task IntegerAugmentationAndFallbackMatchPython(string name, string source, string expected)
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Fact]
    public async Task UnboundLocalStopsBeforeCallingRightHandSide()
    {
        const string source = """
            def rhs():
                print('called')
                return 1
            def add():
                for value in []:pass
                value+=rhs()
            add()
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var compiled = new LythonEngine().Compile(source, budget))
        {
            Assert.True(compiled.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await compiled.RunAsync(new MockLythonHost())
                    : compiled.Run(new MockLythonHost()))
                {
                    var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
                    Assert.Equal("UnboundLocalError", failure.ExceptionType);
                    Assert.Equal(source.ReplaceLineEndings("\n").IndexOf("value+=rhs()", StringComparison.Ordinal), failure.Span?.Start);
                    Assert.Empty(result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("cancel")]
    [InlineData("refuse")]
    public async Task PrivateLocalWaitsForActualRightHandIo(string outcome)
    {
        const string source = """
            def load():
                with open('/input.txt') as input:
                    value=int(input.read())
                return value if value >= 0 else len('x'*2000000)
            def add():
                number=1000
                alias=number
                number+=load()
                print(number,alias)
            print('before')
            try:add()
            except MemoryError:print('caught')
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var compiled = new LythonEngine().Compile(source, budget))
        {
            Assert.True(compiled.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/input.txt", outcome == "refuse" ? "-1" : "7");
            using var cancellation = new CancellationTokenSource();
            var entered = outcome == "cancel" ? host.PauseReadUntilCancellation("/input.txt") : null;
            var pending = compiled.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    await entered.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }

                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                if (outcome == "complete")
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("before\n1007 1000\n", result.StandardOutput);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled
                        : LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    Assert.Equal("before\n", result.StandardOutput);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            if (entered is null) Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
