using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorRecordLookupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedFieldsUseCurrentRowsAndReturnFunding(bool asynchronous)
    {
        const string source = """
            def record(value): return {'value':value,'other':None}
            def current(row):
                with open('check.txt') as source: assert source.read() == 'ready'
                return row
            marker=['retained']
            rows=[record(marker),record(2),record(3),record(4)]
            values=(current(row)['value'] for row in rows)
            interleaved=(current(row)['value'] for row in rows)
            assert next(values) is marker
            changed=['changed']
            rows[1]['value']=changed
            assert next(values) is changed
            rows[2]['inserted']=True
            del rows[2]['value']
            rows[2]['value']=marker
            assert next(values) is marker
            assert next(interleaved) is marker
            assert next(interleaved) is changed
            interleaved.close()
            rows[3].clear()
            try: next(values)
            except KeyError: pass
            else: assert False
            values.close()
            class Key:
                def __hash__(self): return hash('value')
                def __eq__(self, other): return other == 'value'
            protocol={Key():changed}
            fallback=(current(row)['value'] for row in [record(marker),protocol])
            assert next(fallback) is marker
            assert next(fallback) is changed
            fallback.close()
            return 'ok'
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var run = 0; run < 2; run++)
            {
                var immediate = new MockLythonHost();
                var delayed = new DelayedLythonHost();
                immediate.SeedFile("/check.txt", "ready");
                delayed.SeedFile("/check.txt", "ready");
                using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(immediate))
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
    public async Task RefusalDuringRetainedGeneratorCannotContinueGuestWork(bool asynchronous)
    {
        const string source = """
            import time
            def record(): return {'value':'kept'}
            rows=[record(),record()]
            values=(row['value'] for row in rows)
            time.time()
            try:
                next(values)
                print('continued')
            except MemoryError:
                print('suppressed')
            finally:
                print('finally')
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 0);
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }
}
