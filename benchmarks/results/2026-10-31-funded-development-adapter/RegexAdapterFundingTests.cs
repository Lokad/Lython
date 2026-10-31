namespace Lokad.Lython.PublicApi.Tests;

// Development-only public-boundary checks of the exact funded producer pin.
// No implementation-type access or exact tiny-budget threshold is required.
public sealed class RegexAdapterFundingTests
{
    [Fact]
    public void ConstructionRefusalPreservesItsOriginAndDoesNotRetry()
    {
        var root = new LythonMemoryBudget();
        var budget = new ObservedBudget(root);
        budget.State.DenyNextGrowth = true;
        var error = Assert.Throws<LythonRegexException>(() =>
            Utf8RegexProvider.Instance.Compile("(?P<word>a+)"u8.ToArray(), new(32, false), budget, default));
        Assert.Equal(LythonRegexFailure.MemoryAdmission, error.Failure);
        Assert.True(budget.State.DeniedBytes > 0);
        Assert.Equal(budget.State.DeniedBytes, error.RequestedBytes);
        Assert.False(budget.State.GrewAfterRefusal);
        Assert.Equal(0, root.ReservedBytes);

        using (var pattern = Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), new(32, false), root, default))
            Assert.True(root.ReservedBytes > 0);
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void WorkingRefusalReleasesCursorButKeepsProgramFunded()
    {
        var programs = new LythonMemoryBudget();
        var root = new LythonMemoryBudget();
        var working = new ObservedBudget(root);
        using (var pattern = Utf8RegexProvider.Instance.Compile("(a)"u8.ToArray(), new(32, false), programs, default))
        {
            var programBalance = programs.ReservedBytes;
            Assert.True(programBalance > 0);
            using (var subject = pattern.Prepare("aa"u8.ToArray(), new(0, 2, 0, 2), working, default))
            using (var cursor = subject.Find(default))
            {
                working.State.DenyNextGrowth = true;
                var error = Assert.Throws<LythonRegexException>(() => cursor.Next(default));
                Assert.Equal(LythonRegexFailure.MemoryAdmission, error.Failure);
                Assert.True(working.State.DeniedBytes > 0);
                Assert.Equal(working.State.DeniedBytes, error.RequestedBytes);
                Assert.False(working.State.GrewAfterRefusal);
                Assert.Null(cursor.Next(default));
                Assert.Equal(0, root.ReservedBytes);
                Assert.Equal(programBalance, programs.ReservedBytes);
            }

            using (var subject = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), root, default))
            using (var cursor = subject.Find(default))
                Assert.Equal(new LythonRegexCapture(0, 1), cursor.Next(default)!.Captures[0]);
            Assert.Equal(0, root.ReservedBytes);
            Assert.Equal(programBalance, programs.ReservedBytes);
        }
        Assert.Equal(0, programs.ReservedBytes);
    }

    [Fact]
    public void InterleavedCursorsKeepIndependentCurrentBorrows()
    {
        var budget = new LythonMemoryBudget();
        using (var pattern = Utf8RegexProvider.Instance.Compile("(a)(b)?"u8.ToArray(), new(32, false), budget, default))
        {
            var baseline = budget.ReservedBytes;
            using (var subject = pattern.Prepare("ab a"u8.ToArray(), new(0, 4, 0, 4), budget, default))
            using (var left = subject.Find(default))
            using (var right = subject.Find(default))
            {
                Assert.Equal(new LythonRegexCapture(0, 2), left.Next(default)!.Captures[0]);
                var otherCurrent = right.Next(default)!;
                var next = left.Next(default)!;
                Assert.Equal(new LythonRegexCapture(3, 4), next.Captures[0]);
                Assert.Null(next.Captures[2]);
                Assert.Equal(new LythonRegexCapture(0, 2), otherCurrent.Captures[0]);
                Assert.Equal(new LythonRegexCapture(1, 2), otherCurrent.Captures[2]);
                Assert.Null(left.Next(default));
                Assert.Equal(new LythonRegexCapture(0, 2), otherCurrent.Captures[0]);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Observation
    {
        internal bool DenyNextGrowth;
        internal long? DeniedBytes;
        internal bool GrewAfterRefusal;

        internal bool Refuse(long bytes)
        {
            if (bytes <= 0) return false;
            if (DeniedBytes is not null) GrewAfterRefusal = true;
            if (!DenyNextGrowth) return false;
            DenyNextGrowth = false;
            DeniedBytes ??= bytes;
            return true;
        }
    }

    private sealed class ObservedBudget(ILythonMemoryBudget budget, Observation? state = null) : ILythonMemoryBudget
    {
        internal Observation State { get; } = state ?? new();
        public ILythonMemoryScope CreateScope() => new Scope(budget.CreateScope(), State);
        public bool TryReserve(long bytes, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILythonMemoryReservation? result)
        {
            result = null;
            if (State.Refuse(bytes) || !budget.TryReserve(bytes, out var reservation)) return false;
            result = new Reservation(reservation, State);
            return true;
        }

        private sealed class Scope(ILythonMemoryScope scope, Observation state) : ILythonMemoryScope
        {
            private readonly ObservedBudget _budget = new(scope, state);
            public ILythonMemoryScope CreateScope() => _budget.CreateScope();
            public bool TryReserve(long bytes, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILythonMemoryReservation? result)
                => _budget.TryReserve(bytes, out result);
            public void Dispose() => scope.Dispose();
        }

        private sealed class Reservation(ILythonMemoryReservation reservation, Observation state) : ILythonMemoryReservation
        {
            public long Bytes => reservation.Bytes;
            public bool TryResize(long bytes) => !state.Refuse(bytes - Bytes) && reservation.TryResize(bytes);
            public void Dispose() => reservation.Dispose();
        }
    }
}
