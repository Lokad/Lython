using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HashReadAsyncTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var expression in new[] { "hash(key)", "hash((key,))", "(key,).__hash__()", "tuple.__hash__((key,))",
            "hash(slice(key, 3))", "slice(key, 3).__hash__()", "hash(Row(key))" })
            yield return [expression, "isinstance(result, int)"];
        foreach (var expression in new[] { "{}.get((key,), 'kept')", "globals().get((key,), 'kept')", "mapping[(key,)]", "mapping.get((key,))", "read((key,))", "extract(mapping, key)" })
            yield return [expression, "result == 'kept'"];
        foreach (var expression in new[] { "(key,) in mapping", "(key,) in members", "(key,) in mapping.keys()", "((key,), 'kept') in mapping.items()" })
            yield return [expression, "result is True"];
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task DescriptorAndHashBodyGenuinelySuspend(string expression, string assertion)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source + "\nresult = " + expression + "\nassert " + assertion + "\nprint('ok')", budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/bind", "data");
            host.SeedFile("/call", "data");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, expression + ": " + result.Failure?.Message);
                Assert.Equal("descriptor\ncall\nok\n", result.StandardOutput);
                Assert.True(host.CompletedAsynchronously >= 2);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("/bind")]
    [InlineData("/call")]
    public async Task CancellationWaitsForDescriptorOrBodyCleanup(string path)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Source + "\nprint(mapping[(key,)])", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/bind", "data");
        host.SeedFile("/call", "data");
        var entered = host.PauseReadUntilCancellation(path);
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(entered, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal(path == "/bind" ? "descriptor\n" : "descriptor\ncall\n", result.StandardOutput);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task LoweredClassBodiesAwaitKeyProtocols()
    {
        await AssertOutputAsync(Source + "\nclass Probe:\n result=mapping[(key,)]\nprint(Probe.result)", "descriptor\ncall\nkept\n");
    }

    [Fact]
    public async Task GeneratedHashReadsAllFieldsBeforeHashingAfterSuspension()
    {
        await AssertOutputAsync("""
            from dataclasses import dataclass
            events=[]
            class Key:
             def __init__(self,name):self.name=name
             def __hash__(self):
              events.append('hash:'+self.name)
              with open('/call') as marker:marker.read()
              return 17
            @dataclass(unsafe_hash=True)
            class Row:
             a:object
             b:object
             def __getattribute__(self,name):
              if name in ('a','b'):
               events.append('read:'+name)
               with open('/bind') as marker:marker.read()
              return object.__getattribute__(self,name)
            hash(Row(Key('a'),Key('b')))
            print(events)
            """, "['read:a', 'read:b', 'hash:a', 'hash:b']\n");
    }

    [Fact]
    public async Task CycleAfterSuspensionUnwindsBeforeTheNextHash()
    {
        await AssertOutputAsync("""
            class Key:
             def __hash__(self):
              with open('/call') as marker:marker.read()
              return hash(holder)
            holder=(Key(),)
            try:hash(holder)
            except RecursionError:print('cycle')
            print(hash((1,2))==(1,2).__hash__())
            """, "cycle\nTrue\n");
    }

    [Fact]
    public async Task EqualityAfterPendingHashRevalidatesClearedEntries()
    {
        await AssertOutputAsync("""
            armed=False
            class Key:
             def __hash__(self):
              if armed:
               with open('/bind') as marker:marker.read()
              return 17
             def __eq__(self,other):
              with open('/call') as marker:marker.read()
              mapping.clear()
              return True
            mapping={Key():'retained'}
            armed=True
            print(mapping.get(Key(),'missing'),len(mapping))
            """, "missing 0\n");
    }

    [Theory]
    [InlineData("mapping.clear()", "missing 0\n")]
    [InlineData("del mapping['beta']", "retained 1\n")]
    public async Task SharedRecordEqualityRevalidatesEntriesAfterSuspension(string mutation, string expected)
    {
        await AssertOutputAsync(SharedEqualitySource.Replace("MUTATE", mutation)
            + "\nprint(mapping.get(Key(), 'missing'), len(mapping))", expected);
    }

    [Fact]
    public async Task SharedRecordCancellationAwaitsEqualityCleanup()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(SharedEqualitySource.Replace("MUTATE", "mapping.clear()")
            + "\ntry:\n mapping.get(Key())\nfinally:\n return 'must not hide cancellation'", budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/call", "data");
        var entered = host.PauseReadUntilCancellation("/call");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(entered, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string SharedEqualitySource = """
        import json
        mapping = json.loads('{"alpha":"retained","beta":"other"}')
        class Key:
            def __hash__(self): return hash('alpha')
            def __eq__(self, other):
                with open('/call') as marker: marker.read()
                MUTATE
                return other == 'alpha'
        """;

    private static async Task AssertOutputAsync(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/bind", "data");
            host.SeedFile("/call", "data");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string Source = """
        from dataclasses import dataclass
        armed = False
        class Key:
            def __getattribute__(self, name):
                raise RuntimeError('ordinary lookup')
            @property
            def __hash__(self):
                if armed:
                    print('descriptor')
                    with open('/bind') as marker: marker.read()
                def hash_value():
                    if armed:
                        print('call')
                        with open('/call') as marker: marker.read()
                    return 17
                return hash_value
        @dataclass(unsafe_hash=True)
        class Row:
            value: object
        def extract(mapping, key): return mapping.get((key,))
        key = Key()
        mapping = {(key,): 'kept'}
        members = {(key,)}
        read = mapping.get
        armed = True
        """;
}
