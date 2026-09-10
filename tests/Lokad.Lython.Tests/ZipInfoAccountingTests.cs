using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: the ZipInfo factory owns the 128B constructed-value shell beside the
/// governed filename and date_time payloads; the date_time normalization
/// without a context stays free for the assignment path.
/// </summary>
public sealed class ZipInfoAccountingTests
{
    private static object InvokeZipInfo(LythonRuntime.ExecutionContext? context, LythonSourceSpan? span, CallArgumentValue[] arguments)
    {
        var type = typeof(LythonRuntime).GetNestedType("ZipInfoCallable", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ZipInfoCallable not found.");
        var instance = type.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException("ZipInfoCallable.Instance not found.");
        var method = type.GetMethod("Invoke") ?? throw new InvalidOperationException("Invoke not found.");
        return method.Invoke(instance, [arguments, span, context])
            ?? throw new InvalidOperationException("Invoke returned null.");
    }

    [Fact]
    public void FactoryDefaultsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = InvokeZipInfo(context, span, []);
        // Shell (128) plus the NoName filename (128 + 6) plus the default
        // date_time backing (32 + 16 x 6).
        Assert.Equal(390L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
