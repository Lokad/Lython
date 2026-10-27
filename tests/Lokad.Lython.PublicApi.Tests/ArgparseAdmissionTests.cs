using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class ArgparseAdmissionTests
{
    [Theory]
    [InlineData("description")]
    [InlineData("usage")]
    [InlineData("epilog")]
    [InlineData("prog")]
    public async Task ParserOptionsBorrowTheirAdmittedText(string option)
    {
        using var script = new LythonEngine().Compile("import argparse,time\ntext='x'*262144\ntime.time()\np=argparse.ArgumentParser(add_help=False," + option + "=text)\ntime.time()\nreturn 0");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget(); var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.InRange(host.AtSecondGate - budget.AtGate, 1L, 32767L);
                Assert.InRange(host.AllocatedAtSecondGate - host.AllocatedAtGate, 0L, 262143L);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("n=argparse.Namespace(**{'x'*262144:0})", "repr(n)")]
    [InlineData("p=argparse.ArgumentParser(add_help=False,description='x'*262144)", "p.format_help()")]
    [InlineData("p=argparse.ArgumentParser(add_help=False,description='x'*262144)", "p.print_help()")]
    public async Task FormattingRefusesBeforeBuildingLargeUnfundedOutput(string setup, string expression)
    {
        using var script = new LythonEngine().Compile("import argparse,time\n" + setup + "\ntime.time()\ntry:value=" + expression + "\nfinally:return 0");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget(); var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                Assert.InRange(GC.GetTotalAllocatedBytes(true) - host.AllocatedAtGate, 0L, 262143L);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task FormattingResultsRemainChargedAfterExecution()
    {
        using var script = new LythonEngine().Compile("import argparse\np=argparse.ArgumentParser(add_help=False,description='x'*65536)\nreturn p.format_help()");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Contains(new string('x', 65536), Assert.IsType<string>(result.ReturnValue));
                Assert.True(budget.ReservedBytes >= 65536);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
