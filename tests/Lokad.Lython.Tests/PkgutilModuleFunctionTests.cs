using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PkgutilModuleFunctionTests
{
    [Fact]
    public void PkgutilIterModules_ListsBuiltinAndAllowedLocalModules()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import pkgutil

names = [m.name for m in pkgutil.iter_modules()]
selected = []
for name in ["difflib", "json", "pathlib", "pkgutil", "helper", "subprocess"]:
    selected.append(name + "=" + str(name in names))
write_text("/out.txt", "|".join(selected))
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper.py" }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("difflib=True|json=True|pathlib=True|pkgutil=True|helper=True|subprocess=False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PkgutilIterModules_PreservesModuleInfoAttributesAndUnpacking()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import pkgutil

items = []
for finder, name, ispkg in pkgutil.iter_modules(prefix="x."):
    if name in ["x.json", "x.pkgutil"]:
        items.append(name + ":" + str(finder is None) + ":" + str(ispkg))
write_text("/out.txt", "|".join(sorted(items)))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("x.json:True:False|x.pkgutil:True:False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PkgutilWalkPackages_UsesSameContainedDiscoverySurface()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import pkgutil

names = [name for _, name, _ in pkgutil.walk_packages()]
explicit_path = list(pkgutil.walk_packages(["/repo"]))
write_text("/out.txt", str("json" in names) + "|" + str("pkgutil" in names) + "|" + str(explicit_path))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|[]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PkgutilLoaderLookup_ReflectsAvailableBuiltinLocalAndHostCapabilityModules()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();

        var result = new LythonEngine().Run(
            """
import json
import pkgutil

parts = []
parts.append(str(pkgutil.find_loader("json") is not None))
parts.append(pkgutil.find_loader("json").name)
parts.append(str(pkgutil.find_loader("missing") is None))
parts.append(pkgutil.get_loader(json).fullname)
parts.append(pkgutil.get_loader("helper").name)
parts.append(str(pkgutil.find_loader("subprocess") is not None))
write_text("/out.txt", "|".join(parts))
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|json|True|json|helper|True", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("import pkgutil\npkgutil.iter_modules(None, 1)\n", "prefix")]
    [InlineData("import pkgutil\npkgutil.find_loader()\n", "expects one argument")]
    [InlineData("import pkgutil\npkgutil.get_loader(1)\n", "module name string")]
    public void PkgutilModule_NearMisses_FailPrecisely(string source, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }
}
