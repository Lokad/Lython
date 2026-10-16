using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AdmissionFailureCommandTests
{
    public static IEnumerable<object[]> Exits()
    {
        var bodies = new[]
        {
            "try:\n x='x'*2000000\nexcept MemoryError:\n print('caught')",
            "try:\n x='x'*2000000\nexcept:\n print('caught')",
            "try:\n x='x'*2000000\nexcept BaseException:\n raise SystemExit(0)",
            "class Manager:\n def __enter__(self):return self\n def __exit__(self,a,b,c):\n  print('guest-exit')\n  return True\nwith Manager():\n x='x'*2000000",
            "def f():\n try:\n  x='x'*2000000\n finally:\n  return 0\nf()",
            "try:\n x='x'*2000000\nfinally:\n raise SystemExit(0)",
            "try:\n x='x'*2000000\nfinally:\n raise ValueError('replacement')",
            "try:\n x='x'*2000000\nfinally:\n print('guest-finally')",
        };
        for (var i = 0; i < bodies.Length; i++)
        foreach (var asynchronous in new[] { false, true })
        foreach (var local in new[] { false, true }) yield return [i, bodies[i], asynchronous, local];
    }

    [Theory]
    [MemberData(nameof(Exits))]
    public async Task RefusalCannotBecomeSuccessfulOrContinueGuestWork(int shape, string body, bool asynchronous, bool local)
    {
        var source = "print('before')\nwith open('/earlier.txt','w') as output:output.write('saved')\nif fail:\n "
            + body.Replace("\n", "\n ") + "\n with open('/later.txt','w') as output:output.write('bad')\nprint('after')\nreturn 0";
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, shape + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        var options = new LythonRunOptions
        {
            MaxExecutionMemoryBytes = local ? 64 * 1024 : null,
            Globals = new Dictionary<string, object?> { ["fail"] = true },
        };
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.False(result.Success);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(local ? LythonFailureReason.ExecutionMemoryLimit : LythonFailureReason.SharedMemoryAllowance,
                result.FailureAttribution?.Reason);
            Assert.Equal("before\n", result.StandardOutput);
            Assert.Equal("saved", host.ReadText("/earlier.txt"));
            Assert.Throws<InvalidOperationException>(() => host.ReadText("/later.txt"));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        options = new LythonRunOptions
        {
            MaxExecutionMemoryBytes = local ? 64 * 1024 : null,
            Globals = new Dictionary<string, object?> { ["fail"] = false },
        };
        using (var next = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.True(next.Success, next.Failure?.Message);
            Assert.Equal("before\nafter\n", next.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitGuestMemoryErrorStillFollowsPython(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("try:\n raise MemoryError('guest')\nexcept MemoryError as e:\n print(str(e))\nprint('after')");
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("guest\nafter\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("pass", false)]
    [InlineData("pass", true)]
    [InlineData("return 1", false)]
    [InlineData("return 1", true)]
    [InlineData("raise ValueError('guest')", false)]
    [InlineData("raise ValueError('guest')", true)]
    [InlineData("raise SystemExit(0)", false)]
    [InlineData("raise SystemExit(0)", true)]
    public async Task RefusedProjectionOwnerIsOneCleanCommandFailure(string source, bool asynchronous)
    {
        using var script = new LythonEngine().Compile(source);
        using var refusing = new RefusingBudget();
        var options = new LythonRunOptions { ResultMemoryBudget = refusing };
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        Assert.Equal(1, refusing.Refusals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectionRefusalCannotLeaveSystemExitZero(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("print('before')\nraise SystemExit(0)");
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 0 };
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(LythonFailureReason.ProjectionMemoryLimit, result.FailureAttribution?.Reason);
    }

    [Fact]
    public async Task RefusalAfterActualHostSuspensionReleasesOnlyItsOwnStorage()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var retainedScript = new LythonEngine().Compile("return 'held' * 4000", budget);
        using var held = retainedScript.Run(new MockLythonHost());
        Assert.True(held.Success, held.Failure?.Message);
        using var script = new LythonEngine().Compile("with open('/input.txt') as source:print(source.read())\nvalue='x'*2000000", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/input.txt", "before");
        using (var failed = await script.RunAsync(host))
        {
            Assert.False(failed.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failed.FailureAttribution?.Reason);
            Assert.Equal("before\n", failed.StandardOutput);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(string.Concat(Enumerable.Repeat("held", 4000)), held.ReturnValue);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        retainedScript.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        held.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("with open('/staged','w') as output:\n output.write('buffered')", false)]
    [InlineData("with open('/staged','w') as output:\n output.write('buffered')", true)]
    [InlineData("with open('/staged','wb') as output:\n output.write(b'buffered')", false)]
    [InlineData("with open('/staged','wb') as output:\n output.write(b'buffered')", true)]
    [InlineData("import gzip\nwith gzip.open('/staged','wb') as output:\n output.write(b'buffered')", false)]
    [InlineData("import gzip\nwith gzip.open('/staged','wb') as output:\n output.write(b'buffered')", true)]
    [InlineData("import zipfile\nwith zipfile.ZipFile('/staged','w') as output:\n output.writestr('entry',b'buffered')", false)]
    [InlineData("import zipfile\nwith zipfile.ZipFile('/staged','w') as output:\n output.writestr('entry',b'buffered')", true)]
    public async Task NativeCleanupDoesNotPublishBufferedWritesAfterRefusal(string prefix, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(prefix + "\n value='x'*2000000", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        immediate.SeedBytes("/staged", [1, 2, 3]);
        delayed.SeedBytes("/staged", [1, 2, 3]);
        using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(immediate))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(new byte[] { 1, 2, 3 }, asynchronous ? delayed.ReadBytes("/staged") : immediate.ReadBytes("/staged"));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    // A valid host hook may refuse even zero-byte ownership registration.
    // Empty children share this root and all admission refuses immediately.
    private sealed class RefusingBudget : ILythonMemoryScope
    {
        private readonly RefusingBudget? _parent;
        private readonly RefusingBudget _root;
        private bool _disposed;
        private int _refusals;
        public int Refusals => Volatile.Read(ref _root._refusals);
        public RefusingBudget() { _root = this; }
        private RefusingBudget(RefusingBudget parent) { _parent = parent; _root = parent._root; }
        public ILythonMemoryScope CreateScope() { CheckOpen(); return new RefusingBudget(this); }
        public bool TryReserve(long bytes, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            CheckOpen();
            ArgumentOutOfRangeException.ThrowIfNegative(bytes);
            Interlocked.Increment(ref _root._refusals);
            reservation = null;
            return false;
        }
        public void Dispose() { _disposed = true; }
        private void CheckOpen()
        {
            _parent?.CheckOpen();
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
