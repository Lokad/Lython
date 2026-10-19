using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class BinaryFileScopeLifetimeTests
{
    [Theory]
    [InlineData("Read", false)]
    [InlineData("Read", true)]
    [InlineData("Write", false)]
    [InlineData("Write", true)]
    [InlineData("Append", false)]
    [InlineData("Append", true)]
    public void EscapingHandleRetainsItsStorageWithoutRetainingCreationLocals(string mode, bool close)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/blob", [(byte)'g']);
        var budget = new LythonMemoryBudget(28000);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var (handle, frame, payload) = CreateHandle(context, Enum.Parse<LythonRuntime.TextFileOperation>(mode), close);
        var funded = budget.ReservedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(frame.TryGetTarget(out _));
        Assert.False(payload.TryGetTarget(out _));
        Assert.Equal(funded, budget.ReservedBytes);
        Assert.True(funded >= handle.OwnedBytes);
        Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.Reserve(16000, null));
        if (!close) handle.Exit();
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(handle);
        handle = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(16000, out var next));
        next.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (LythonRuntime.ExecutionContext.BinaryFileHandle Handle, WeakReference<LythonRuntime.ExecutionContext> Frame,
        WeakReference<PyString> Payload) CreateHandle(LythonRuntime.ExecutionContext parent, LythonRuntime.TextFileOperation mode, bool close)
    {
        var frame = new LythonRuntime.ExecutionContext(parent);
        var payload = PyString.FromString(new string('x', 16000), parent.MemoryGovernor);
        frame.Variables["unrelated"] = payload;
        var handle = LythonRuntime.ExecutionContext.BinaryFileHandle.OpenAsync("/blob", mode, frame, new(0, 0, 0, 0), false)
            .GetAwaiter().GetResult();
        if (close) handle.Exit();
        return (handle, new(frame), new(payload));
    }
}
