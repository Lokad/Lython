using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DateTimeDirectiveTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AmbiguityCompositesLiteralsAndFractionsMatchPython(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import datetime,time
            for value,pattern in [('112','%m%d'),('2024-002','%Y-%j'),
                ('Tue Jan 02 03:04:05 2024','%c'),('x  2','x %d'),
                ('2024-01-02 +02:30','%Y-%m-%d %z'),('literal %','literal %%'),('UTC','%Z'),('GMT','%Z')]:
                print(datetime.datetime.strptime(value,pattern).isoformat())
            print(datetime.datetime(2024,1,2,3,4,5,123456).strftime('%f'))
            print(format(datetime.date(2024,1,2),'🌍 %Y'))
            print(time.strftime('%Y-%m-%d',(2024,1,2,3,4,5,1,2,-1)))
            for pattern in ['%f%f','%c%Y']:
                try: datetime.datetime.strptime('1111',pattern)
                except Exception as error: print(type(error).__name__)
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(262144);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("1900-11-02T00:00:00\n2024-01-02T00:00:00\n2024-01-02T03:04:05\n1900-01-02T00:00:00\n2024-01-02T00:00:00+02:30\n1900-01-01T00:00:00\n1900-01-01T00:00:00\n1900-01-01T00:00:00\n123456\n🌍 2024\n2024-01-02\nPatternError\nPatternError\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancellationInterruptsAnAdmittedDirectiveScan(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import datetime\nreturn datetime.datetime.strptime('" + new string('a', 1000000) + "','%Z!')");
        Assert.True(script.IsValid);
        using var cancellation = new CancellationTokenSource();
        using var budget = new CancelDuringWorkingStorage(cancellation);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token }))
        {
            Assert.True(budget.Armed);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
        using var reuse = new LythonEngine().Compile("return 42");
        using var reused = reuse.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget.Root });
        Assert.True(reused.Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RetainedParsingFailuresKeepTheirMessagesFunded(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import datetime\nerrors=[]\nfor i in range(5):\n try: datetime.datetime.strptime('" + new string('x', 50000) + "','y')\n except ValueError as error: errors.append(error)\nreturn len(errors)");
        Assert.True(script.IsValid);
        foreach (var allowance in new[] { 524288L, 1048576L })
        {
            var budget = new LythonMemoryBudget(allowance);
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                if (allowance == 524288) Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new System.Numerics.BigInteger(5), result.ReturnValue);
                }
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    // Start cancellation after input preparation and working admission. A
    // dedicated worker avoids thread-pool timer starvation during the full
    // suite; it is joined before the token source is disposed. No checkpoint
    // counts, exact reservation sizes or elapsed-time assertions are used.
    private sealed class CancelDuringWorkingStorage : ILythonMemoryBudget, IDisposable
    {
        internal readonly LythonMemoryBudget Root = new();
        internal bool Armed;
        private bool _textAdmitted;
        private readonly ManualResetEventSlim _ready = new();
        private readonly ManualResetEventSlim _release = new();
        private readonly Thread _worker;
        private bool _dispose;

        internal CancelDuringWorkingStorage(CancellationTokenSource cancellation)
        {
            _worker = new Thread(() =>
            {
                _ready.Set();
                _release.Wait();
                if (Volatile.Read(ref _dispose)) return;
                // Give the admitted parser time to enter its scan, without a
                // thread-pool callback or the OS sleep timer's coarse quantum.
                var until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 1000;
                while (Stopwatch.GetTimestamp() < until) Thread.SpinWait(16);
                cancellation.Cancel();
            }) { IsBackground = true };
            _worker.Start();
            Assert.True(_ready.Wait(TimeSpan.FromSeconds(5)));
        }
        public void Dispose()
        {
            Volatile.Write(ref _dispose, true);
            _release.Set();
            Assert.True(_worker.Join(TimeSpan.FromSeconds(5)));
            _ready.Dispose();
            _release.Dispose();
        }
        private void Grew(long previous, long bytes)
        {
            if (bytes <= previous) return;
            if (_textAdmitted && !Armed) { Armed = true; _release.Set(); }
            if (bytes - previous > 100000) _textAdmitted = true;
        }
        public ILythonMemoryScope CreateScope() => new Owner(this, Root.CreateScope());
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
            => Reserve(Root, bytes, out reservation);
        private bool Reserve(ILythonMemoryBudget owner, long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!owner.TryReserve(bytes, out var inner)) { reservation = null; return false; }
            reservation = new Admission(this, inner);
            return true;
        }
        private sealed class Owner(CancelDuringWorkingStorage outer, ILythonMemoryScope inner) : ILythonMemoryScope
        {
            public ILythonMemoryScope CreateScope() => new Owner(outer, inner.CreateScope());
            public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation) => outer.Reserve(inner, bytes, out reservation);
            public void Dispose() => inner.Dispose();
        }
        private sealed class Admission(CancelDuringWorkingStorage outer, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var previous = inner.Bytes;
                if (!inner.TryResize(bytes)) return false;
                outer.Grew(previous, bytes);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
