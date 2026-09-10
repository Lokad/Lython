using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: os.stat results own one box unit per call beside host-side state.
/// </summary>
public sealed class StatValueAccountingTests
{
    [Fact]
    public void StatCallCommitsExactly()
    {
        var host = new MockLythonHost();
        host.SeedFile("/d/f0", "x");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var method = typeof(LythonRuntime).GetMethod("OsStat", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OsStat not found.");
        _ = method.Invoke(null, [new object[] { Lokad.Lython.Runtime.Text.PyString.FromString("/d/f0") }, span, context]);
        Assert.Equal(64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
