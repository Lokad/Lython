using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG06: str.join builds through the owning governor. A fixed join pins the
/// exact contract: builder growth peaks, then releases, leaving exactly the
/// retained string charge behind. Reflection reaches the private helper;
/// renames fail loudly here by design.
/// </summary>
public sealed class StringJoinAccountingTests
{
    [Fact]
    public void JoinStringsCommitsExactOutput()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        // JoinStrings lives directly on LythonRuntime, shared by string and CSV members.
        var join = typeof(LythonRuntime).GetMethod("JoinStrings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("JoinStrings not found.");
        var parts = Enumerable.Range(0, 100).Select(static _ => PyString.FromString("abcdefghij")).ToList();

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var peakBefore = context.MemoryGovernor.PeakAccountedBytes;
        var result = Assert.IsType<PyString>(join.Invoke(
            null,
            [PyString.FromString(","), parts, context.MemoryGovernor, span]));

        Assert.Equal(string.Join(",", Enumerable.Repeat("abcdefghij", 100)), result.AsString());
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(32 + 1099, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
        // The builder peaks above the retained charge and releases the
        // difference: with an ungoverned builder the peak would equal the
        // retained charge exactly (same idiom as SetConstructionAccounting).
        Assert.True(
            context.MemoryGovernor.PeakAccountedBytes - peakBefore >
                context.MemoryGovernor.CurrentCommittedBytes - committedBefore,
            "Join scratch was never covered while building.");
    }
}
