using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LocalImportLifecycleTests
{
    public static IEnumerable<object[]> Programs()
    {
        yield return ["failed-import-retry", """
import sys
try: import selected
except ValueError: print('selected' in sys.modules, sys.modules['held'].__spec__._initializing)
import selected
print(selected.value, selected.__spec__._initializing)
""", """
import sys
attempt = sys.modules.get('attempts', 0) + 1
sys.modules['attempts'] = attempt
sys.modules['held'] = sys.modules[__name__]
if attempt == 1: raise ValueError('retry')
value = 7
""", "False False\n7 False\n"];
        yield return ["replacement-then-failure", """
import sys
try: import selected
except ValueError: print('selected' in sys.modules, sys.modules['held'].value())
""", """
import sys
number = 7
def value(): return number
sys.modules['held'] = sys.modules[__name__]
class Replacement: value = 9
sys.modules[__name__] = Replacement()
raise ValueError('failed')
""", "False 7\n"];
        yield return ["deleted-on-success", """
import sys
try: import selected
except KeyError: print('missing', 'selected' in sys.modules)
""", "import sys\ndel sys.modules[__name__]", "missing False\n"];
        yield return ["loaded-spec-identity", """
import selected, importlib.util
print(selected.__package__ == '', selected.__loader__ is selected.__spec__.loader)
print(importlib.util.find_spec('selected') is selected.__spec__)
print(selected.__spec__.origin == selected.__file__, selected.__spec__.parent == '')
selected.__spec__ = None
try: importlib.util.find_spec('selected')
except ValueError: print('none spec')
""", "import sys\nprint(sys.modules[__name__].__spec__._initializing)", "True\nTrue True\nTrue\nTrue True\nnone spec\n"];
        yield return ["partial-cycle-missing-member", """
import sys
try: import selected
except ImportError: print('selected' in sys.modules)
""", "from selected import missing", "False\n"];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task RegistryAndMetadataFollowTheActualModule(string name, string source, string module, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var mock = new MockLythonHost(); mock.SeedFile("/selected.py", module);
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/selected.py", module);
                var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "selected" } };
                using (var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(mock, options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PackageSearchLocationsAreLiveAndKeepPathAuthorization(bool asynchronous)
    {
        const string source = """
import pkg, importlib.util
print(pkg.__path__ is pkg.__spec__.submodule_search_locations)
pkg.__path__.append('/extra')
print(importlib.util.find_spec('pkg.extra').origin == '/extra/extra.py')
import pkg.extra
print(pkg.extra.value)
pkg.__path__ = ('/other',)
import pkg.alt
print(pkg.alt.value, pkg.__spec__.submodule_search_locations == ['/pkg', '/extra'])
try: import pkg.denied
except ModuleNotFoundError: print('grant checked')
pkg.__path__ = []
try: import pkg.original
except ModuleNotFoundError: print('no fallback')
pkg.__path__ = iter(['/pkg'])
try: import pkg.original
except NotImplementedError: print('unsupported path iterable')
""";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(1024 * 1024);
        var mock = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        foreach (var host in new ILythonHost[] { mock, delayed })
        {
            foreach (var directory in new[] { "/pkg", "/extra", "/other" })
                await host.MkDirAsync(directory, default);
            await FixtureFiles.WriteAsync(host,"/pkg/__init__.py", "pass"u8.ToArray(), default);
            await FixtureFiles.WriteAsync(host,"/pkg/original.py", "raise AssertionError('ignored path')"u8.ToArray(), default);
            await FixtureFiles.WriteAsync(host,"/extra/extra.py", "value=7"u8.ToArray(), default);
            await FixtureFiles.WriteAsync(host,"/other/alt.py", "value=8"u8.ToArray(), default);
            await FixtureFiles.WriteAsync(host,"/other/denied.py", "raise AssertionError('unauthorized')"u8.ToArray(), default);
        }
        var options = new LythonRunOptions
        {
            MemoryBudget = budget,
            AllowedLocalModules = new HashSet<string> { "/pkg/__init__.py", "/pkg/original.py", "/extra/extra.py", "/other/alt.py" },
        };
        using (var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(mock, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True\nTrue\n7\n8 True\ngrant checked\nno fallback\nunsupported path iterable\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task SelectedPackagePathStaysFundedDuringPendingModuleRead()
    {
        var root = "/" + new string('p', 32768);
        var host = new DelayedLythonHost();
        host.SeedFile("/pkg/__init__.py", "pass");
        host.SeedFile(root + "/child.py", "value=7");
        var entered = host.PauseReadUntilCancellation(root + "/child.py");
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        long before = 0;
        host.OnStandardOutputWrite = () => before = budget.ReservedBytes;
        using var script = new LythonEngine().Compile("import pkg\npkg.__path__=[location]\nprint('ready')\nimport pkg.child");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions
        {
            MemoryBudget = budget,
            Globals = new Dictionary<string, object?> { ["location"] = root },
            AllowedLocalModules = new HashSet<string> { "pkg", "pkg.child" },
            CancellationToken = cancellation.Token,
        });
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            // The selected CLR path stays live after candidate-construction
            // scratch (including transient UTF-16 decoding) has been released.
            Assert.True(before > 0);
            Assert.True(budget.ReservedBytes - before >= 2L * root.Length);
        }
        finally
        {
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpecLookupDoesNotBypassBuiltinGrantsThroughRegistryAliases(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import sys, importlib.util, selected\nsys.modules['json']=selected\nprint(importlib.util.find_spec('json') is None)");
        var mock = new MockLythonHost(); mock.SeedFile("/selected.py", "value=7");
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/selected.py", "value=7");
        var options = new LythonRunOptions
        {
            AllowedBuiltinModules = new HashSet<string> { "sys", "importlib", "importlib.util" },
            AllowedLocalModules = new HashSet<string> { "selected" },
        };
        using var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(mock, options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PackagePreferenceDoesNotBypassPathGrants(bool asynchronous)
    {
        var mock = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        foreach (var host in new ILythonHost[] { mock, delayed })
        {
            await host.MkDirAsync("/selected", default);
            await FixtureFiles.WriteAsync(host,"/selected.py", "value=1"u8.ToArray(), default);
            await FixtureFiles.WriteAsync(host,"/selected/__init__.py", "value=2"u8.ToArray(), default);
        }
        using var script = new LythonEngine().Compile("import selected\nprint(selected.value,hasattr(selected,'__path__'))");
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "/selected.py" } };
        using var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(mock, options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1 False\n", result.StandardOutput);
    }
}
