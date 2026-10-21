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
        using var script = new LythonEngine().Compile("import datetime\nreturn datetime.datetime.strptime('" + new string('a', 200000) + "','%Z!')");
        Assert.True(script.IsValid);
        using var cancellation = new CancellationTokenSource();
        var budget = new CancelDuringWorkingStorage(cancellation);
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

    // Arm once substantial text scratch exists and subsequent working storage
    // is admitted. The timer fires during parsing, not compilation/startup. No
    // checkpoint counts or exact reservation sizes form part of the assertion.
    private sealed class CancelDuringWorkingStorage(CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        internal readonly LythonMemoryBudget Root = new();
        internal bool Armed;
        private bool _textAdmitted;
        private void Grew(long previous, long bytes)
        {
            if (bytes <= previous) return;
            if (_textAdmitted && !Armed) { Armed = true; cancellation.CancelAfter(1); }
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
