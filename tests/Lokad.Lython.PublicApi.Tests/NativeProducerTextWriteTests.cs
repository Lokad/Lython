using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeProducerTextWriteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CodecFailureOwnsItsInputAndLeavesStagingUnchanged(bool asynchronous)
    {
        await AssertBothOwnershipAndOutput("""
            import csv, json
            with open('csv-output', 'w', encoding='ascii', newline='') as target:
                try:
                    csv.writer(target).writerow(['a', 'α😀'])
                except UnicodeEncodeError as error:
                    assert error.object == 'a,α😀\r\n'
                    assert (error.encoding, error.start, error.end) == ('ascii', 2, 4)
                    kept = error.object
                else:
                    raise AssertionError('expected CSV encoding failure')
                target.write('safe')
            with open('json-output', 'w', encoding='ascii', newline='') as target:
                try:
                    json.dump('α😀', target, ensure_ascii=False)
                except UnicodeEncodeError as error:
                    assert error.object == '"α😀"'
                    assert (error.encoding, error.start, error.end) == ('ascii', 1, 3)
                else:
                    raise AssertionError('expected JSON encoding failure')
                target.write('safe')
            assert kept == 'a,α😀\r\n'
            for path in ['csv-output', 'json-output']:
                with open(path) as source:
                    assert source.read() == 'safe'
            return 'ok'
            """, asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeProducersPreserveScalarCountsNewlinesAndBom(bool asynchronous)
    {
        await AssertBothOwnershipAndOutput("""
            import csv, json
            for encoding in ['utf-8', 'utf-8-sig', 'utf-16', 'utf-16-le']:
                with open('csv-output', 'w', encoding=encoding, newline='\r\n') as target:
                    assert csv.writer(target, lineterminator='\n').writerow(['é\n😀']) == 6
                with open('csv-output', 'rb') as source:
                    assert source.read() == '"é\r\n😀"\r\n'.encode(encoding)
                with open('json-output', 'w', encoding=encoding, newline='\r\n') as target:
                    json.dump(['é😀'], target, ensure_ascii=False, indent=2)
                with open('json-output', 'rb') as source:
                    assert source.read() == '[\r\n  "é😀"\r\n]'.encode(encoding)
            return 'ok'
            """, asynchronous);
    }

    private static async Task AssertBothOwnershipAndOutput(string source, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var delayed = new DelayedLythonHost();
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok", result.ReturnValue);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
