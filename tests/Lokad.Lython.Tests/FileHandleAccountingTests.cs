using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Open handles fund their shell and its existing reclamation registration.
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
        Assert.Equal(64L + ChargeReclamationPool.EntryChargeBytes + context.State.CallTemporaries.CommittedBackingBytes,
            context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(1, context.State.CallTemporaries.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
