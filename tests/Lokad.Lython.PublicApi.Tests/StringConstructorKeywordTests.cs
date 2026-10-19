using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringConstructorKeywordTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "empty", "try:\n value=str()\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "integer-object", "try:\n value=str(object=12)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok '12'\n" },
        { "string-object", "try:\n value=str(object='text')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok 'text'\n" },
        { "none-object", "try:\n value=str(object=None)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok 'None'\n" },
        { "codec-object", "try:\n value=str(object=b'\\xc3\\xa9',encoding='utf-8')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok '\u00e9'\n" },
        { "encoding-only", "try:\n value=str(encoding='utf-8')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "errors-only", "try:\n value=str(errors='ignore')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "unknown-encoding-no-object", "try:\n value=str(encoding='missing-codec')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "unknown-errors-no-object", "try:\n value=str(errors='missing-handler')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "bad-encoding-no-object", "try:\n value=str(encoding=12)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "none-errors-no-object", "try:\n value=str(errors=None)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "none-object-with-codec", "try:\n value=str(object=None,encoding='utf-8')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "string-object-with-codec", "try:\n value=str(object='text',encoding='utf-8')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "ignore-with-default-encoding", "try:\n value=str(b'\\xff',errors='ignore')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "keyword-ignore-default-encoding", "try:\n value=str(object=b'\\xff',errors='ignore')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "keyword-replace-default-encoding", "try:\n value=str(object=b'\\xff',errors='replace')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok '\ufffd'\n" },
        { "all-keywords", "try:\n value=str(object=b'plain',encoding='utf-8',errors='strict')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok 'plain'\n" },
        { "duplicate-object", "try:\n value=str(12,object=23)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "duplicate-encoding", "try:\n value=str(b'plain','utf-8',encoding='ascii')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "bad-codec-first", "try:\n value=str(object=12,encoding=None)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "bad-errors-first", "try:\n value=str(object='text',errors=12)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "bad-both-no-object", "try:\n value=str(encoding=None,errors=12)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "unknown-codec-empty", "try:\n value=str(object=b'',encoding='missing-codec')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "unknown-errors-empty", "try:\n value=str(object=b'',errors='missing-handler')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "ok ''\n" },
        { "unexpected-name", "try:\n value=str(unknown=1)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "too-many", "try:\n value=str(1,2,3,4)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "duplicate-expanded-object", "try:\n value=str(object=12,**{'object':13})\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "positional-none-codec", "try:\n value=str(None,'utf-8')\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "positional-bad-encoding", "try:\n value=str(12,encoding=None)\n print('ok',repr(value))\nexcept Exception as error:\n print(type(error).__name__)", "TypeError\n" },
        { "argument-order", "events=[]\ndef mark(label,value):\n events.append(label)\n return value\ntry:str(object=mark('object',12),encoding=mark('encoding',None),errors=mark('errors',12))\nexcept TypeError:print(events)", "['object', 'encoding', 'errors']\n" },
        { "alias", "constructor=str\nprint(constructor(object=12))", "12\n" },
        { "expanded", "options={'object':b'\\xff','errors':'replace'}\nprint(str(**options))", "\ufffd\n" },
        { "imported-alias", "from builtins import str as constructor\nprint(constructor(object=None))", "None\n" },
        { "identity-and-type", "value='caf\u00e9 \ud83d\ude00'\nprint(str(object=value) is value,str.__name__,str.__module__,isinstance(str(object=12),str))", "True str builtins True\n" },
        { "new-slot", "print(str.__new__(str,object=12))", "12\n" },
        { "type-metadata", "first=str.__mro__\nprint(first is str.__mro__,[t.__name__ for t in first],type(str).__name__)", "True ['str', 'object'] type\n" },
        { "custom", "events=[]\nclass Value:\n def __str__(self):events.append('str');return 'custom'\nprint(str(object=Value()),events)", "custom ['str']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task KeywordDefaultsAndErrorsMatchPython(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, label + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task KeywordObjectAwaitsDescriptorAndBodyWithoutLosingOwners(bool descriptor, bool cancel)
    {
        var original = StringProtocolTests.IoSource("__str__", "str", descriptor);
        var source = original.Replace("str(Value())", "str(object=Value())", StringComparison.Ordinal);
        Assert.NotEqual(original, source);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/call.txt", "before"); host.SeedFile("/bind.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation(descriptor ? "/bind.txt" : "/call.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Same(entered, first);
                Assert.False(pending.IsCompleted); Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(cancel ? "" : "rendered\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforecall", host.ReadText("/call.txt"));
            Assert.Equal(!cancel && descriptor ? "beforebind" : "before", host.ReadText("/bind.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }
}
