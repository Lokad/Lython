using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class KeywordNameAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpandedNamesAreAdmittedBeforeDecodingAndBeforeMappingLookup(bool customMapping)
    {
        var mapping = customMapping
            ? "class Mapping:\n def keys(self):return [name]\n def __getitem__(self,key):print('lookup');return 0\nargs=Mapping()"
            : "args={name:0}";
        using var script = new LythonEngine().Compile("import time\nname='x'*262144\n" + mapping +
            "\ndef target(**kw):print('called')\ntime.time()\ntry:target(**args)\nfinally:return 0");
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
                Assert.Empty(result.StandardOutput);
                Assert.InRange(GC.GetTotalAllocatedBytes(true) - host.AllocatedAtGate, 0L, 262143L);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task ReturnedKeywordStorageStaysFundedUntilItsResultIsDisposed()
    {
        using var script = new LythonEngine().Compile("name='x'*65536\ndef target(**kw):return kw\nreturn target(**{name:42})");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var dictionary = Assert.IsAssignableFrom<IDictionary<object, object?>>(result.ReturnValue);
                Assert.Equal(new string('x', 65536), Assert.Single(dictionary).Key);
                Assert.True(budget.ReservedBytes >= 65536);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
