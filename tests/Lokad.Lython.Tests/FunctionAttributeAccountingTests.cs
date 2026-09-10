using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: function metadata tables charge per new key like instance attribute
/// tables; overwrites stay free. Functions always carry their closure
/// governor, so there is no ungoverned case.
/// </summary>
public sealed class FunctionAttributeAccountingTests
{
    [Fact]
    public void MetadataSlotsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var function = new PyFunction(
            "f",
            [],
            [],
            context,
            new Dictionary<string, object>(StringComparer.Ordinal),
            ScopeDirectiveFacts.Empty);
        Assert.True(function.TrySetMember("a", PyNone.Instance));
        Assert.True(function.TrySetMember("a", PyNone.Instance));
        Assert.True(function.TrySetMember("b", PyNone.Instance));
        Assert.True(function.TrySetMember("c", PyNone.Instance));
        // Three attribute slots beside the owned definition name (128 + 1).
        Assert.Equal(3L * 64L + 129L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}