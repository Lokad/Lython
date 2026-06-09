using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PkgutilModuleFunctionTests
{
    [Fact]
    public void PkgutilIterModules_ListsBuiltinAndAllowedLocalModules()
    {
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "value = 1");

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
manual = pkgutil.ModuleInfo(None, "manual", True)
replaced = manual._replace(name="changed", ispkg=False)
checks = [
    str(len(manual)),
    manual[1],
    str(manual.module_finder is None),
    str(manual.ispkg),
    str(manual == pkgutil.ModuleInfo(None, "manual", True)),
    str(manual._fields),
    str(manual._asdict()["name"]),
    str(replaced.name) + ":" + str(replaced.ispkg),
    str(manual.count("manual")),
    str(manual.index("manual")),
    str(repr(manual).startswith("ModuleInfo(")),
]
write_text("/out.txt", "|".join(sorted(items)) + "||" + "|".join(checks))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("x.json:True:False|x.pkgutil:True:False||3|manual|True|True|True|(module_finder, name, ispkg)|manual|changed:False|1|1|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PkgutilWalkPackages_UsesSameContainedDiscoverySurface()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import pkgutil

names = [name for _, name, _ in pkgutil.walk_packages()]
packages = []
for _, name, ispkg in pkgutil.walk_packages():
    if name in ["openpyxl", "openpyxl.reader", "json"]:
        packages.append(name + ":" + str(ispkg))
write_text("/out.txt", str("json" in names) + "|" + str("pkgutil" in names) + "|" + "|".join(sorted(packages)))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|json:False|openpyxl.reader:True|openpyxl:True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PkgutilExplicitPathDiscovery_UsesHostFixturesAndAllowlist()
    {
        var host = new MockLythonHost();
        host.SeedFile("/repo/alpha.py", "value = 'alpha'");
        host.SeedFile("/repo/pkg/__init__.py", "kind = 'package'");
        host.SeedFile("/repo/pkg/sub.py", "value = 'sub'");
        host.SeedFile("/repo/pkg/nested/__init__.py", "kind = 'nested'");
        host.SeedFile("/repo/.hidden.py", "value = 'hidden'");
        host.SeedFile("/repo/not-module.txt", "skip");

        var result = new LythonEngine().Run(
            """
import pkgutil

direct = []
for _, name, ispkg in pkgutil.iter_modules("/repo", "p."):
    direct.append(name + ":" + str(ispkg))
walked = []
for _, name, ispkg in pkgutil.walk_packages(["/repo"], "p."):
    walked.append(name + ":" + str(ispkg))
write_text("/out.txt", "|".join(sorted(direct)) + "||" + "|".join(sorted(walked)))
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal)
                {
                    "alpha",
                    "pkg",
                    "sub",
                    "nested",
                    "/repo/alpha.py",
                    "/repo/pkg/__init__.py",
                    "/repo/pkg/sub.py",
                    "/repo/pkg/nested/__init__.py",
                }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("p.alpha:False|p.pkg:True||p.alpha:False|p.pkg.nested:True|p.pkg.sub:False|p.pkg:True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PkgutilWalkPackages_OnErrorReceivesPackageNameForBlockedNestedPath()
    {
        var host = new MockLythonHost();
        host.SeedFile("/repo/pkg/__init__.py", "kind = 'package'");
        host.FailListDir("/repo/pkg", "blocked package");

        var result = new LythonEngine().Run(
            """
import pkgutil

errors = []
def onerror(name):
    errors.append(name)

items = [name for _, name, _ in pkgutil.walk_packages(["/repo"], "", onerror)]
write_text("/out.txt", ",".join(items) + "|" + ",".join(errors))
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "pkg", "/repo/pkg/__init__.py" }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("pkg|pkg", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PkgutilLoaderLookup_ReflectsAvailableBuiltinLocalAndHostCapabilityModules()
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        host.SeedFile("/helper.py", "value = 42");
        host.SeedFile("/pkg/__init__.py", "kind = 'package'");

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
parts.append(str(pkgutil.get_loader("helper").get_source().startswith("value = 42")))
parts.append(str(pkgutil.get_loader("pkg").is_package()))
parts.append(str(pkgutil.get_loader("pkg").get_source().startswith("kind = ")))
parts.append(str(pkgutil.find_loader("subprocess") is not None))
write_text("/out.txt", "|".join(parts))
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal)
                {
                    "helper",
                    "pkg",
                    "/helper.py",
                    "/pkg/__init__.py",
                }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|json|True|json|helper|True|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PkgutilResolveNameAndExtendPath_HandleCommonAgentUsage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import pkgutil

dumps = pkgutil.resolve_name("json.dumps")
colon = pkgutil.resolve_name("json:dumps")
extended = pkgutil.extend_path(["/a"], "pkg")
write_text("/out.txt", dumps({"ok": True}) + "|" + colon({"ok": False}) + "|" + str(extended))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("{\"ok\":true}|{\"ok\":false}|[/a]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Pkgutil_StaticDiagnosticsCoverExpandedSurface()
    {
        var compiled = new LythonEngine().Compile(
            """
import pkgutil

info = pkgutil.ModuleInfo(None, 1, "no")
pkgutil.iter_modules(1)
pkgutil.iter_modules(None, 1)
pkgutil.walk_packages(None, "", 1)
pkgutil.find_loader(1)
pkgutil.get_loader(1)
pkgutil.extend_path([1], 2)
loader = pkgutil.get_loader("json")
loader.is_package(1)
loader.get_source(1)
info._replace(name=1, ispkg="no")
info.index("x", "bad")
""");

        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("ModuleInfo", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("iter_modules", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("walk_packages", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("find_loader", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("get_loader", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("extend_path", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("loader.is_package", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("loader.get_source", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("_replace", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("ModuleInfo.index", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("import pkgutil\npkgutil.iter_modules(None, 1)\n", "prefix")]
    [InlineData("import pkgutil\npkgutil.find_loader()\n", "expects one argument")]
    [InlineData("import pkgutil\npkgutil.get_loader(1)\n", "module name string")]
    [InlineData("import pkgutil\npkgutil.walk_packages(None, '', 1)\n", "onerror")]
    [InlineData("import pkgutil\npkgutil.iter_modules(1)\n", "path string")]
    [InlineData("import pkgutil\npkgutil.resolve_name('json:')\n", "module:object")]
    [InlineData("import pkgutil\npkgutil.get_data('pkg', 'data.txt')\n", "unsupported by Lython")]
    [InlineData("import pkgutil\npkgutil.ModuleInfo(None, 1, False)\n", "ModuleInfo")]
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
