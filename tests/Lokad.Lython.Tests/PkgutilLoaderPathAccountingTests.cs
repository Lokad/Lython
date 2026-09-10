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
        var type = typeof(LythonRuntime).GetNestedType("PkgutilModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PkgutilModule not found.");
        var method = type.GetMethod("TryCreateLoader", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TryCreateLoader not found.");
        var arguments = new object?[] { fullname, context, span, null };
        Assert.True((bool)method.Invoke(null, arguments)!);
        return Assert.IsType<LythonRuntime.PkgutilLoaderObject>(arguments[3]);
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
        var context = new LythonRuntime.ExecutionContext(host, options);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var loader = CreateLoader("helper", context, span);
        var sourcePath = Assert.IsType<PyString>(loader.SourcePath);
        Assert.NotNull(sourcePath.OwnerMemoryGovernor);
        Assert.Equal("/helper.py", sourcePath.AsString());
        // Shell (64) plus the governed name (128 + 6) and path (128 + 10).
        Assert.Equal(336L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void BuiltinLoaderHasNoSourcePath()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var loader = CreateLoader("sys", context, span);
        Assert.Null(loader.SourcePath);
        // Shell (64) plus the governed name (128 + 3).
        Assert.Equal(195L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}