using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ContainerDisplayPipelineTests
{
    [Fact]
    public async Task LargeCallbackDisplayPreservesValuesAndReturnsItsFunding()
    {
        var entries = string.Join(',', Enumerable.Range(0, 2049).Select(i => $"'k{i}':{i}"));
        var source = "def record(_): return {" + entries + "}\n" +
            "result=next(map(record,[None]))\nassert len(result)==2049\n" +
            "assert result['k2048']==2048\nreturn 'ok'\n";
        var budget = new LythonMemoryBudget(64 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("ok", result.ReturnValue);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapingAliasesRemainFundedAcrossLaterExecutions(bool asynchronous)
    {
        const string source = """
            def record(value): return {'first':value,'same':value}
            result=record(['x'*8192])
            assert result['first'] is result['same']
            return result
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var values = Assert.IsType<Dictionary<object, object?>>(retained.ReturnValue);
                Assert.Equal(Assert.IsType<List<object?>>(values["first"]), Assert.IsType<List<object?>>(values["same"]));
                var charged = budget.ReservedBytes;
                Assert.True(charged > baseline);
                using (var later = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(later.Success, later.Failure?.Message);
                    Assert.True(budget.ReservedBytes > charged);
                }
                Assert.Equal(charged, budget.ReservedBytes);
                Assert.Equal(new string('x', 8192), Assert.Single(Assert.IsType<List<object?>>(values["same"])));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedDisplayCannotBeSuppressedOrContinueEffects(bool asynchronous)
    {
        const string source = """
            import time
            previous={'old':'retained'}
            def record():
                try: return {'first':'value','second':'value'}
                except MemoryError: return previous
                finally: return previous
            time.time()
            result=record()
            print('must not continue')
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 64);
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    public static IEnumerable<object[]> Programs()
    {
        yield return ["aliases-collisions-and-final-storage", """
            def make(key, value):
                return {key: value, True: 'first', 1: 'last', False: value, 0: 'zero'}
            marker = ['retained']
            key = 'key' * 1000
            result = make(key, marker)
            assert list(result) == [key, True, False]
            assert next(iter(result)) is key
            assert result[key] is marker
            assert result[1] == 'last' and result[False] == 'zero'
            marker.append(result)
            assert result[key][-1] is result
            def sets(value): return {value, value, True, 1}
            assert sets(10**50) == {10**50, True}
            def lists(value): return [value, value], (value, value)
            a, b = lists(marker)
            assert a[0] is a[1] and a[0] is b[1]
            return 'ok'
            """];
        yield return ["protocol-order-and-partial-effects", """
            events=[]
            class Key:
                def __init__(self, name): self.name=name
                def __hash__(self):
                    events.append('hash'+self.name)
                    return 7
                def __eq__(self, other):
                    events.append('eq'+self.name+other.name)
                    return self.name == other.name
            def value(name):
                events.append('value'+name)
                return name
            first, second = Key('a'), Key('b')
            def mapping(a,b):
                events.append('enter')
                return {a:value('a'), b:value('b')}
            result=mapping(first,second)
            assert list(result.values()) == ['a','b']
            assert events == ['enter','valuea','valueb','hasha','hashb','eqab']
            def broken():
                events.append('broken')
                return {[]:1}
            try: broken()
            except TypeError: pass
            else: assert False
            assert events[-1] == 'broken' and events.count('broken') == 1
            return 'ok'
            """];
        yield return ["unsupported-syntax-handoff-and-cleanup", """
            events=[]
            def mapping(value):
                events.append('enter')
                if value < 0: raise ValueError('negative')
                return {'value':value,'same':value}
            marker=10**50
            result=mapping(marker)
            assert result['value'] is marker and result['same'] is marker
            try: mapping(-1)
            except ValueError as error: assert str(error) == 'negative'
            else: assert False
            def cleanup():
                total=0
                for value in range(5):
                    try:
                        if value == 1: continue
                        if value == 4: break
                        total += value
                    finally: events.append(value)
                return {'total':total}
            assert cleanup() == {'total':5}
            assert events == ['enter','enter',0,1,2,3,4]
            def nested(value):
                events.append('nested')
                def get(): return value
                return {'read':get}
            assert nested(marker)['read']() is marker
            assert events[-1] == 'nested'
            return 'ok'
            """];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task CompleteHelpersPreserveConstructionSemantics(string name, string source)
    {
        _ = name;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("ok", result.ReturnValue);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
