using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class DiscoveryAdmissionTests
{
    public static TheoryData<string, string, bool> Operations => new()
    {
        { "absolute-name", "import importlib.util as u,time\nname='x'*262144\ntime.time()\nvalue=u.resolve_name(name,'pkg')\nassert value is name\ntime.time()", true },
        { "relative-package-alias", "import importlib.util as u,time\nname='x'*262144\ntime.time()\nvalue=u.resolve_name('.',name)\nassert value is name\ntime.time()", true },
        { "list-copy", "import pkgutil,time\npaths=['/a']*32768\ntime.time()\nvalue=pkgutil.extend_path(paths,'missing')", false },
        { "spec-output", "import importlib.util as u,time\nclass Loader:\n def __repr__(self):time.time();return 'loader'\nspec=u.find_spec('sys')\nspec.name='x'*262144\nspec.loader=Loader()\nvalue=repr(spec)", false },
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task MajorStorageIsBorrowedOrAdmittedBeforeAllocation(string name, string source, bool success)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 32768, new MockLythonHost());
            using (var result = asynchronous
                ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.Equal(success, result.Success);
                Assert.True(host.AllocatedAtGate > 0);
                if (!success) Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                var allocated = (host.AllocatedAtSecondGate == 0 ? GC.GetTotalAllocatedBytes(true) : host.AllocatedAtSecondGate) - host.AllocatedAtGate;
                Assert.InRange(allocated, 0, 128 * 1024);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("", "pkg", "")]
    [InlineData(".", "pkg..sub", "pkg..sub")]
    [InlineData("..child", "pkg..sub", "pkg..child")]
    [InlineData("..child", "pkg.", "pkg.child")]
    [InlineData(".\u03b2", "\u03b1.\u03b3", "\u03b1.\u03b3.\u03b2")]
    public async Task RelativeNameResolutionPreservesComponents(string name, string package, string expected)
    {
        using var script = new LythonEngine().Compile("import importlib.util\nreturn importlib.util.resolve_name(name,package)");
        var budget = new LythonMemoryBudget(1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["name"] = name, ["package"] = package } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.ReturnValue);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
