using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvNativeInputTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task QuotedEofPublishesTheLastRowUnlessStrict(bool asynchronous, bool nativeFile, bool strict)
    {
        var input = nativeFile ? "source" : "['a,\"first\\n', 'last']";
        var source = "with open('/input.csv', newline='') as source:\n" +
            $"    import csv\n    reader = csv.reader({input}, strict={(strict ? "True" : "False")})\n" + """
                try:
                    row = next(reader)
                    print(row, reader.line_num)
                except csv.Error as error:
                    print(str(error), reader.line_num)
                print(next(reader, None) is None)
            """ + (strict ? "" : "\n    return row");
        var expected = strict ? "unexpected end of data 2\nTrue\n" : "['a', 'first\\nlast'] 2\nTrue\n";
        await AssertFile(asynchronous, "a,\"first\nlast", source, expected,
            expectedReturn: strict ? null : new object?[] { "a", "first\nlast" });
    }

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
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task NumericFailureConsumesOnePhysicalLineAndKeepsTheNextRefill(bool asynchronous, bool translate)
    {
        var content = "1,2\r\n3,bad,\"unfinished\r\nskip,me\r\n4,1e2\r\n5,\"" +
            new string('x', 18000) + "\",6\r\n7,8\r\n";
        var newline = translate ? "None" : "''";
        await AssertFile(asynchronous, content, $$"""
            import csv
            with open('/input.csv', newline={{newline}}) as source:
                reader = csv.reader(source, quoting=csv.QUOTE_NONNUMERIC, strict=True)
                saved = next(reader)
                try:
                    next(reader)
                except ValueError:
                    print('bad number', reader.line_num)
                print(repr(source.readline()))
                print(next(reader), reader.line_num)
                number, text, tail = next(reader)
                print(number, len(text), tail, reader.line_num)
                print(next(reader), reader.line_num, saved)
            """, "bad number 2\n" + (translate ? "'skip,me\\n'\n" : "'skip,me\\r\\n'\n") +
            "[4.0, 100.0] 3\n5.0 18000 6.0 4\n[7.0, 8.0] 5 [1.0, 2.0]\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReboundFloatCanObserveOriginalFieldsAcrossFailedConversion(bool asynchronous)
    {
        await AssertFile(asynchronous, "SITE,1.0,bad,2.0,alpha\nSITE,3.0,4.0,5.0,beta\n", """
            import csv
            original_float = float
            observed = []
            def convert(value):
                observed.append(value)
                current = globals()
                print(value is current['kwh'] or value is current['peak'] or value is current['price'])
                return original_float(value)
            float = convert
            with open('/input.csv', encoding='utf-8', newline='') as source:
                for site, kwh, peak, price, note in csv.reader(source):
                    try:
                        kwh, peak, price = float(kwh), float(peak), float(price)
                    except ValueError:
                        print(kwh is observed[0], peak is observed[1], price, note)
                    else:
                        print(kwh, peak, price, note)
            print(observed)
            """, """
            True
            True
            True True 2.0 alpha
            True
            True
            True
            3.0 4.0 5.0 beta
            ['1.0', 'bad', '3.0', '4.0', '5.0']
            """.ReplaceLineEndings("\n") + "\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShortCsvDictionaryKeysKeepTheFirstInsertedIdentity(bool asynchronous)
    {
        await AssertFile(asynchronous, "é1,1\né1,2\na,3\nb,4\nc,5\nd,6\né1,7\n", """
            import csv
            counts = {}
            previous_keys = []
            with open('/input.csv', encoding='utf-8', newline='') as source:
                for key, amount in csv.reader(source):
                    previous = counts.get(key, 0)
                    counts[key] = previous + int(amount)
                    previous_keys.append(key)
            first = next(iter(counts))
            print(counts[first], len(counts))
            print(first is previous_keys[0], first is previous_keys[1], first is previous_keys[-1])
            return list(counts)
            """, "10 5\nTrue False False\n", expectedReturn: ["é1", "a", "b", "c", "d"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FloatConversionOfShortFieldsRetainsCanonicalAliasesAfterFailure(bool asynchronous)
    {
        await AssertFile(asynchronous, "١.٥,bad\n٢.٥,-0.0\n", """
            import csv
            with open('/input.csv', encoding='utf-8', newline='') as source:
                for amount, tax in csv.reader(source):
                    try:
                        amount, tax = float(amount), float(tax)
                    except ValueError as error:
                        print(str(error))
                        aliases = (amount, tax)
                        print(amount, tax, aliases[0] is amount, aliases[1] is tax)
                    else:
                        print(amount, tax)
            print(aliases)
            """, """
            could not convert string to float: 'bad'
            ١.٥ bad True True
            2.5 -0.0
            ('١.٥', 'bad')
            """.ReplaceLineEndings("\n") + "\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedNativeRowsSurviveQuotesAndChangesInWidth(bool asynchronous)
    {
        await AssertFile(asynchronous,
            "first,a\nprefix-é,\"two\npieces\",z\none,two,three,four,five,six\na,\"b,c\",d,e,f,g\r\nbare\"quote,,last\n,\"quoted,comma\",tail,\n\nfinal,", """
            import csv
            with open('/input.csv', newline='') as source:
                reader = csv.reader(source)
                saved = None
                for row in reader:
                    if reader.line_num == 3:
                        saved = row
                    print(row, reader.line_num)
                print(saved)
            """, """
            ['first', 'a'] 1
            ['prefix-é', 'two\npieces', 'z'] 3
            ['one', 'two', 'three', 'four', 'five', 'six'] 4
            ['a', 'b,c', 'd', 'e', 'f', 'g'] 5
            ['bare"quote', '', 'last'] 6
            ['', 'quoted,comma', 'tail', ''] 7
            [] 8
            ['final', ''] 9
            ['prefix-é', 'two\npieces', 'z']
            """.ReplaceLineEndings("\n") + "\n");
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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ClosingQuoteSuffixFollowsStrictnessAndKeepsReaderPosition(bool asynchronous, bool strict)
    {
        var script = $$"""
            import csv
            with open('/input.csv', newline='') as source:
                reader = csv.reader(source, delimiter='§', quotechar='«', escapechar='🦋', strict={{(strict ? "True" : "False")}})
                try:
                    print(next(reader), reader.line_num)
                except csv.Error as error:
                    print(str(error), reader.line_num)
                try:
                    print(next(reader), reader.line_num)
                except csv.Error as error:
                    print(str(error), reader.line_num)
                print(next(reader), reader.line_num)
            """;
        var expected = strict ? "'§' expected after '«' 1\n'§' expected after '«' 2\n['kept', '2'] 3\n"
            : "['prefix', '1'] 1\n['a🦋βγ', 'z'] 2\n['kept', '2'] 3\n";
        await AssertFile(asynchronous, "«pre«fix§1\n«a«🦋β🦋γ§z\n«kept«§2\n", script, expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedItemAndPhysicalNewlinesKeepTheRecordOpen(bool asynchronous)
    {
        await AssertFile(asynchronous, "first@\nsecond\nlast@\n", """
            import csv
            for strict in [False, True]:
                for rows in [['one@'], ['one@','next'], ['one@\n','next'], ['"one@','next"']]:
                    reader = csv.reader(rows, escapechar='@', strict=strict)
                    try:
                        print(list(reader), reader.line_num)
                    except csv.Error as error:
                        print(str(error), reader.line_num)
            with open('/input.csv', newline='') as source:
                reader = csv.reader(source, escapechar='@', strict=True)
                print(next(reader), reader.line_num)
                try:
                    next(reader)
                except csv.Error as error:
                    print(str(error), reader.line_num)
                print(next(reader, None) is None)
            """, """
            [['one\n']] 1
            [['one\nnext']] 2
            [['one\nnext']] 2
            [['one\nnext']] 2
            unexpected end of data 1
            [['one\nnext']] 2
            unexpected end of data 2
            [['one\nnext']] 2
            ['first\nsecond'] 2
            unexpected end of data 3
            True
            """.ReplaceLineEndings("\n") + "\n");
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

    private static async Task AssertFile(bool asynchronous, string content, string source, string expected, int expectedCloses = 1,
        IReadOnlyList<object?>? expectedReturn = null)
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
        if (expectedReturn is not null)
        {
            Assert.Equal(expectedReturn, Assert.IsType<List<object?>>(result.ReturnValue));
            Assert.True(budget.ReservedBytes > baseline);
        }
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
