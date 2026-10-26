using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: local-module loaders own their source-path string beside the shell
/// and name; builtin loaders stay path-free.
/// </summary>
public sealed class PkgutilLoaderPathAccountingTests
{
    private static LythonRuntime.PkgutilLoaderObject CreateLoader(string fullname, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var method = typeof(LythonRuntime).GetMethod("TryCreateContainedModuleSpec", BindingFlags.Static | BindingFlags.NonPublic)!;
        var arguments = new object?[] { fullname, context, span, null };
        Assert.True((bool)method.Invoke(null, arguments)!);
        Assert.True(((LythonRuntime.ImportlibModuleSpecObject)arguments[3]!).TryGetMember("loader", out var loader));
        return Assert.IsType<LythonRuntime.PkgutilLoaderObject>(loader);
    }

    [Fact]
    public void LocalLoaderOwnsSourcePath()
    {
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "value = 1\n");
        var options = new LythonRunOptions
        {
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" },
        };
        var context = new LythonRuntime.ExecutionContext(host, options,regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var loader = CreateLoader("helper", context, span);
        var sourcePath = Assert.IsType<PyString>(loader.SourcePath);
        Assert.NotNull(sourcePath.OwnerMemoryGovernor);
        Assert.Equal("/helper.py", sourcePath.AsString());
        // The selected CLR path and exposed UTF-8 path have separate lifetimes.
        // Both must be funded; do not prescribe one exact bookkeeping total.
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >=
            64L + loader.Name.CommittedOwnedBytes + sourcePath.CommittedOwnedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void BuiltinLoaderHasNoSourcePath()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var loader = CreateLoader("sys", context, span);
        Assert.Null(loader.SourcePath);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= 64L + loader.Name.CommittedOwnedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
