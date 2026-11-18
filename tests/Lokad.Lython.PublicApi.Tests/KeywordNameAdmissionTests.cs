using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class KeywordNameAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeExpandedNamesAreAdmittedBeforeDecodingAndBeforeMappingLookup(bool customMapping)
    {
        var mapping = customMapping
            ? "class Mapping:\n def keys(self):return [name]\n def __getitem__(self,key):print('lookup');return 0\nargs=Mapping()"
            : "args={name:0}";
        using var script = new LythonEngine().Compile("import time\nname='x'*262144\n" + mapping +
            "\ntime.time()\ntry:dict(**args)\nfinally:return 0");
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
    public async Task ExpandedGuestNameCanBindWithoutFundingADecodedCopy()
    {
        using var script = new LythonEngine().Compile("""
            import time
            key = 'x' * 262144
            supplied = {key: 42}

            def target(**kwargs):
                return next(iter(kwargs)) is key and kwargs[key] == 42

            time.time()
            return target(**supplied)
            """);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32 * 1024);
            using (var result = asynchronous
                       ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(true, result.ReturnValue);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Fact]
    public async Task RefusalWhileMergingKeywordSourcesStopsBeforeReadingLaterItems()
    {
        using var script = new LythonEngine().Compile("""
            import time
            class Mapping:
                def keys(self):
                    time.time()
                    return ('later',)
                def __getitem__(self, key):
                    print('lookup')
                    return 0

            def target(**kwargs):
                print('called')

            prefix = {str(i): i for i in range(1024)}
            try:
                target(**prefix, **Mapping())
            finally:
                return 0
            """);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 8 * 1024);
            using (var result = asynchronous
                       ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
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
