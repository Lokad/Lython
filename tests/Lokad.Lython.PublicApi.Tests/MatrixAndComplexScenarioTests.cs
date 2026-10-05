using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MatrixAndComplexScenarioTests
{
    [Theory]
    [InlineData("print(M()@1)")]
    [InlineData("x=M()\nx @= 1\nprint(x)")]
    [InlineData("print(matmul(M(),1),imatmul(M(),1))")]
    public async Task MatrixProtocolsAwaitHost(string operation)
    {
        var script = Compile("from operator import matmul,imatmul\nclass M:\n    def __matmul__(self,x):\n        with open('/data.txt') as f:return f.read()\n    def __imatmul__(self,x):return NotImplemented\n" + operation + "\n");
        await AssertHostParity(script);
    }

    [Fact]
    public async Task MatrixAugmentedTargetReadsAndStoresOnceAndAwaitsBoth()
    {
        var script = Compile("""
            events=[]
            class M:
                def __matmul__(self,x):return x
            class Box:
                def __getitem__(self,index):
                    with open('/data.txt') as f:events.append('get'+f.read())
                    return M()
                def __setitem__(self,index,value):
                    with open('/data.txt') as f:events.append('set'+f.read())
            box=Box()
            def receiver():events.append('receiver');return box
            def index():events.append('index');return 0
            receiver()[index()] @= 7
            print(events)
            """);
        var result = await AssertHostParity(script);
        Assert.Equal("['receiver', 'index', 'get!', 'set!']\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("__complex__", "return 1+2j", "(1+2j)\n")]
    [InlineData("__float__", "return 3.0", "(3+0j)\n")]
    [InlineData("__index__", "return 4", "(4+0j)\n")]
    public async Task ComplexConversionHooksAwaitHost(string hook, string returned, string expected)
    {
        var script = Compile("class C:\n    def " + hook + "(self):\n        with open('/data.txt') as f:f.read()\n        " + returned + "\nprint(complex(C()))\n");
        var result = await AssertHostParity(script);
        Assert.Equal(expected, result.StandardOutput);
    }

    [Theory]
    [InlineData("kept=[]\nfor i in range(10000):kept.append(complex(i,1))\n")]
    [InlineData("kept=[]\nz=1j\nfor i in range(10000):kept.append(z.conjugate)\n")]
    [InlineData("print(format(1j,'.1000000f'))\n")]
    public async Task ComplexValuesMethodsAndFormattingRespectMemoryLimits(string source)
    {
        var script = Compile(source);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success); Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal("", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ComplexValuesProjectAcrossPublicBoundary()
    {
        var script = Compile("return 3+4j\n");
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new Complex(3,4), Assert.IsType<Complex>(result.ReturnValue));
        }
    }

    private static async Task<LythonExecutionResult> AssertHostParity(LythonCompiledScript script)
    {
        var host = new MockLythonHost(); host.SeedFile("/data.txt", "!");
        var sync = script.Run(host); Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/data.txt", "!");
        var result = await script.RunAsync(delayed); Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput); Assert.True(delayed.CompletedAsynchronously > 0);
        return result;
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        return script;
    }
}
