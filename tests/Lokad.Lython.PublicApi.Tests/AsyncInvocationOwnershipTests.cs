using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AsyncInvocationOwnershipTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task ScopeCreationFailureKeepsTaskOutcomeAndDisposesEarlierOwners(int failingScope, bool cancelled)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("print('guest ran')");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exception expected = cancelled ? new OperationCanceledException(cancellation.Token)
            : new InvalidOperationException("scope creation failed");
        var budget = new FailingScopeBudget(failingScope, expected);
        var host = new MockLythonHost();
        var writes = 0;
        host.OnStandardOutputWrite = () => Interlocked.Increment(ref writes);

        // Setup failures belong to the returned task; entering RunAsync must
        // not synchronously throw or publish a guest result before cleanup.
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget });
        var actual = await Assert.ThrowsAnyAsync<Exception>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        if (cancelled)
        {
            var error = Assert.IsAssignableFrom<OperationCanceledException>(actual);
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.True(pending.IsCanceled);
        }
        else
        {
            Assert.Same(expected, actual);
            Assert.True(pending.IsFaulted);
        }
        Assert.Equal(failingScope - 1, budget.DisposedScopes);
        Assert.Equal(0, budget.Root.ReservedBytes);
        Assert.Equal(0, writes);
    }

    private sealed class FailingScopeBudget(int failingScope, Exception error) : ILythonMemoryBudget
    {
        internal readonly LythonMemoryBudget Root = new();
        internal int DisposedScopes;
        private int _created;

        public ILythonMemoryScope CreateScope()
        {
            if (Interlocked.Increment(ref _created) == failingScope) throw error;
            return new Owner(this, Root.CreateScope());
        }

        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
            => Root.TryReserve(bytes, out reservation);

        private sealed class Owner(FailingScopeBudget budget, ILythonMemoryScope inner) : ILythonMemoryScope
        {
            private bool _disposed;
            public ILythonMemoryScope CreateScope() => new Owner(budget, inner.CreateScope());
            public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
                => inner.TryReserve(bytes, out reservation);
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                inner.Dispose();
                Interlocked.Increment(ref budget.DisposedScopes);
            }
        }
    }
}
