using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvNativeInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericItemsShareTheSameSingleRecordAndErrorLifetime(bool asynchronous)
    {
        await AssertFile(asynchronous, "", """
            import csv
            items = ['a,b\n\n', '"prefix\n']
            items.append(42)
            items.extend(['tail",v\n', 'last,row\n'])
            reader = csv.reader(items)
            print(next(reader), reader.line_num)
            try:
                next(reader)
            except csv.Error:
                print('bad', reader.line_num)
            print(next(reader), reader.line_num)
            print(next(reader), reader.line_num)
            """, "['a', 'b'] 1\nbad 2\n['tail\"', 'v'] 3\n['last', 'row'] 4\n", expectedCloses: 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRowsKeepValuesAndShareTheFileCursor(bool asynchronous)
    {
        await AssertFile(asynchronous, "\"α\"\"β\",x\r\nskip,me\r\nlast,row\r\n", """
            import csv
            with open('/input.csv', newline='') as source:
                reader = csv.reader(source)
                first = next(reader)
                print(first, reader.line_num)
                print(repr(source.readline()))
                print(next(reader), reader.line_num)
                print(first)
            """, "['α\"β', 'x'] 1\n'skip,me\\r\\n'\n['last', 'row'] 2\n['α\"β', 'x']\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmbeddedRecordDelimiterRejectsTheWholeItemAndNextPullRecovers(bool asynchronous)
    {
        await AssertFile(asynchronous, "a,b\rc,d\r\nlast,row\n", """
            import csv
            with open('/input.csv', newline='\n') as source:
                reader = csv.reader(source)
                try:
                    next(reader)
                except csv.Error:
                    print('bad', reader.line_num)
                print(next(reader), reader.line_num)
            """, "bad 1\n['last', 'row'] 2\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseFailureConsumesTheLineAndDoesNotDamageTheFollowingRow(bool asynchronous)
    {
        await AssertFile(asynchronous, "\"bad\"x,1\n\"kept\",2\n", """
            import csv
            with open('/input.csv', newline='') as source:
                reader = csv.reader(source, strict=True)
                try:
                    next(reader)
                except csv.Error:
                    print('bad', reader.line_num)
                print(next(reader), reader.line_num)
            """, "bad 1\n['kept', '2'] 2\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedReadDiscardsTheUnfinishedRecord(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("""
            import csv
            with open('/input.csv', newline='') as source:
                reader = csv.reader(source)
                try:
                    next(reader)
                except OSError:
                    print('read', reader.line_num)
                row = next(reader)
                print(len(row[0]), row[0].startswith('prefix\n'), row[0].endswith('tail"'), row[1], reader.line_num)
            """, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new InterruptedFileHost(asynchronous, "\"prefix\n" + new string('x', 20000) + "tail\",v\n");
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("read 1\n18981 False True v 2\n", result.StandardOutput);
        Assert.True(host.FailedOnce);
        Assert.Equal(1, host.ClosedFileCount);
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async Task AssertFile(bool asynchronous, string content, string source, string expected, int expectedCloses = 1)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var sync = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        sync.SeedFile("/input.csv", content);
        delayed.SeedFile("/input.csv", content);
        using var result = asynchronous ? await script.RunAsync(delayed) : script.Run(sync);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
        Assert.Equal(expectedCloses, asynchronous ? delayed.ClosedFileCount : sync.ClosedFileCount);
        if (asynchronous && expectedCloses != 0) Assert.True(delayed.CompletedAsynchronously > 0);
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class InterruptedFileHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private readonly MockLythonHost? _sync;
        private readonly DelayedLythonHost? _delayed;
        private readonly ILythonHost _inner;
        public InterruptedFileHost(bool asynchronous, string content)
        {
            if (asynchronous) { _delayed = new(); _delayed.SeedFile("/input.csv", content); _inner = _delayed; }
            else { _sync = new(); _sync.SeedFile("/input.csv", content); _inner = _sync; }
        }
        public bool FailedOnce { get; private set; }
        public int ClosedFileCount => _sync?.ClosedFileCount ?? _delayed!.ClosedFileCount;
        public bool CompletesSynchronously => _sync is not null;
        public string Cwd => _inner.Cwd;
        public DateTimeOffset LocalNow => _inner.LocalNow;
        public DateTimeOffset UtcNow => _inner.UtcNow;
        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path, LythonFileOpenOptions options,
            ILythonMemoryBudget memoryBudget, CancellationToken cancellationToken)
        {
            var file = await _inner.OpenFileAsync(path, options, memoryBudget, cancellationToken);
            return new TestByteFile(file)
            {
                SizeHint = () => null,
                Read = (buffer, token) =>
                {
                    var position = file.Position;
                    if (position < 8) return file.ReadAsync(buffer[..(int)Math.Min(buffer.Length, 8 - position)], token);
                    if (position < 1032) return file.ReadAsync(buffer[..(int)Math.Min(buffer.Length, 1032 - position)], token);
                    if (!FailedOnce)
                    {
                        FailedOnce = true;
                        throw new LythonHostException(LythonHostFailureKind.Transport, "interrupted read");
                    }
                    return file.ReadAsync(buffer, token);
                }
            };
        }
        public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token) => _inner.ExistsAsync(path, token);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest path, CancellationToken token) => _inner.ListDirAsync(path, token);
        public ValueTask MkDirAsync(LythonPathRequest path, CancellationToken token) => _inner.MkDirAsync(path, token);
        public ValueTask RemoveAsync(LythonPathRequest path, CancellationToken token) => _inner.RemoveAsync(path, token);
        public ValueTask CopyAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken token) => _inner.CopyAsync(source, destination, token);
        public ValueTask MoveAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken token) => _inner.MoveAsync(source, destination, token);
        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token) => _inner.StatAsync(path, token);
    }
}
