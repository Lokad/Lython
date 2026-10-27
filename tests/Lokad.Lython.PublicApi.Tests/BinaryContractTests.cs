using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BinaryContractTests
{
    [Fact]
    public async Task UnicodeNumericSlotsValidateBeforeReplacingTheirValue()
    {
        // Maintained CPython 3.13 structmember.c validates ssize fields before
        // storing. CPython 3.13.2's old failed-setter sentinel is not a contract.
        using var script=new LythonEngine().Compile("try:b'\\xff'.decode('ascii')\nexcept UnicodeDecodeError as error:\n for value in [1.5,1<<100,None]:\n  try:error.start=value\n  except (TypeError,OverflowError):pass\n  print(error.start,error.end)");
        foreach(var asynchronous in new[]{false,true})
        {
            using var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost());
            Assert.True(result.Success,result.Failure?.Message);
            Assert.Equal("0 1\n0 1\n0 1\n",result.StandardOutput);
        }
    }

    [Fact]
    public async Task UnprovidedErrorHandlersFailExplicitlyWithoutARegistry()
    {
        using var script=new LythonEngine().Compile("for name in ['STRICT','bogus','surrogateescape']:\n try:b'a'.decode('ascii',name)\n except Exception as error:print(type(error).__name__)");
        Assert.True(script.IsValid);
        foreach(var asynchronous in new[]{false,true})
        {
            using var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost());
            Assert.True(result.Success,result.Failure?.Message);
            Assert.Equal("LookupError\nLookupError\nNotImplementedError\n",result.StandardOutput);
        }
    }

    public static IEnumerable<object[]> HostedCases()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","BinaryContracts","hosted.json")));
        foreach(var item in document.RootElement.EnumerateArray())
            yield return [item.GetProperty("name").GetString()!,item.GetProperty("source").GetString()!,item.GetProperty("output").GetString()!];
    }

    [Theory,MemberData(nameof(HostedCases))]
    public async Task ConversionHooksAwaitTheActiveHost(string name,string source,string expected)
    {
        var budget=new LythonMemoryBudget(2*1024*1024);
        using(var script=new LythonEngine().Compile(source,budget))
        {
            Assert.True(script.IsValid,name);
            var baseline=budget.ReservedBytes;
            foreach(var mode in new[]{"sync","async-immediate","async-delayed"})
            {
                var immediate=new MockLythonHost("/repo");immediate.SeedFile("/repo/input.txt","ok");
                var delayed=new DelayedLythonHost("/repo");delayed.SeedFile("/repo/input.txt","ok");
                using(var result=mode=="sync"?script.Run(immediate):await script.RunAsync(mode=="async-delayed"?delayed:immediate))
                {
                    Assert.True(result.Success,name+": "+result.Failure?.Message);
                    Assert.Equal(expected,result.StandardOutput);
                    if(mode=="async-delayed") Assert.True(delayed.CompletedAsynchronously>0);
                }
                Assert.Equal(baseline,budget.ReservedBytes);
            }
        }
        Assert.Equal(0,budget.ReservedBytes);
    }

    public static IEnumerable<object[]> Cases()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","BinaryContracts","cases.json")));
        foreach(var item in document.RootElement.EnumerateArray())
            yield return [item.GetProperty("name").GetString()!,item.GetProperty("source").GetString()!,item.GetProperty("output").GetString()!];
    }

    [Theory,MemberData(nameof(Cases))]
    public async Task NativeByteAndCodecContractsMatchIndependentCpython(string name,string source,string expected)
    {
        var budget=new LythonMemoryBudget(4*1024*1024);
        using(var script=new LythonEngine().Compile(source,budget))
        {
            Assert.True(script.IsValid,name+": "+string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
            var baseline=budget.ReservedBytes;
            foreach(var asynchronous in new[]{false,true})
            {
                using(var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success,name+": "+result.Failure?.Message);
                    Assert.Equal(expected,result.StandardOutput);
                    Assert.Empty(result.StandardError);
                }
                Assert.Equal(baseline,budget.ReservedBytes);
            }
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
}
