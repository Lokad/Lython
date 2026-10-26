using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DiscoveryUnwindTests
{
    private static readonly (string Name, string Source)[] Operations =
    {
        ("cached-spec", "class Holder:\n @property\n def __spec__(self):read();return 'spec'\nsys.modules['held']=Holder()\nu.find_spec('held')"),
        ("spec-truth", "class Flag:\n def __bool__(self):read();return True\nspec=u.find_spec('sys')\nspec.has_location=Flag()"),
        ("path-iterator", "def paths():\n read()\n yield '/'\nlist(pkgutil.iter_modules(paths()))"),
        ("onerror", "def failed(name):read()\nlist(pkgutil.walk_packages(['/repo'],onerror=failed))"),
        ("resolver-attribute", "class Holder:\n @property\n def value(self):read();return 1\nsys.modules['held']=Holder()\npkgutil.resolve_name('held:value')"),
        ("loader-source", "pkgutil.get_loader('helper').get_source()"),
        ("cold-resolver", "pkgutil.resolve_name('helper:value')"),
        ("cold-spec-parent", "u.find_spec('heldpkg.child')"),
    };
    public static IEnumerable<object[]> Boundaries() => Operations.Select(item => new object[] { item.Name, item.Source });
    public static IEnumerable<object[]> RefusalBoundaries() => Boundaries().Where(item => item[0] is not ("loader-source" or "cold-resolver" or "cold-spec-parent"));

    private const string Prelude = """
        import pkgutil, sys, importlib.util as u
        def read():
            with open('/cleanup','w') as target:
                target.write('done')
                target.flush()
                with open('/value') as source: source.read()
                try:
                    if fail: value='x'*4194304
                finally: return 1
        """;

    private static void Seed(Action<string, string> seed)
    {
        seed("/value", "1");
        seed("/repo/badpkg/__init__.py", "raise ValueError('package')");
        seed("/repo/helper.py", "with open('/value') as source: value=source.read()");
        seed("/repo/heldpkg/__init__.py", "with open('/value') as source: value=source.read()");
        seed("/repo/heldpkg/child.py", "value=1");
    }

    private static LythonRunOptions Options(bool fail, CancellationToken cancellationToken = default) => new()
    {
        CancellationToken = cancellationToken,
        SourcePath = "/repo/main.py",
        AllowedLocalModules = new HashSet<string> { "helper", "badpkg", "heldpkg", "heldpkg.child" },
        Globals = new Dictionary<string, object?> { ["fail"] = fail }
    };

    [Theory, MemberData(nameof(Boundaries))]
    public async Task InFlightCancellationUnwindsAndAllowsReuse(string name, string boundary)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Prelude + "\n" + boundary, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost("/repo"); Seed(host.SeedFile);
        var started = host.PauseReadUntilCancellation(name == "loader-source" ? "/repo/helper.py" : "/value");
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(host, Options(false, cancellation.Token));
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var fresh = new DelayedLythonHost("/repo"); Seed(fresh.SeedFile);
        using (var result = await script.RunAsync(fresh, Options(false))) Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory, MemberData(nameof(RefusalBoundaries))]
    public async Task RefusalCannotBecomeSuccessfulDiscovery(string name, string boundary)
    {
        // Source reads have separate cancellation coverage above. Here the
        // read's finally return and package onerror cannot swallow admission.
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Prelude + "\ntry:\n " + boundary.Replace("\n", "\n ")
            + "\nexcept MemoryError:pass\nprint('later')", budget);
        Assert.True(script.IsValid, name);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new DelayedLythonHost("/repo"); Seed(host.SeedFile);
            var immediate = new MockLythonHost("/repo"); Seed(immediate.SeedFile);
            using (var result = asynchronous ? await script.RunAsync(host, Options(true)) : script.Run(immediate, Options(true)))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal("done", asynchronous ? host.ReadText("/cleanup") : immediate.ReadText("/cleanup"));
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
