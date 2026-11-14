using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinScalarPublicationTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["integer-boundaries-and-aliases", """
            def compute(text):
                number=int(text)
                alias=number
                total=number+3
                return alias,total,alias is number
            for text in ['-9223372036854775808','9223372036854775807','9223372036854775808',' ١_٢ ','+003']:
                print(compute(text))
            """, "(-9223372036854775808, -9223372036854775805, True)\n(9223372036854775807, 9223372036854775810, True)\n(9223372036854775808, 9223372036854775811, True)\n(12, 15, True)\n(3, 6, True)\n"];
        yield return ["text-bytes-and-length-intermediates", """
            def compute(text,values):
                number=int(text)
                length=len(values)
                total=(number+length)*2
                return total,length
            print(compute(b'1_2','a😀b'))
            print(compute('-3',[1,2]))
            print(compute('0',range(20)))
            def number(text):return int(text)
            def length(value):return len(value)
            print(number('42'),length('😀ab'),number(b'-2'))
            """, "(30, 3)\n(-2, 2)\n(40, 20)\n42 3 -2\n"];
        yield return ["working-values-survive-conversion-errors", """
            def compute(text):
                prior=len(text)+10
                try: value=int(text)
                except ValueError: return prior,'bad'
                return prior,value+1
            for text in ['12','x','1__2','1\0x']:print(compute(text))
            """, "(12, 13)\n(11, 'bad')\n(14, 'bad')\n(13, 'bad')\n"];
        yield return ["live-builtin-rebinding", """
            saved_int=int
            saved_len=len
            def compute(text):return int(text)+len(text)
            print(compute('12'))
            def replacement_int(text):return 7
            def replacement_len(text):return 9
            int=replacement_int
            len=replacement_len
            print(compute('12'))
            int=saved_int
            len=saved_len
            print(compute('12'))
            """, "14\n16\n14\n"];
        yield return ["protocols-and-error-order", """
            events=[]
            class Number:
                def __int__(self):
                    events.append('int')
                    return 7
            class Sized:
                def __len__(self):
                    events.append('len')
                    return 4
            def compute(x,y):
                first=int(x)
                second=len(y)
                return first+second
            print(compute(Number(),Sized()),events)
            try:compute('bad',Sized())
            except ValueError:print(events)
            """, "11 ['int', 'len']\n['int', 'len']\n"];
        yield return ["larger-bound-working-storage", """
            def compute(p0,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14,p15,p16,p17):
                first=int(p0)
                second=len(p1)
                total=first+second+p17
                return first,second,total
            print(compute(*(['12','😀ab']+[0]*15+[5])))
            """, "(12, 3, 20)\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task BuiltinResultsPreservePythonPublication(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string Reading = """
        def read_value():
            with open('input.txt') as source:return int(source.read())
        def compute(text,values):
            number=int(text)
            length=len(values)
            total=number+length
            extra=read_value()
            return number,length,total,total+extra
        return compute('9223372036854775800',[1,2,3])
        """;

    [Fact]
    public async Task ResultsAndWorkingValuesRemainFundedAcrossExecutionsAndSuspension()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Reading, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/input.txt", "7");
            using (var first = script.Run(immediate))
            {
                AssertValues(first);
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/input.txt", "7");
                using (var second = await script.RunAsync(delayed)) AssertValues(second);
                Assert.Equal(1, delayed.ClosedFileCount);
                Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes >= retained);
                AssertValues(first);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationAfterConversionJoinsIoBeforeRefundingAllowance()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Reading, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = host.PauseReadUntilCancellation("/input.txt");
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal(1, host.ClosedFileCount);
            }
            finally
            {
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RefusedParsingAdmissionCannotBeSuppressed()
    {
        const string source = """
            import time
            text='9'*1024
            def compute(text):
                prior=len(text)+1
                try:return int(text)+prior
                except MemoryError:return 0
            time.time()
            compute(text)
            print('must not continue')
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 0);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    private static void AssertValues(LythonExecutionResult result)
    {
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new object[] { BigInteger.Parse("9223372036854775800"), new BigInteger(3),
            BigInteger.Parse("9223372036854775803"), BigInteger.Parse("9223372036854775810") },
            Assert.IsAssignableFrom<IReadOnlyList<object>>(result.ReturnValue));
    }
}
