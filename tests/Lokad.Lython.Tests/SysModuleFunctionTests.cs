using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SysModuleFunctionTests
{
    [Fact]
    public void SysMetadata_ExposesContainedRuntimeView()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
import sys

parts = [
    str(sys.version.startswith("3.11.0")),
    str(sys.version_info[0]) + "." + str(sys.version_info[1]) + "." + str(sys.version_info[2]),
    str(sys.hexversion > 0),
    sys.implementation.name,
    sys.implementation.cache_tag,
    sys.platform,
    str(sys.maxsize > 0),
    sys.byteorder,
    sys.prefix,
    sys.base_prefix,
    sys.executable,
    sys.getdefaultencoding(),
    sys.path[0],
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host,
            new LythonRunOptions { SourcePath = "/repo/main.py" });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|3.11.0|True|lython|lython-3.11|lython|True|little|/repo|/repo|lython|utf-8|/repo", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SysModuleNameSets_UseDiscoverableLythonBuiltinsAndHostGates()
    {
        var noProcessHost = new MockLythonHost();
        var noProcess = new LythonEngine().Run(
            """
import sys
parts = [
    str("json" in sys.builtin_module_names),
    str("pkgutil" in sys.stdlib_module_names),
    str("subprocess" in sys.builtin_module_names),
    str("subprocess" in sys.stdlib_module_names),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            noProcessHost);

        Assert.True(noProcess.Success, noProcess.Failure?.Message);
        Assert.Equal("True|True|False|False", noProcessHost.ReadText("/out.txt"));

        var processHost = new MockLythonHost();
        processHost.EnableSubprocess();
        var process = new LythonEngine().Run(
            """
import sys
__lython_file = open("/out.txt", "w")
__lython_file.write(str("subprocess" in sys.builtin_module_names) + "|" + str("subprocess" in sys.stdlib_module_names))
__lython_file.close()
""",
            processHost);

        Assert.True(process.Success, process.Failure?.Message);
        Assert.Equal("True|True", processHost.ReadText("/out.txt"));
    }

    [Fact]
    public void SysModules_SnapshotsBuiltinsAndImportedLocalModules()
    {
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "value = 42");

        var result = new LythonEngine().Run(
            """
import sys

before = "helper" in sys.modules
import helper
after = "helper" in sys.modules
parts = [
    str("sys" in sys.modules),
    str("json" in sys.modules),
    str(before),
    str(after),
    str(sys.modules["helper"].value),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper.py", "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|False|True|42", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SysExcInfo_TracksActiveExceptBlockOnly()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import sys

outside = sys.exc_info()
try:
    raise ValueError("bad")
except ValueError as ex:
    active = sys.exc_info()
    inside = [
        str(outside[0] is None),
        active[0].__name__,
        active[1].type,
        active[1].message,
        str(active[1] is ex),
        str(active[2] is None),
    ]

after = sys.exc_info()
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(inside) + "||" + str(after[0] is None))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|ValueError|ValueError|bad|True|True||True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SysExit_CanBeCaughtWithCodeAndArgsPayload()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import sys

parts = []
try:
    sys.exit(7)
except SystemExit as ex:
    parts.append(str(ex.code))
    parts.append(str(ex.args))

try:
    raise SystemExit()
except SystemExit as ex:
    parts.append(str(ex.code is None))
    parts.append(str(ex.args))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("7|(7,)|True|()", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SysGetSizeOf_ReturnsApproximateContainedSizesAndDefaultFallback()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import sys

parts = [
    str(sys.getsizeof("abc") > 0),
    str(sys.getsizeof([1, 2, 3]) > sys.getsizeof([])),
    str(sys.getsizeof(sys.implementation, 123)),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|123", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SysStreams_ExposeHostMediatedReadWriteAndFlush()
    {
        var host = new MockLythonHost();
        host.SeedStandardInput("alpha\nbeta");

        var result = new LythonEngine().Run(
            """
import sys

first = sys.stdin.readline()
rest = sys.stdin.read()
sys.stdout.write(first)
sys.stdout.flush()
sys.stderr.write(rest)
sys.stderr.flush()
__lython_file = open("/out.txt", "w")
__lython_file.write(first + "|" + rest)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("alpha\n|beta", host.ReadText("/out.txt"));
        Assert.Equal("alpha\n", result.StandardOutput);
        Assert.Equal("beta", result.StandardError);
        Assert.Equal("alpha\n", host.CapturedStandardOutput());
        Assert.Equal("beta", host.CapturedStandardError());
    }

    [Theory]
    [InlineData("sys.settrace(None)", "sys.settrace(...) is not supported by Lython.")]
    [InlineData("sys.setprofile(None)", "sys.setprofile(...) is not supported by Lython.")]
    [InlineData("sys.setrecursionlimit(1000)", "sys.setrecursionlimit(...) is not supported by Lython")]
    [InlineData("sys.addaudithook(None)", "sys.addaudithook(...) is not supported by Lython.")]
    [InlineData("sys.audit(\"event\", 1)", "sys.audit(...) is not supported by Lython.")]
    public void SysProcessMutationHooks_FailExplicitly(string expression, string message)
    {
        var result = new LythonEngine().Run(
            "import sys\n" + expression + "\n",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("NotImplementedError", result.Failure!.ExceptionType);
        Assert.Contains(message, result.Failure.Message, StringComparison.Ordinal);
    }
}
