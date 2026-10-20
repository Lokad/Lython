using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class HostRequirementStorageTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1024, 17)]
    public void RefusedMetadataLeavesNoWorkingStorage(long limit, int calls)
    {
        var (script, facts) = Prepare(calls);
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var error = Assert.Throws<LythonRuntimeException>(() => StaticHostRequirementDiagnostics.Collect(script, facts, governor, default));
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Same(error, governor.AdmissionFailure);
            if (limit != 0) Assert.True(error.Attribution?.Observed > 0); // Existing backing stays funded during replacement.
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(0, governor.CurrentCommittedBytes);
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public void OnlyCompletedFactsRemainChargedToTheCompilationOwner()
    {
        var (script, facts) = Prepare(3);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var requirements = StaticHostRequirementDiagnostics.Collect(script, facts, governor, default);
            Assert.Equal(3, requirements.Length);
            Assert.Equal(new[] { 1, 2, 3 }, requirements.Select(requirement => requirement.Span.Line));
            Assert.All(requirements, requirement => Assert.Equal("LA3040", requirement.DiagnosticCode));
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.True(governor.CurrentCommittedBytes > 0);
            Assert.Equal(governor.CurrentCommittedBytes, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CancellationAfterAdmissionUnwindsPrivateStorage()
    {
        var (script, facts) = Prepare(3);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(new CancelOnGrowthBudget(owner, cancellation));
            var error = Assert.Throws<LythonRuntimeException>(() => StaticHostRequirementDiagnostics.Collect(script, facts, governor, cancellation.Token));
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.True(budget.PeakBytes > 0);
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal(0, governor.CurrentAccountedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static (ScriptSyntax, ScopeDirectiveFacts) Prepare(int calls)
    {
        var script = LythonFrontend.Compile(string.Concat(Enumerable.Repeat("input()\n", calls))).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(script));
        return (script, code.EntryPoint.ScopeFacts);
    }

    private sealed class CancelOnGrowthBudget(ILythonMemoryBudget inner, CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var storage)) { reservation = null; return false; }
            reservation = new Reservation(storage, cancellation);
            return true;
        }
        private sealed class Reservation(ILythonMemoryReservation inner, CancellationTokenSource cancellation) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var growth = bytes > inner.Bytes;
                if (!inner.TryResize(bytes)) return false;
                if (growth) cancellation.Cancel();
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
