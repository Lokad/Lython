using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GovernedMemoryContractTests
{
    [Fact]
    public async Task RefusedCompilationPreservesItsBorrowedBudget()
    {
        var budget = new LythonMemoryBudget(1);
        using var parent = budget.CreateScope();
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return 123\n", parent);
        Assert.False(script.IsValid);
        using (var result = script.Run(new MockLythonHost()))
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        Assert.Equal(0, budget.ReservedBytes);
        parent.Dispose();
        Assert.Throws<ObjectDisposedException>(() => script.Run(new MockLythonHost()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => script.RunAsync(new MockLythonHost()));
    }

    [Fact]
    public void RetainedResultsRefuseFurtherWorkAndDisposalMakesAllowanceReusable()
    {
        var budget = new LythonMemoryBudget(512000);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return 'x' * 16000\n", budget);
        var baseline = budget.ReservedBytes;
        var retained = new List<LythonExecutionResult>();
        var refused = false;
        try
        {
            for (var i = 0; i < 32; i++)
            {
                var before = budget.ReservedBytes;
                var result = script.Run(new MockLythonHost());
                if (!result.Success)
                {
                    refused = true;
                    result.Dispose();
                    Assert.Equal(before, budget.ReservedBytes);
                    break;
                }
                retained.Add(result);
            }
            Assert.True(refused);
            Assert.NotEmpty(retained);
            Assert.Equal(16000, Assert.IsType<string>(retained[0].ReturnValue).Length);
        }
        finally { foreach (var result in retained) result.Dispose(); }
        Assert.Equal(baseline, budget.ReservedBytes);
        using var resumed = script.Run(new MockLythonHost());
        Assert.True(resumed.Success);
        resumed.Dispose();
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void EstimatesCannotOverflowTheSharedAllowance()
    {
        var budget = new LythonMemoryBudget();
        using var owner = budget.CreateScope();
        Assert.True(owner.TryReserve(long.MaxValue, out var reservation));
        Assert.False(budget.TryReserve(1, out var refused));
        Assert.Null(refused);
        Assert.Equal(long.MaxValue, reservation.Bytes);
        Assert.Equal(long.MaxValue, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CompilationAndItsCacheKeepTheirLongerLivedOwner()
    {
        var budget = new LythonMemoryBudget(512000);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return 'x' * 16000\n", budget);
        Assert.True(script.IsValid);
        var compilation = budget.ReservedBytes;
        Assert.True(compilation >= 32768);
        using var result = script.Run(new MockLythonHost());
        Assert.True(result.Success);
        Assert.True(budget.ReservedBytes > compilation);
        var retained = budget.ReservedBytes - compilation;
        script.Dispose();
        Assert.Equal(retained, budget.ReservedBytes);
        Assert.Throws<ObjectDisposedException>(() => script.Run(new MockLythonHost()));
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(512000, out var stillBorrowed));
        stillBorrowed.Dispose();
    }

    [Fact]
    public void CompilationRefusalAllocatesNoCompiledPayloadAndReturnsTheBaseline()
    {
        var budget = new LythonMemoryBudget(1);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return 123\n", budget);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA0008");
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void OneShotRunReleasesItsCompilationBeforeReturningRetainedOutput()
    {
        var budget = new LythonMemoryBudget(512000);
        using var result = new LythonEngine(Utf8RegexProvider.Instance).Run("return 'x' * 16000\n", new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = budget });
        Assert.True(result.Success);
        Assert.InRange(budget.ReservedBytes, 32000, 40000);
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ScopesAreEmptyAndShareTheirRootWithoutIndependentCaps()
    {
        var budget = new LythonMemoryBudget(128);
        using var first = budget.CreateScope();
        using var second = budget.CreateScope();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(first.TryReserve(96, out var retained));
        Assert.False(second.TryReserve(33, out var refused));
        Assert.Null(refused);
        Assert.Equal(96, retained.Bytes);
        Assert.Equal(96, budget.ReservedBytes);
        Assert.True(second.TryReserve(32, out var other));
        Assert.Equal(128, budget.ReservedBytes);
        retained.Dispose();
        Assert.True(other.TryResize(128));
        Assert.Equal(128, budget.ReservedBytes);
    }

    [Fact]
    public void FailedResizePreservesOwnershipAndAncestorDisposalInvalidatesItsSubtree()
    {
        var budget = new LythonMemoryBudget(128);
        var scope = budget.CreateScope();
        var child = scope.CreateScope();
        Assert.True(child.TryReserve(96, out var reservation));
        Assert.False(reservation.TryResize(129));
        Assert.Equal(96, reservation.Bytes);
        Assert.Equal(96, budget.ReservedBytes);
        Assert.Equal(96, budget.PeakBytes);
        Assert.True(reservation.TryResize(64));
        Assert.Equal(64, budget.ReservedBytes);
        scope.Dispose();
        Assert.Equal(0, reservation.Bytes);
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Throws<ObjectDisposedException>(() => child.TryReserve(0, out _));
        Assert.Throws<ObjectDisposedException>(() => reservation.TryResize(0));
        Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
        scope.Dispose();
        child.Dispose();
        reservation.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ZeroIsValidAndNegativeEstimatesAreRejectedWithoutChangingAccounting()
    {
        var budget = new LythonMemoryBudget(0);
        using var scope = budget.CreateScope();
        Assert.True(scope.TryReserve(0, out var zero));
        Assert.True(zero.TryResize(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => scope.TryReserve(-1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => zero.TryResize(-1));
        Assert.False(zero.TryResize(1));
        Assert.Equal(0, zero.Bytes);
        Assert.Equal(0, budget.ReservedBytes);
        zero.Dispose();
    }

    [Fact]
    public void DeepOwnerDisposalUsesNoRecursiveManagedStack()
    {
        var budget = new LythonMemoryBudget(128);
        var owner = budget.CreateScope();
        var deepest = owner;
        for (var i = 0; i < 4096; i++) deepest = deepest.CreateScope();
        Assert.True(deepest.TryReserve(128, out var reservation));
        owner.Dispose();
        Assert.Equal(0, reservation.Bytes);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResultsOutliveExecutionCleanupAndShareTheAllowance(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512000);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return ['x' * 16000]\n");
        var options = new LythonRunOptions { MemoryBudget = budget };
        using var first = asynchronous ? await script.RunAsync(new DelayedLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(first.Success);
        var retained = budget.ReservedBytes;
        Assert.InRange(retained, 32000, 40000);
        using var second = asynchronous ? await script.RunAsync(new DelayedLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(second.Success);
        Assert.Equal(2 * retained, budget.ReservedBytes);
        var alias = Assert.IsType<List<object?>>(first.ReturnValue);
        Assert.Equal(16000, Assert.IsType<string>(alias[0]).Length);
        alias = null!;
        first.Dispose();
        Assert.Equal(retained, budget.ReservedBytes);
        second.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ResultOwnerCanSurviveItsBorrowedInvocationAncestor()
    {
        var root = new LythonMemoryBudget(512000);
        var invocation = root.CreateScope();
        using var result = new LythonEngine(Utf8RegexProvider.Instance).Compile("return 'x' * 16000\n").Run(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = invocation, ResultMemoryBudget = root });
        Assert.True(result.Success);
        invocation.Dispose();
        Assert.True(root.ReservedBytes >= 32000);
        Assert.Equal(16000, Assert.IsType<string>(result.ReturnValue).Length);
        result.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void ReturnedMagnitudeKeepsItsStorageFundedAfterExecutionEnds()
    {
        var budget = new LythonMemoryBudget(512000);
        using var result = new LythonEngine(Utf8RegexProvider.Instance).Compile("return 1 << 128000\n").Run(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = budget });
        Assert.True(result.Success);
        Assert.True(budget.ReservedBytes >= 16000);
        Assert.IsType<System.Numerics.BigInteger>(result.ReturnValue);
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ConcurrentAdmissionCannotOversubscribeAndOwnersReturnTheBaseline()
    {
        var budget = new LythonMemoryBudget(64);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            using var owner = budget.CreateScope();
            for (var i = 0; i < 128; i++)
                if (owner.TryReserve(16, out var reservation))
                {
                    Assert.InRange(budget.ReservedBytes, 16, 64);
                    reservation.Dispose();
                }
        })));
        Assert.Equal(0, budget.ReservedBytes);
        Assert.InRange(budget.PeakBytes, 16, 64);
    }
}
