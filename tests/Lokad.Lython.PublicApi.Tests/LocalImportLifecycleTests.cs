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
            await host.WriteTextUtf8Async("/selected.py", "value=1"u8.ToArray(), default);
            await host.WriteTextUtf8Async("/selected/__init__.py", "value=2"u8.ToArray(), default);
        }
        using var script = new LythonEngine().Compile("import selected\nprint(selected.value,hasattr(selected,'__path__'))");
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "/selected.py" } };
        using var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(mock, options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1 False\n", result.StandardOutput);
    }
}
