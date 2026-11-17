using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BulkGeneratorConsumptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalInsideConsumptionCannotBeSuppressedAndTheBudgetIsReusable(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile("""
            def values():
                try:
                    for i in range(37):
                        if fail and i == 6: yield 'x' * 2000000
                        yield 'x'
                finally: return
            print('before')
            try: result = ''.join(values())
            except MemoryError: print('caught')
            print('after')
            return result
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var fail in new[] { true, false })
            {
                var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["fail"] = fail } };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.Equal(!fail, result.Success);
                    Assert.Equal(fail ? "before\n" : "before\nafter\n", result.StandardOutput);
                    if (fail) Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    else Assert.Equal(new string('x', 37), result.ReturnValue);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static TheoryData<string> Programs => new()
    {
        """
        def inner():
            for i in range(37): yield str(i)
            return 7
        def outer():
            result = yield from inner()
            yield str(result)
        assert ','.join(outer()) == ','.join([str(i) for i in range(37)] + ['7'])
        return 'ok'
        """,
        """
        def values():
            for i in range(37):
                try: next(g)
                except ValueError as error: assert str(error) == 'generator already executing'
                else: assert False
                yield str(i)
        g = values()
        assert ','.join(g) == ','.join(str(i) for i in range(37))
        assert next(g, 'end') == 'end'
        return 'ok'
        """,
        """
        def values():
            try: raise KeyError('inner')
            except KeyError as original:
                for i in range(37):
                    try: raise
                    except KeyError as error: assert error is original
                    yield 'x'
            try: raise
            except ValueError as error: assert str(error) == 'caller'
            yield '!'
        try: raise ValueError('caller')
        except ValueError as original:
            assert ''.join(values()) == 'x' * 37 + '!'
            try: raise
            except ValueError as error: assert error is original
        return 'ok'
        """,
        """
        def values():
            for i in range(9): yield i
        g = values()
        try: a, b = g
        except ValueError: pass
        else: assert False
        assert next(g) == 3
        g.close()
        return 'ok'
        """,
        """
        shared = []
        def values():
            for i in range(37):
                shared.append(i)
                yield shared
        parts = sorted(values(), key=lambda value: 0)
        assert len(parts) == 37 and len(shared) == 37
        assert all(value is shared for value in parts)
        return 'ok'
        """
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ConsumptionPreservesResumeStateAliasesAndPullBounds(string source)
    {
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
