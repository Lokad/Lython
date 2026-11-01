using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeProtocolDictionaryWriteTests
{
    public sealed record Case(string Name, string Source, string Output);

    public static IEnumerable<object[]> Cases() => JsonSerializer.Deserialize<Case[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "DictionaryWrites", "cases.json")))!
        .Select(item => new object[] { item.Name, item });

    [Theory, MemberData(nameof(Cases))]
    public async Task NativeAndCustomKeysFollowPythonWrites(string name, Case item)
    {
        Assert.Equal(name, item.Name);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(item.Source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous
                    ? await script.RunAsync(new MockLythonHost())
                    : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(item.Output, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationInsideMatchedKeyEqualityStopsTheCommandAndAllowsReuse(bool asynchronous)
    {
        const string source = """
            class Key:
                def __hash__(self): return hash('x')
                def __eq__(self, other):
                    print('eq-check', end='')
                    return other == 'x'
            key = Key()
            data = {key: 1}
            data['x'] = 2
            print('done')
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            var host = new MockLythonHost { OnStandardOutputWrite = () => cancellation.Cancel() };
            var options = new LythonRunOptions { CancellationToken = cancellation.Token };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("eq-check", host.CapturedStandardOutput());
                Assert.DoesNotContain("done", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var healthy = asynchronous
                ? await script.RunAsync(new MockLythonHost())
                : script.Run(new MockLythonHost()))
            {
                Assert.True(healthy.Success, healthy.Failure?.Message);
                Assert.Equal("eq-checkdone\n", healthy.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedCandidateStorageKeepsTheWriteOriginAndCannotBeSuppressed(bool asynchronous)
    {
        const string source = """
            native = 'x'
            class Key:
                def __hash__(self): return hash(native)
                def __eq__(self, other):
                    if isinstance(other, Key): return self is other
                    print('eq-effect', end='')
                    return other == native
            data = {Key(): 1 for _ in range(16)}
            print('armed', end='')
            try:
                data[native] = 2
            except MemoryError:
                print('caught', end='')
            finally:
                return 0
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            ILythonMemoryReservation? pin = null;
            var host = new MockLythonHost();
            host.OnStandardOutputWrite = () =>
            {
                if (pin is null)
                    Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out pin));
            };
            try
            {
                using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
                Assert.NotNull(pin);
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                var assignmentLine = Array.FindIndex(source.Split('\n'), line => line.Contains("data[native] = 2")) + 1;
                Assert.Equal(assignmentLine, result.Failure?.Span?.Line);
                Assert.Equal("armed", host.CapturedStandardOutput());
            }
            finally { pin?.Dispose(); }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var healthy = new LythonEngine().Run("return 42", new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = budget }))
                Assert.True(healthy.Success, healthy.Failure?.Message);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
