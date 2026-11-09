using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BooleanIdentityTests
{
    // Independent CPython output, including literals, host inputs and module results.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BooleanResultsKeepPythonIdentityAndNumericSemantics(bool asynchronous)
    {
        const string source = """
import json
positive=[True,host_true,bool(1),1==1,1<2,[1]==[1],any([1]),all([1]),isinstance(1,int),issubclass(bool,int),hasattr([],'append'),callable(len),True|False,json.loads('true')]
negative=[False,host_false,bool(0),1!=1,1>2,[1]==[2],any([]),all([0]),isinstance(1,str),issubclass(int,bool),hasattr([],'missing'),callable(1),True^True,json.loads('false')]
print(all(value is True for value in positive),all(value is False for value in negative))
print(len({id(value) for value in positive})==1,len({id(value) for value in negative})==1)
number=1
print(True is number,id(True)!=id(number),hash(True)==hash(number))
token=[]
class Equal:
 def __eq__(self,other):return token
answer=Equal()==Equal()
print(answer is token,(not answer) is True)
print(type(True|False).__name__,True+True,hash(False))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            MemoryBudget = budget,
            Globals = new Dictionary<string, object?> { ["host_true"] = true, ["host_false"] = false },
        };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True True\nTrue True\nFalse True True\nTrue True\nbool 2 0\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
