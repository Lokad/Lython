using System.Runtime.InteropServices;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StringStoragePublicationTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IndexingAndStableMemoryKeepEachOthersStorage(bool borrowFirst)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var text = PyString.FromString("α😀β", context.MemoryGovernor, Span);
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            ReadOnlyMemory<byte> borrowed = default;
            if (borrowFirst) borrowed = text.Utf8Bytes;
            Assert.Equal(2, text.GetByteIndexForRuneBoundary(1));
            if (!borrowFirst) borrowed = text.Utf8Bytes;
            var held = root.ReservedBytes;
            for (var index = 0; index < 3; index++)
            {
                Assert.Equal(3, text.Length);
                Assert.Equal(6, text.GetByteIndexForRuneBoundary(2));
                Assert.Equal(Encoding.UTF8.GetBytes("α😀β"), borrowed.ToArray());
                Assert.True(MemoryMarshal.TryGetArray(borrowed, out var original));
                Assert.True(MemoryMarshal.TryGetArray(text.Utf8Bytes, out var current));
                Assert.Same(original.Array, current.Array);
                Assert.Equal(held, root.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.True(root.ReservedBytes >= text.CommittedOwnedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdmissionReentryPreservesBorrowAndAvoidsDuplicateCacheCharge(bool buildIndex)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var text = PyString.FromString("α😀β", context.MemoryGovernor, Span);
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            Assert.Equal(3, text.Length);
            var before = root.ReservedBytes;
            ReadOnlyMemory<byte> borrowed = default;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                borrowed = text.Utf8Bytes;
                if (buildIndex) Assert.Equal(6, text.GetByteIndexForRuneBoundary(2));
            };
            Assert.Equal(2, text.GetByteIndexForRuneBoundary(1));
            Assert.True(MemoryMarshal.TryGetArray(borrowed, out var original));
            Assert.True(MemoryMarshal.TryGetArray(text.Utf8Bytes, out var current));
            Assert.Same(original.Array, current.Array);
            Assert.Equal(Encoding.UTF8.GetBytes("α😀β"), borrowed.ToArray());
            Assert.Equal(before + 32L + 4L * (text.Length + 1), root.ReservedBytes);
            Assert.Equal(root.ReservedBytes, text.CommittedOwnedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
