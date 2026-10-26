using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class GzipPathScopeLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    private static readonly byte[] Member = Convert.FromHexString("1F8B08007B00000002FF4BCC29C848E44A4A2D49E402006E50306E0B000000");

    [Theory]
    [InlineData("rb", false)]
    [InlineData("rb", true)]
    [InlineData("rt", false)]
    [InlineData("rt", true)]
    [InlineData("wb", false)]
    [InlineData("wb", true)]
    [InlineData("wt", false)]
    [InlineData("wt", true)]
    [InlineData("ab", false)]
    [InlineData("ab", true)]
    [InlineData("at", false)]
    [InlineData("at", true)]
    public void EscapingPathHandlePreservesContentWithoutCreationLocals(string mode, bool close)
    {
        var host = new MockLythonHost();
        host.SeedBytes("/data.gz", Member);
        var nativeWorking = mode.StartsWith('r') ? LythonRuntime.ZlibModule.InflationScratchBytes + 32768
            : LythonRuntime.ZlibModule.CompressionScratchBytes;
        var budget = new LythonMemoryBudget(nativeWorking + 28000);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var (handle, frame, payload) = CreateHandle(context, mode, close);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(frame.TryGetTarget(out _));
        Assert.False(payload.TryGetTarget(out _));
        if (!close && mode.StartsWith('r'))
        {
            var value = Call(handle, "read", context);
            if (mode == "rt") Assert.Equal("alpha\nbeta\n", Assert.IsType<PyString>(value).AsString());
            else Assert.Equal("alpha\nbeta\n"u8.ToArray(), Assert.IsType<PyBytes>(value).Bytes.ToArray());
        }
        // Withdrawal frees CLR references, not the batch's scalar funding.
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= 16000);
        if (!close) Call(handle, "close", context);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(handle);
        handle = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(16000, out var next));
        next.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Handle, WeakReference<LythonRuntime.ExecutionContext> Frame, WeakReference<PyString> Payload)
        CreateHandle(LythonRuntime.ExecutionContext parent, string mode, bool close)
    {
        var frame = new LythonRuntime.ExecutionContext(parent);
        var payload = PyString.FromString(new string('x', 16000), parent.MemoryGovernor);
        frame.Variables["unrelated"] = payload;
        Assert.True(LythonRuntime.GzipModule.Instance.TryGetMember("open", out var open));
        var handle = Assert.IsAssignableFrom<LythonRuntime.ICallable>(open).Invoke(
            [CallArgumentValue.Positional(PyString.FromString("/data.gz")), CallArgumentValue.Positional(PyString.FromString(mode))], Span, frame);
        if (close) Call(handle, "close", frame);
        return (handle, new(frame), new(payload));
    }

    private static object Call(object handle, string member, LythonRuntime.ExecutionContext context)
    {
        Assert.True(((IPyDynamicAttributes)handle).TryGetMember(member, out var callable));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(callable).Invoke([], Span, context);
    }
}
