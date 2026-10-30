using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class FormatStorageTests
{
    [Fact]
    public void NestedSpecStorageRefundsAfterEachOperationWithoutRetainingPrivateCopies()
    {
        var root = new LythonMemoryBudget(64 * 1024);
        using (var owner = root.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var template = PyString.FromString("{0:{1}}", governor);
            var width = PyString.FromString(new string('x', 4096), governor);
            var baseline = root.ReservedBytes;
            for (var i = 0; i < 3; i++)
            {
                var result = PyStringOps.Format(template, [new BigInteger(7), width], null, null,
                    (value, _, spec) =>
                    {
                        if (value is PyString text) return text;
                        Assert.Equal(new string('x', 4096), spec);
                        return PyString.Empty;
                    }, governor, null);
                Assert.Same(PyString.Empty, result);
                Assert.Equal(baseline, root.ReservedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public async Task CancelledLookupKeepsMetadataFundedUntilItSettlesAndStopsLaterCallbacks()
    {
        var root = new LythonMemoryBudget(64 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var template = PyString.FromString("{name.attr:{width}}", governor);
            var baseline = root.ReservedBytes;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = PyStringOps.FormatMapAsync(template, _ => { entered.TrySetResult(); return new(release.Task); },
                (_, _) => throw new InvalidOperationException("suffix after cancellation"),
                (_, _, _) => throw new InvalidOperationException("format after cancellation"),
                governor, new(0, 0, 0, 0), cancellation.Token).AsTask();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(root.ReservedBytes > baseline);
                cancellation.Cancel();
                Assert.False(pending.IsCompleted);
                Assert.True(root.ReservedBytes > baseline);
            }
            finally
            {
                cancellation.Cancel();
                release.TrySetResult(PyString.Empty);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            Assert.Equal(baseline, root.ReservedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
