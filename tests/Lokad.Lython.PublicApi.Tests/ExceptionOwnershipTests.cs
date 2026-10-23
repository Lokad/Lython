namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class ExceptionOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StringArgumentIsRetainedWithoutAClrMessageMirror(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import time\npayload='x'*131072\ntime.time()\nerror=ValueError(payload)\nassert error.args[0] is payload\ntime.time()");
        Assert.True(script.IsValid);
        for (var pass = 0; pass < 3; pass++)
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.InRange(host.AtSecondGate - budget.AtGate, 1, 32768);
#if !DEBUG
                // A UTF-16 copy would exceed 256 KB. Leave ample headroom for
                // unrelated optimized test-host traffic; never assert exact bytes.
                if (pass == 2) Assert.InRange(host.AllocatedAtSecondGate - host.AllocatedAtGate, 0, 65536);
#endif
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnhandledDiagnosticConversionRefusesBeforeAllocatingTheClrCopy(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import time\npayload='x'*131072\ntime.time()\nraise ValueError(payload)");
        Assert.True(script.IsValid);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 32768);
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.True(result.DeniedReservationBytes > 131072);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DictionaryReferenceReplacementReusesFundedStorage(bool asynchronous, bool self)
    {
        var setup = self ? "error.__dict__.update(replacement)\nreplacement=error.__dict__\n" : "";
        using var script = new LythonEngine().Compile("import time\nerror=ValueError()\nreplacement={i:None for i in range(2048)}\n" + setup +
            "old=error.__dict__\ntime.time()\nerror.__dict__=replacement\nassert error.__dict__ is replacement\nassert len(old)==" + (self ? "2048" : "0") + "\ntime.time()");
        Assert.True(script.IsValid);
        var budget = new AdmissionGateBudget();
        using (var result = asynchronous ? await script.RunAsync(new AdmissionGateHost(budget, 32768), new LythonRunOptions { MemoryBudget = budget }) : script.Run(new AdmissionGateHost(budget, 32768), new LythonRunOptions { MemoryBudget = budget }))
            Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedReprReleasesOnlyItsPrivateScratch(bool asynchronous)
    {
        async Task<long> Measure(bool wrapped)
        {
            using var script = new LythonEngine().Compile("import time\nclass Leaf:\n def __repr__(self):raise ValueError('bad')\nleaf=Leaf();error=ValueError(leaf)\ntime.time()\nfor i in range(2):\n try:repr(" + (wrapped ? "error" : "leaf") + ")\n except ValueError:pass\ntime.time()");
            Assert.True(script.IsValid);
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768);
            long held;
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }) : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                held = host.AtSecondGate - budget.AtGate;
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
            return held;
        }
        Assert.Equal(await Measure(false), await Measure(true));
    }
}
