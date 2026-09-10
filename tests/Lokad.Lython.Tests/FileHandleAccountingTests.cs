using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: open handles own the 128B constructed-value shell plus the path
/// payload at every construction funnel.
/// </summary>
public sealed class FileHandleAccountingTests
{
    [Fact]
    public void WriteHandleConstructionCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var method = typeof(LythonRuntime.ExecutionContext).GetNestedType("TextFileHandle", BindingFlags.NonPublic)
            ?.GetMethod("ForWrite", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, [typeof(string), typeof(LythonRuntime.ExecutionContext)])
            ?? throw new InvalidOperationException("ForWrite not found.");
        _ = method.Invoke(null, ["/f.txt", context]);
        // One table slot; the path aliases caller-owned or governed strings.
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
