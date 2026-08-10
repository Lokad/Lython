using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class OpenPyxlSaveGuardTests
{
    [Fact]
    public void Safe_DoesNotExposeAReason()
    {
        var guard = LythonRuntime.OpenPyxlSaveGuard.Safe;

        Assert.False(guard.TryGetUnsafeReason(out _));
    }

    [Fact]
    public void Unsafe_RequiresConsumersToHandleItsReason()
    {
        var guard = LythonRuntime.OpenPyxlSaveGuard.Unsafe("unsupported relationship");

        Assert.True(guard.TryGetUnsafeReason(out var reason));
        Assert.Equal("unsupported relationship", reason);
    }
}
