using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class WhitespaceSplitBoundaryTests
{
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public async Task LongMixedWordsAndWhitespaceKeepExactCountedRemainders(int size)
    {
        using var script = new LythonEngine().Compile($$"""
            left = 'a' * {{size}} + '\u03b1\0\x7f\U0001f600'
            space = '\u2003' * {{size}}
            text = ' \t' + left + space + 'tail \t'
            assert text.split() == [left, 'tail']
            assert text.split(None, 0) == [left + space + 'tail \t']
            assert text.split(None, 1) == [left, 'tail \t']
            assert text.split(None, 2) == [left, 'tail']
            assert text.split(None, 2147483647) == [left, 'tail']
            assert (' \t' * {{size}}).split() == []
            assert text.rsplit() == [left, 'tail']
            assert text.rsplit(None, 0) == [' \t' + left + space + 'tail']
            assert text.rsplit(None, 1) == [' \t' + left, 'tail']
            assert text.rsplit(None, 2) == [left, 'tail']
            assert text.rsplit(None, 2147483647) == [left, 'tail']
            assert (' \t' * {{size}}).rsplit() == []
            ascii_text = ''.join(chr(i) for i in range(128))
            words = [ascii_text[:9], ascii_text[14:28], ascii_text[33:]]
            assert ascii_text.split() == words
            assert ascii_text.rsplit() == words
            assert ascii_text.lower() == ascii_text[:65] + 'abcdefghijklmnopqrstuvwxyz' + ascii_text[91:]
            assert ascii_text.upper() == ascii_text[:97] + 'ABCDEFGHIJKLMNOPQRSTUVWXYZ' + ascii_text[123:]
            def lower_words(value):return value.lower().split()
            def upper_words(value):return value.upper().split()
            cased = 'A' * {{size}} + 'ΟΣ’' + '\u2003' + 'İßﬃ'
            assert lower_words(cased) == ['a' * {{size}} + 'ος’', 'i\u0307ßﬃ']
            assert upper_words(cased) == ['A' * {{size}} + 'ΟΣ’', 'İSSFFI']
            print('ok')
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
