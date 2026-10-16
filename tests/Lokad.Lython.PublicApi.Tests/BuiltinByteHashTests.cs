using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinByteHashTests
{
    private const string Source = """
        class Key:
            def __init__(self, value):
                self.value = value
            def __hash__(self):
                return hash(self.value)
            def __eq__(self, other):
                if isinstance(other, Key):
                    return self.value == other.value
                return self.value == other
        seen = []
        for count in [0,1,3,4,7,8,15,16,17,31,32,33,63,64,65]:
            text = 'é😀\x00ab' * count
            equal = ('!' + text)[1:]
            raw = text.encode('utf-8')
            equal_raw = bytes(list(raw))
            for value, twin in [(text, equal), (raw, equal_raw)]:
                direct = {value: 1}
                wrapped = {Key(value): 2}
                seen.append(
                    hash(value) == hash(twin)
                    and value.__hash__() == hash(value)
                    and hash((value,)) == hash((twin,))
                    and direct[twin] == 1 and direct[Key(twin)] == 1
                    and wrapped[twin] == 2 and wrapped[Key(twin)] == 2
                    and len({value, twin, Key(twin)}) == 1
                )
        print(len(seen), all(seen))
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuiltinAndProtocolKeysShareHashesAndKeepRetainedStorageFunded(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Source + "\nreturn [text, raw]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var expectedText = string.Concat(Enumerable.Repeat("é😀\0ab", 65));
        for (var run = 0; run < 2; run++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("30 True\n", result.StandardOutput);
                var values = Assert.IsType<List<object?>>(result.ReturnValue);
                Assert.Equal(expectedText, Assert.IsType<string>(values[0]));
                Assert.Equal(Encoding.UTF8.GetBytes(expectedText), Assert.IsType<byte[]>(values[1]));
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
