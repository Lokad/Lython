using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class FormatStorageTests
{
    [Fact]
    public async Task PreparedLayoutRetainsItsOwnerAndUsesEachOperationsToken()
    {
        var budget = new LythonMemoryBudget(64 * 1024);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var template = PyString.FromString("{0:>4}");
            var baseline = budget.ReservedBytes;
            var layout = PyStringOps.PrepareFormatTemplate(template, governor, null, default);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PyStringOps.FormatAsync(
                template, [BigInteger.One], null, null, (_, _, _, _, _) => default,
                governor, null, cancellationToken: cancellation.Token, prepared: layout).AsTask());
            Assert.Equal(retained, budget.ReservedBytes);
            var result = await PyStringOps.FormatAsync(template, [BigInteger.One], null, null,
                (_, _, _, _, _) => default, governor, null, prepared: layout);
            Assert.Same(PyString.Empty, result);
            Assert.Equal(retained, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void LayoutAdmissionRefusesBeforeRetainingStorage()
    {
        var budget = new LythonMemoryBudget(1);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var failure = Assert.Throws<LythonRuntimeException>(() => PyStringOps.PrepareFormatTemplate(
            PyString.FromString("{value:>4}"), governor, null, default));
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.Same(failure, governor.AdmissionFailure);
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }

    [Fact]
    public async Task NestedSpecStorageRefundsAfterEachOperationWithoutRetainingPrivateCopies()
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
                var result = await PyStringOps.FormatAsync(template, [new BigInteger(7), width], null, null,
                    (output, value, _, spec, _) =>
                    {
                        if (value is PyString text) { output.Append(text); return default; }
                        Assert.Equal(new string('x', 4096), spec);
                        return default;
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
            var pending = PyStringOps.FormatAsync(template, [], null,
                (_, _) => throw new InvalidOperationException("suffix after cancellation"),
                (_, _, _, _, _) => throw new InvalidOperationException("format after cancellation"),
                governor, new(0, 0, 0, 0), forbidPositionalFields: true, cancellationToken: cancellation.Token,
                resolveKeyword: _ => { entered.TrySetResult(); return new(release.Task); }).AsTask();
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
