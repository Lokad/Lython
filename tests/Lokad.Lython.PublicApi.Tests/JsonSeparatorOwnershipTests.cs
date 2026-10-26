using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class JsonSeparatorOwnershipTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task RetainedOptionsBorrowAlreadyFundedSeparators(bool asynchronous, bool iterators)
    {
        var setup = "import json,time\ns='x'*262144\n";
        var expression = "json.JSONEncoder(separators=(s,s))";
        if (iterators)
        {
            setup += "encoder=json.JSONEncoder(separators=(s,s))\n";
            expression = "encoder.iterencode([])";
        }
        using var script = new LythonEngine().Compile(setup + $"""
            time.time()
            held=[{expression} for i in range(8)]
            time.time()
            return 0
            """);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 32768);
        using (var result = asynchronous
            ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.InRange(host.AllocatedAtSecondGate - host.AllocatedAtGate, 0, 128 * 1024);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SeparatorAliasesAndIteratorSnapshotsRemainIndependent(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import json
            item='\u00e9\U0001f600'
            key='\u03bb'
            e=json.JSONEncoder(separators=(item,key))
            print(e.item_separator is item,e.key_separator is key)
            it=e.iterencode({'x':[1,2]})
            e.item_separator='changed'
            e.key_separator='changed'
            print(ascii(''.join(it)))
            """);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True True\n'{\"x\"\\u03bb[1\\xe9\\U0001f6002]}'\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BorrowedSeparatorOutputIsAdmittedBeforeSubstantialGrowth(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import json,time
            separator='x'*262144
            encoder=json.JSONEncoder(separators=(separator,separator))
            time.time()
            encoder.encode([1,2])
            print('after')
            """);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 32768);
        using (var result = asynchronous
            ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.InRange(GC.GetTotalAllocatedBytes(true) - host.AllocatedAtGate, 0, 128 * 1024);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }
}
