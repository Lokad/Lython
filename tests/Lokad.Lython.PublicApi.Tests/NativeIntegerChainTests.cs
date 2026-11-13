using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeIntegerChainTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def f(a,b,c): return (a*b+5)//c\nfor a in [-9,-1,0,1,9]:\n for b in [-7,7]:\n  for c in [-3,3]:\n   assert f(a,b,c)==(a*b+5)//c\nprint('ok')", "ok\n" },
        { "def f(a,b,c): return (a-b)%c\nfor a in [-9223372036854775808,-9,0,9,9223372036854775807]:\n for b in [-1,0,1]:\n  for c in [-7,-1,1,7]:\n   assert f(a,b,c)==(a-b)%c\nprint('ok')", "ok\n" },
        { "def f(a,b): return 100-(a*b)\nprint(f(-9,7),f(9223372036854775807,2),f(True,3))", "163 -18446744073709551514 97\n" },
        { "def f(a,b):\n if False: missing=1\n return (a//b)+missing\ntry:\n f(2,0)\nexcept ZeroDivisionError:\n print('zero first')\ntry:\n f(2,1)\nexcept UnboundLocalError:\n print('local later')", "zero first\nlocal later\n" },
        { "def f(a,b):\n result=(a*b)+1\n alias=result\n return result is alias,result\nprint(f(50000,50000))", "(True, 2500000001)\n" },
        { "class Value:\n def __mul__(self,other):\n  print('multiply')\n  return 11\ndef f(a,b): return (a*b)+7\nprint(f(Value(),3))", "multiply\n18\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ArithmeticKeepsExactValuesProtocolsAndErrorOrder(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CompletedArithmeticTransfersOnceThroughActualIo()
    {
        const string source = """
            def tail():
                with open('/input.txt') as stream:
                    return int(stream.read())
            def compute(a,b):
                result=(a*b+5000)//10000
                return result+tail()
            return compute(12345,6789)
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/input.txt", "19");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new System.Numerics.BigInteger(8400), Assert.IsType<System.Numerics.BigInteger>(result.ReturnValue));
                Assert.True(host.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
