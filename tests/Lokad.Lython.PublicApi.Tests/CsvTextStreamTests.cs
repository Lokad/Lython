using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvTextStreamTests
{
    [Fact]
    public async Task WriterCapturesTheMethodAndReturnsItsValue()
    {
        await AssertBothModes("""
            import csv
            class Writer:
                def write(self, text):
                    print(repr(text))
                    return 42
            stream = Writer()
            writer = csv.writer(stream, lineterminator='\n')
            stream.write = lambda text: print('new')
            print(writer.writerow([1, 2]) + 1)
            print(writer.writerows([[3], [4]]))
            dictionary = csv.DictWriter(Writer(), ['a'], lineterminator='\n')
            print(dictionary.writeheader() + 1)
            print(dictionary.writerow({'a': 5}) + 1)
            print(dictionary.writerows([{'a': 6}]))
            """, "'1,2\\n'\n43\n'3\\n'\n'4\\n'\nNone\n'a\\n'\n43\n'5\\n'\n43\n'6\\n'\nNone\n");
    }

    [Fact]
    public async Task WriteResultsRetainIdentityAndNeedNotBeNumbers()
    {
        await AssertBothModes("""
            import csv
            result = {'value': 'kept'}
            class Writer:
                def write(self, text):
                    return result
            writer = csv.writer(Writer())
            dictionary = csv.DictWriter(Writer(), ['a'])
            print(writer.writerow([1]) is result)
            print(dictionary.writeheader() is result)
            print(dictionary.writerow({'a': 2})['value'])
            print(writer.writerows([[3]]) is None)
            print(dictionary.writerows([{'a': 4}]) is None)
            """, "True\nTrue\nkept\nTrue\nTrue\n");
    }

    [Fact]
    public async Task ConstructorLooksUpWriteOnceBeforeRowsAreConsumed()
    {
        await AssertBothModes("""
            import csv
            events = []
            class Writer:
                @property
                def write(self):
                    events.append('lookup')
                    return self.publish
                def publish(self, text):
                    events.append(text)
                    return None
            def rows():
                events.append('pull')
                yield [1]
                yield [2]
            writer = csv.writer(Writer(), lineterminator='\n')
            print(events)
            print(writer.writerows(rows()))
            print(events)
            """, "['lookup']\nNone\n['lookup', 'pull', '1\\n', '2\\n']\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConstructorRejectsMissingAndNoncallableWrite(bool dictionary)
    {
        var constructor = dictionary ? "csv.DictWriter(value, ['a'])" : "csv.writer(value)";
        await AssertBothModes("""
            import csv
            class Missing:
                pass
            class Bad:
                write = 7
            class OwnCall:
                pass
            own = OwnCall()
            own.__call__ = lambda text: 1
            class InstanceWrite:
                write = own
            for value in [Missing(), Bad(), InstanceWrite(), 1]:
                try:
            """ + "\n        " + constructor + "\n    except TypeError:\n        print('bad')", "bad\nbad\nbad\nbad\n");
    }

    [Theory]
    [InlineData("AttributeError", "TypeError")]
    [InlineData("ValueError", "ValueError")]
    public async Task GetterFailuresFollowTheConstructorContract(string thrown, string caught)
    {
        await AssertBothModes("import csv\nclass Writer:\n    @property\n    def write(self):\n        raise " + thrown +
            "('getter')\nfor make in [lambda: csv.writer(Writer()), lambda: csv.DictWriter(Writer(), ['a'])]:\n" +
            "    try:\n        make()\n    except " + caught + ":\n        print('caught')", "caught\ncaught\n");
    }

    [Fact]
    public async Task CallableWriteObjectsUseTheTypeSlot()
    {
        await AssertBothModes("""
            import csv
            class Callback:
                __call__ = lambda self, text: ('type', text)
            class Writer:
                write = Callback()
            stream = Writer()
            stream.write.__call__ = lambda text: 'own'
            writer = csv.writer(stream, lineterminator='\n')
            print(writer.writerow([1]))
            Callback.__call__ = lambda self, text: ('new', text)
            print(writer.writerow([2]))
            """, "('type', '1\\n')\n('new', '2\\n')\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackFailureStopsRowPullsAndKeepsThePrefix(bool dictionary)
    {
        var make = dictionary ? "csv.DictWriter(Writer(), ['a'], lineterminator='\\n')" : "csv.writer(Writer(), lineterminator='\\n')";
        var row = dictionary ? "{'a': n}" : "[n]";
        await AssertBothModes("""
            import csv
            events = []
            chunks = []
            class Writer:
                def write(self, text):
                    events.append('write:' + text.strip())
                    if text.startswith('2'):
                        raise ValueError('stop')
                    chunks.append(text)
                    return object()
            def rows():
                for n in [1, 2, 3]:
                    events.append('pull:' + str(n))
            """ + "\n        yield " + row + "\ntry:\n    " + make + ".writerows(rows())\nexcept ValueError:\n" +
            "    print(events)\n    print(repr(''.join(chunks)))",
            "['pull:1', 'write:1', 'pull:2', 'write:2']\n'1\\n'\n");
    }

    [Fact]
    public async Task StandardStreamsReceiveRowsAndRemainWritable()
    {
        var script = Compile("""
            import csv
            import sys
            print(csv.writer(sys.stdout, lineterminator='\n').writerow(['é', '😀']))
            print(csv.DictWriter(sys.stderr, ['a'], lineterminator='\n').writeheader())
            sys.stdout.write('still open\n')
            sys.stderr.write('still open\n')
            """);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("é,😀\n4\n2\nstill open\n", result.StandardOutput);
            Assert.Equal("a\nstill open\n", result.StandardError);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncWritersAwaitLookupCallbacksAndRowAcquisition(bool dictionary)
    {
        var body = dictionary
            ? "writer.writeheader()\nwriter.writerows(rows())"
            : "writer.writerow(cells())\nwriter.writerows(rows())";
        var make = dictionary ? "csv.DictWriter(Writer(), ['a'], lineterminator='\\n')" : "csv.writer(Writer(), lineterminator='\\n')";
        var row = dictionary ? "{'a': value}" : "cells()";
        var script = Compile("""
            import csv
            class Writer:
                @property
                def write(self):
                    with open('/marker') as marker:
                        marker.read()
                    return self.publish
                def publish(self, text):
                    with open('/out', 'a') as handle:
                        handle.write(text)
                    return len(text)
            def cells():
                with open('/marker') as marker:
                    value = marker.read()
                yield value
            def rows():
                for i in range(2):
                    with open('/marker') as marker:
                        value = marker.read()
            """ + "\n        yield " + row + "\nwriter = " + make + "\n" + body);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/marker", "é😀");
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayedHost = new DelayedLythonHost();
        delayedHost.SeedFile("/marker", "é😀");
        var asynchronous = await script.RunAsync(delayedHost);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(dictionary ? "a\né😀\né😀\n" : "é😀\né😀\né😀\n", syncHost.ReadText("/out"));
        Assert.Equal(syncHost.ReadText("/out"), delayedHost.ReadText("/out"));
        Assert.True(delayedHost.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("lookup", false)]
    [InlineData("row", false)]
    [InlineData("write", false)]
    [InlineData("write", true)]
    public async Task AsyncWriterHonorsInFlightCancellation(string stage, bool dictionary)
    {
        var getter = stage == "lookup" ? "with open('/marker') as handle:\n            handle.read()\n        " : "";
        var row = stage == "row" ? "with open('/marker') as handle:\n        handle.read()\n    " : "";
        var maker = dictionary ? "csv.DictWriter(Writer(), ['a'])" : "csv.writer(Writer())";
        var call = dictionary ? "writer.writeheader()" : "writer.writerow(cells())";
        var script = Compile("import csv\nclass Writer:\n    @property\n    def write(self):\n        " + getter +
            "return self.publish\n    def publish(self, text):\n        with open('/out', 'w') as handle:\n" +
            "            handle.write(text)\ndef cells():\n    " + row + "yield 'x'\nwriter = " + maker + "\n" + call);
        var host = new DelayedLythonHost();
        host.SeedFile("/marker", "ok");
        var started = stage == "write" ? host.PauseWriteUntilCancellation("/out") : host.PauseReadUntilCancellation("/marker");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
        }
        finally
        {
            cancellation.Cancel();
        }
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message ?? "");
    }

    [Fact]
    public async Task DictWriterConsumesFieldNamesBeforeCapturingWrite()
    {
        await AssertBothModes("""
            import csv
            events = []
            def fields():
                events.append('fields')
                yield 'a'
            class Writer:
                @property
                def write(self):
                    events.append('lookup')
                    return lambda text: events.append(text)
            writer = csv.DictWriter(Writer(), fields(), lineterminator='\n')
            writer.writeheader()
            print(events)
            """, "['fields', 'lookup', 'a\\n']\n");
    }

    [Fact]
    public async Task DictWriterAwaitsFieldNameAcquisition()
    {
        var script = Compile("""
            import csv
            import sys
            def fields():
                with open('/marker') as marker:
                    yield marker.read()
            csv.DictWriter(sys.stdout, fields(), lineterminator='\n').writeheader()
            """);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/marker", "a");
        var synchronous = script.Run(syncHost);
        Assert.True(synchronous.Success, synchronous.Failure?.Message);
        var delayedHost = new DelayedLythonHost();
        delayedHost.SeedFile("/marker", "a");
        var asynchronous = await script.RunAsync(delayedHost);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal("a\n", synchronous.StandardOutput);
        Assert.Equal(synchronous.StandardOutput, asynchronous.StandardOutput);
        Assert.True(delayedHost.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task RetainedRowStringsRemainFundedAndUnchanged()
    {
        var script = Compile("""
            import csv
            chunks = []
            class Writer:
                def write(self, text):
                    chunks.append(text)
            writer = csv.writer(Writer(), lineterminator='\n')
            for i in range(1000):
                try:
                    writer.writerow(['x' * 4096])
                except MemoryError:
                    print('denied', len(chunks) > 0, chunks[0] == 'x' * 4096 + '\n')
                    break
            else:
                print('unfunded')
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("denied True True\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task WideGeneratedRowsAreDeniedBeforeTheCallbackAndAllowLaterWrites()
    {
        var script = Compile("""
            import csv
            class Writer:
                def __init__(self):
                    self.count = 0
                def write(self, text):
                    self.count += 1
            stream = Writer()
            writer = csv.writer(stream)
            def cells():
                for i in range(100000):
                    yield None
            try:
                writer.writerow(cells())
            except MemoryError:
                print('denied', stream.count)
            writer.writerow([1])
            print('later', stream.count)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("denied 0\nlater 1\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task StandardOutputCapsStillRejectAnOversizedRowBeforePublication()
    {
        var script = Compile("import csv\nimport sys\ncsv.writer(sys.stdout).writerow(['long'])");
        var options = new LythonRunOptions { MaxStandardOutputBytes = 3 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
        }
    }

    [Fact]
    public async Task FailedCallbacksReclaimDiscardedRowStrings()
    {
        var script = Compile("""
            import csv
            class Writer:
                def write(self, text):
                    raise ValueError('stop')
            writer = csv.writer(Writer())
            value = 'x' * 4096
            for i in range(2000):
                try:
                    writer.writerow([value])
                except ValueError:
                    pass
            print('done')
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("done\n", result.StandardOutput);
        }
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }

    private static async Task AssertBothModes(string source, string expected)
    {
        var script = Compile(source);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
