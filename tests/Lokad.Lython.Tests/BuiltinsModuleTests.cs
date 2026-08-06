using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BuiltinsModuleTests
{
    private const string IdentitySource = """
import builtins
import sys
from builtins import len as size, ValueError as ImportedValueError

original_len = len
def shadowed_len(value):
    return 99
def shadow_is_builtin():
    len = shadowed_len
    return builtins.len is len

checks = [
    builtins.__name__,
    str(builtins.len is original_len),
    str(shadow_is_builtin()),
    str(builtins.object is object),
    str(builtins.ValueError is ValueError),
    str(ImportedValueError is ValueError),
    str(builtins.IOError is builtins.OSError),
    str(builtins.EnvironmentError is OSError),
    str(getattr(builtins, "True") is True),
    str(getattr(builtins, "False") is False),
    str(getattr(builtins, "None") is None),
    str(builtins.__debug__),
    str(size([1, 2, 3])),
    str(builtins is sys.modules["builtins"]),
    str("builtins" in sys.builtin_module_names),
    str("builtins" in sys.stdlib_module_names),
    str("len" in dir(builtins)),
    str("__file__" in dir(builtins)),
    str(hasattr(builtins, "eval")),
]
return "|".join(checks)
""";

    [Fact]
    public void ModuleExposesTheContextsExactSupportedBuiltinObjects()
    {
        var result = new LythonEngine().Run(IdentitySource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? Describe(result.Diagnostics));
        Assert.Equal(
            "builtins|True|False|True|True|True|True|True|True|True|True|True|3|True|True|True|True|False|False",
            result.ReturnValue);
    }

    [Fact]
    public async Task RunAsync_UsesTheSameBuiltinModuleView()
    {
        var result = await new LythonEngine().RunAsync(IdentitySource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? Describe(result.Diagnostics));
        Assert.Equal(
            "builtins|True|False|True|True|True|True|True|True|True|True|True|3|True|True|True|True|False|False",
            result.ReturnValue);
    }

    [Fact]
    public void LocalModulesShareBuiltinIdentityWithoutInheritingScriptMetadata()
    {
        var host = new MockLythonHost();
        host.SeedFile(
            "/repo/helper.py",
            """
import builtins

same = builtins.len is len and builtins.object is object and builtins.ValueError is ValueError
metadata = builtins.__name__ + "|" + str(hasattr(builtins, "__file__")) + "|" + __name__
""");

        var result = new LythonEngine().Run(
            """
import builtins
import helper
return str(helper.same) + "|" + helper.metadata + "|" + str(builtins.len is len)
""",
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/main.py",
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper", "/repo/helper.py" }
            });

        Assert.True(result.Success, result.Failure?.Message ?? Describe(result.Diagnostics));
        Assert.Equal("True|builtins|False|helper|True", result.ReturnValue);
    }

    [Fact]
    public void StaticModuleSurfaceAcceptsSupportedNamesAndRejectsUnsupportedOnes()
    {
        var supported = new LythonEngine().Compile(
            """
import builtins
from builtins import len as size, ValueError
count = size([1, 2])
name = builtins.__name__
""");
        Assert.True(supported.IsValid, Describe(supported.Diagnostics));

        var unsupported = new LythonEngine().Compile(
            """
import builtins
missing = builtins.eval
""");
        Assert.False(unsupported.IsValid);
        Assert.Contains(unsupported.Diagnostics, diagnostic => diagnostic.Code == "LA3113");
    }

    private static string Describe(IReadOnlyList<LythonDiagnostic> diagnostics)
        => string.Join(" | ", diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}
