using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonEncoderIterationLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RepeatedExactBuiltinDumpsReleasePrivateTraversal(bool asynchronous, bool chooseOptions)
    {
        var budget = new LythonMemoryBudget(96 * 1024);
        using (var script = new LythonEngine().Compile($$"""
            import json
            with open('/output', 'w') as target:
                for i in range(1024):
                    json.dump(0, target{{(chooseOptions ? ", sort_keys=True" : "")}})
            with open('/output', encoding='utf-8') as source:
                return source.read()
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new string('0', 1024), result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFileStagesCompletedChunksBeforeTheNextDefaultCallback(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            import json
            offsets = []
            def fallback(value):
                offsets.append(target.tell())
                if len(offsets) == 1:
                    return 'kept'
                raise TypeError('stop encoding')
            with open('/output', 'w', encoding='utf-8', newline='') as target:
                try:
                    json.dump([1, {2}, {3}], target, default=fallback)
                except TypeError as error:
                    assert str(error) == 'stop encoding'
                else:
                    raise AssertionError('missing encoding error')
            assert len(offsets) == 2 and offsets[0] >= 2 and offsets[1] >= 10, repr(offsets)
            with open('/output', encoding='utf-8') as source:
                return source.read()
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new DelayedLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Contains(Assert.IsType<string>(result.ReturnValue), new[] { "[1, \"kept\"", "[1, \"kept\", " });
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeWriterFailureLeavesCapturedEncoderResumable(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            import json
            saved = None
            class Encoder(json.JSONEncoder):
                def iterencode(self, value):
                    global saved
                    saved = super().iterencode(value)
                    return saved
            with open('/output', 'w') as target:
                pass
            try:
                json.dump([1, 2], target, cls=Encoder)
            except ValueError:
                pass
            else:
                raise AssertionError('closed writer accepted output')
            tail = list(saved)
            assert tail and ''.join(tail).endswith(']')
            assert next(saved, 'finished') == 'finished'
            return 'ok'
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok", result.ReturnValue);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReentrantDefaultCannotAdvanceTheActiveIterator(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            import json
            seen = []
            def fallback(value):
                with open('/value') as source:
                    text = source.read()
                try:
                    next(chunks)
                except ValueError as error:
                    seen.append(str(error))
                return text
            chunks = json.JSONEncoder(default=fallback).iterencode([{1}, 2])
            first = next(chunks)
            assert first == '[' and not seen
            parts = [first] + list(chunks)
            assert ''.join(parts) == '["v", 2]', repr((parts, seen))
            assert seen == ['generator already executing'], repr((parts, seen))
            return ''.join(parts)
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            host.SeedFile("/value", "v");
            delayed.SeedFile("/value", "v");
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("[\"v\", 2]", result.ReturnValue);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DictionaryEncoderChecksLiveItemsAfterDefaultCallback(bool asynchronous, bool eager)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile($$"""
            import json
            def encode(change_size):
                record = {'first': {1}, 'next': 1}
                def fallback(value):
                    with open('/value') as source:
                        text = source.read()
                    if change_size:
                        record['new'] = 1
                    else:
                        record['next'] = 2
                    return text
                return {{(eager ? "json.dumps(record, default=fallback)" : "''.join(json.JSONEncoder(default=fallback).iterencode(record))")}}
            assert encode(False) == '{"first": "v", "next": 2}'
            if {{(eager ? "False" : "True")}}:
                try:
                    encode(True)
                except RuntimeError as error:
                    assert 'dictionary changed size' in str(error)
                else:
                    raise AssertionError('encoder accepted a change to the key set')
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            var delayed = new DelayedLythonHost();
            host.SeedFile("/value", "v");
            delayed.SeedFile("/value", "v");
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlierChunksRemainIndependentAcrossLargeAndSmallValues(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            import json
            values = ['a' * 4097, 'b', '\u03b1' * 4097]
            chunks = json.JSONEncoder(ensure_ascii=False).iterencode(values)
            parts = list(chunks)
            assert ''.join(parts) == '["' + values[0] + '", "b", "' + values[2] + '"]'
            assert json.loads(''.join(parts)) == values
            assert next(chunks, 'finished') == 'finished'
            return 'ok'
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok", result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
