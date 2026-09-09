using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: Decimal.to_eng_string owns its payload like other scalar renderer
/// outputs (missed by the MG06 scalar-renderer slice).
/// </summary>
public sealed class DecimalEngineeringAccountingTests
{
    [Fact]
    public void ToEngineeringStringCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var member = LythonRuntime.DecimalMembers.TryGetMember(new PyDecimal(1.5m), "to_eng_string", out var value)
            ? value
            : throw new InvalidOperationException("to_eng_string member not found.");
        var text = (PyString)((LythonRuntime.ICallable)member).Invoke([], span, context);
        Assert.Equal("1.5", text.AsString());
        Assert.Same(context.MemoryGovernor, text.OwnerMemoryGovernor);
        Assert.Equal(131L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
