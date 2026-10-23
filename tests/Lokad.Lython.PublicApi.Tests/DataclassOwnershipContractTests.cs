using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DataclassOwnershipContractTests
{
    [Theory]
    [InlineData("asdict")]
    [InlineData("astuple")]
    public async Task MajorBackingIsAdmittedBeforeVisitingLeaves(string operation)
    {
        using var script = new LythonEngine().Compile("""
            from dataclasses import dataclass,asdict,astuple
            import time
            class Leaf:
                def __deepcopy__(self,memo):
                    time.time()
                    print('visited')
                    return self
            @dataclass
            class D:x:object
            d=D([Leaf()]*10000)
            time.time()
            """ + "\ntry:" + operation + "(d)\nexcept MemoryError:print('suppressed')");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget(); var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success); Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput); Assert.Equal(0, host.AtSecondGate);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
            using var recovery = new LythonEngine().Compile("return 7");
            using (var result = recovery.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })) Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public void FailedGeneratedReprReleasesPrivateScratch()
    {
        long Measure(bool wrapped)
        {
            var name = "D" + new string('x', 1024);
            var source = "from dataclasses import dataclass\nimport time\nclass Leaf:\n def __repr__(self):raise ValueError('bad')\n@dataclass\nclass " + name + ":\n x:object\nleaf=Leaf();d=" + name + "(leaf)\ntime.time()\nfor i in range(2):\n try:repr(" + (wrapped ? "d" : "leaf") + ")\n except ValueError:pass\ntime.time()";
            using var script = new LythonEngine().Compile(source); Assert.True(script.IsValid);
            var budget = new AdmissionGateBudget(); var host = new AdmissionGateHost(budget, 1024 * 1024);
            long retained;
            using (var result = script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            { Assert.True(result.Success, result.Failure?.Message); retained = host.AtSecondGate - budget.AtGate; }
            Assert.Equal(0, budget.Root.ReservedBytes); return retained;
        }
        Assert.Equal(Measure(false), Measure(true));
    }

    [Fact]
    public async Task ResultFromRetainedFactoryArgumentsRemainsFunded()
    {
        using var script = new LythonEngine().Compile("""
            from dataclasses import dataclass,asdict
            @dataclass
            class D:x:object
            saved=[]
            def factory(pairs):saved.append(pairs);return None
            asdict(D([1]*256),dict_factory=factory)
            return saved
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(512 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget, ResultMemoryBudget = budget }) : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget, ResultMemoryBudget = budget }))
            { Assert.True(result.Success, result.Failure?.Message); Assert.NotNull(result.ReturnValue); Assert.True(budget.ReservedBytes >= 256 * 8); }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
