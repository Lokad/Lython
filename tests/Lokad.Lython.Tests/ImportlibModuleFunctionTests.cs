using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ImportlibModuleFunctionTests
{
    [Fact]
    public void BuiltinDiscoveryAndDynamicImportStayInsideLythonModuleInventory()
    {
        var result = new LythonEngine().Run(
            """
import importlib
import importlib.util
import json
import sys

json_spec = importlib.util.find_spec("json")
package_spec = importlib.util.find_spec("importlib")
json_spec.loader_state = "seen"
checks = [
    importlib.import_module("json") is json,
    importlib.import_module("importlib.util") is importlib.util,
    importlib.invalidate_caches() is None,
    json_spec.name == "json",
    json_spec.origin == "built-in",
    json_spec.parent == "",
    json_spec.has_location is False,
    json_spec.cached is None,
    json_spec.loader_state == "seen",
    json_spec.submodule_search_locations is None,
    json_spec.loader.is_package("json") is False,
    package_spec.name == "importlib",
    package_spec.parent == "importlib",
    package_spec.loader.is_package() is True,
    len(package_spec.submodule_search_locations) == 0,
    importlib.util.find_spec("socket") is None,
    importlib.util.find_spec("hashlib") is not None,
    "importlib" in sys.builtin_module_names,
    "importlib.util" in sys.stdlib_module_names,
    "find_spec" in dir(importlib.util),
]
return str(checks)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "[True, True, True, True, True, True, True, True, True, True, True, True, True, True, True, True, True, True, True, True]",
            result.ReturnValue);
    }

    [Fact]
    public void FindSpecDescribesAllowlistedLocalModulesWithoutExecutingThem()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile(
            "/repo/helper.py",
            """
from pathlib import Path
Path("/repo/discovery-side-effect.txt").write_text("executed")
value = 42
""");

        var result = new LythonEngine().Run(
            """
import importlib.util

spec = importlib.util.find_spec("helper")
return "|".join([
    spec.name,
    spec.origin,
    spec.parent,
    str(spec.has_location),
    str(spec.submodule_search_locations is None),
    str(spec.loader.is_package()),
    spec.loader.get_source().splitlines()[-1],
    str(importlib.util.find_spec("missing") is None),
])
""",
            host,
            LocalOptions("helper", "/repo/helper.py"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("helper|/repo/helper.py||True|True|False|value = 42|True", result.ReturnValue);
        Assert.False(host.Exists("/repo/discovery-side-effect.txt"));
    }

    [Fact]
    public void ImportModuleReusesAllowlistedImportIdentityAndResolvesRelativeNames()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/helper.py", "value = 42\n");
        host.SeedFile("/repo/pkg/__init__.py", "kind = 'package'\n");
        host.SeedFile("/repo/pkg/child.py", "value = 7\n");

        var result = new LythonEngine().Run(
            """
import importlib
import importlib.util
import helper
import pkg.child

dynamic_helper = importlib.import_module("helper")
dynamic_child = importlib.import_module(".child", "pkg")
pkg_spec = importlib.util.find_spec("pkg")
child_spec = importlib.util.find_spec(".child", "pkg")
return "|".join([
    str(dynamic_helper is helper),
    str(dynamic_child is pkg.child),
    str(dynamic_helper.value),
    str(dynamic_child.value),
    pkg_spec.parent,
    pkg_spec.submodule_search_locations[0],
    child_spec.parent,
    importlib.util.resolve_name(".child", "pkg"),
])
""",
            host,
            LocalOptions("helper", "/repo/helper.py", "pkg", "/repo/pkg/__init__.py", "pkg/child.py", "/repo/pkg/child.py"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("True|True|42|7|pkg|/repo/pkg|pkg|pkg.child", result.ReturnValue);
    }

    [Fact]
    public async Task AsyncImportAndDiscoveryUseAsyncHostOperations()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedFile("/repo/helper.py", "value = 42\n");

        var result = await new LythonEngine().RunAsync(
            """
import importlib
import importlib.util

spec = importlib.util.find_spec("helper")
module = importlib.import_module("helper")
return spec.origin + "|" + str(module.value)
""",
            host,
            LocalOptions("helper", "/repo/helper.py"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal("/repo/helper.py|42", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("importlib.util.module_from_spec(None)", "ambient loader execution")]
    [InlineData("importlib.util.spec_from_file_location('x', '/tmp/x.py')", "arbitrary file-backed module loading")]
    [InlineData("importlib.util.spec_from_loader('x', None)", "contained discovery")]
    public void LoaderConstructionAndExecutionHelpersFailExplicitly(string expression, string messageFragment)
    {
        var result = new LythonEngine().Run(
            $$"""
import importlib.util
{{expression}}
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticContractsRecognizeImportlibModulesAndCallShapes()
    {
        var valid = new LythonEngine().Compile(
            """
import importlib
import importlib.util
from importlib import import_module
from importlib.util import find_spec

importlib.invalidate_caches()
import_module("json")
find_spec("json")
""");
        var invalid = new LythonEngine().Compile(
            """
import importlib
import importlib.util

importlib.import_module()
importlib.invalidate_caches(1)
importlib.util.find_spec()
importlib.util.resolve_name("x")
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Equal(4, invalid.Diagnostics.Count(d => d.Code == "LA3151"));
    }

    [Fact]
    public void RelativeAndMalformedNamesFailWithPythonShapedErrors()
    {
        var missingPackage = new LythonEngine().Run(
            """
import importlib
importlib.import_module(".child")
""",
            new MockLythonHost());
        var beyondTop = new LythonEngine().Run(
            """
import importlib.util
importlib.util.resolve_name("...child", "pkg")
""",
            new MockLythonHost());
        var empty = new LythonEngine().Run(
            """
import importlib
importlib.import_module("")
""",
            new MockLythonHost());

        Assert.Equal("TypeError", missingPackage.Failure?.ExceptionType);
        Assert.Contains("package", missingPackage.Failure?.Message, StringComparison.Ordinal);
        Assert.Equal("ImportError", beyondTop.Failure?.ExceptionType);
        Assert.Contains("beyond top-level package", beyondTop.Failure?.Message, StringComparison.Ordinal);
        Assert.Equal("ValueError", empty.Failure?.ExceptionType);
        Assert.Contains("Empty module name", empty.Failure?.Message, StringComparison.Ordinal);
    }

    private static LythonRunOptions LocalOptions(params string[] allowed)
        => new()
        {
            SourcePath = "/repo/main.py",
            AllowedLocalModules = allowed.ToHashSet(StringComparer.Ordinal),
        };

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));
}
