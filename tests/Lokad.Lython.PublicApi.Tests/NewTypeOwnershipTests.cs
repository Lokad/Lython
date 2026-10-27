using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NewTypeOwnershipTests
{
    [Fact]
    public async Task MetadataIdentityAndBindingMatchIndependentCpython313()
    {
        const string source = """
            from typing import NewType
            f=[NewType][0]
            U=f(name='scope.User Id',tp=int)
            V=f('V',U)
            value=[]
            print(U.__name__,U.__qualname__,U.__supertype__ is int,V.__supertype__ is U)
            print(U(value) is value,V(value) is value,repr(U),repr(V))
            print(U.__name__ is U.__name__,U.__qualname__ is U.__qualname__)
            for mode in range(8):
                try:
                    if mode==0: f('X',int,3)
                    elif mode==1: f('X',int,extra=3)
                    elif mode==2: f('X',int,name='Y')
                    elif mode==3: f('X')
                    elif mode==4: U()
                    elif mode==5: U(1,2)
                    elif mode==6: U(value=1)
                    else: U(obj=1)
                except TypeError as e: print(type(e).__name__)
            print(f('not an identifier',int).__name__)
            """;
        const string expected = "User Id scope.User Id True True\nTrue True __main__.scope.User Id __main__.V\nTrue True\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nnot an identifier\n";
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, root))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = root.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, root.ReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedNameAndRenderWitnessesRefuseBeforePublishingStorage(bool render)
    {
        var source = "import time\nfrom typing import NewType\n" + (render
            ? "T=NewType('X'*262144,int)\ntime.time()\nr=repr(T)"
            : "name='X'*262144\ntime.time()\nT=NewType(name,int)");
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task SupertypeAliasRemainsFundedUntilResultCleanup()
    {
        const string source = """
            from typing import NewType
            original=['x'*32768]
            U=NewType('U',original)
            assert U.__supertype__ is original
            assert U(original) is original
            return U.__supertype__
            """;
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, root))
        {
            Assert.True(script.IsValid);
            var baseline = root.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new string('x',32768), Assert.Single(Assert.IsType<List<object?>>(result.ReturnValue)));
                    Assert.True(root.ReservedBytes >= baseline + 32768);
                }
                Assert.Equal(baseline, root.ReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
