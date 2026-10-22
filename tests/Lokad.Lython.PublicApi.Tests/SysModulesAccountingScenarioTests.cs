using Lokad.Lython.Tests.Harness;
namespace Lokad.Lython.PublicApi.Tests;
public sealed class SysModulesAccountingScenarioTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegistryReadsAreStableAndOwnersReturnToBaseline(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import sys,math
            first=sys.modules
            for i in range(2000):
                assert first is sys.modules
            return ['math' in first]
            """);
        var budget = new LythonMemoryBudget(262144);
        for (var i=0;i<2;i++)
        {
            using (var result=asynchronous ? await script.RunAsync(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget})
                : script.Run(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget}))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal(new List<object?> { true },result.ReturnValue);
                Assert.True(budget.ReservedBytes>0);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedObjectsAndNoneBlockersAreAuthoritative(bool asynchronous)
    {
        using var script=new LythonEngine().Compile("""
            import sys,math,importlib
            print('random' in sys.modules)
            del sys.modules['math']
            print('math' in sys.modules)
            import math
            sys.modules['math']=None
            try:import math
            except ModuleNotFoundError:print('blocked')
            class C:value=7
            value=C()
            sys.modules['selected']=value
            import selected
            print(selected is value,selected.value)
            print(importlib.import_module('selected') is value)
            """);
        using var result=asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("False\nFalse\nblocked\nTrue 7\nTrue\n",result.StandardOutput);
    }
}
