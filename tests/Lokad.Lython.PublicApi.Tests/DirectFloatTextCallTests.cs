using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DirectFloatTextCallTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "text-bytes", "print(float('1.25'),float(b'2.5'))", "1.25 2.5\n" },
        { "underscore-exponent", "print(float('1_234.5'),float('2e3'))", "1234.5 2000.0\n" },
        { "nonfinite-signed-zero", "print(float('-0.0'),float('INF'),float('-Infinity'),str(float('nan')))", "-0.0 inf -inf nan\n" },
        { "invalid-text", "for text in ['1__0','1\\x00','_1','1_']:\n try:float(text)\n except ValueError:print('bad')", "bad\nbad\nbad\nbad\n" },
        { "alias-and-shadow", "parse=float\nfloat=lambda value:'guest'\nprint(parse('1.25'),float('1.25'))", "1.25 guest\n" },
        { "keyword-effects", "events=[]\ndef value():events.append('value');return '1.25'\ntry:float(value=value())\nexcept TypeError:print(events)", "['value']\n" },
        { "guest-protocol", "events=[]\nclass Value:\n def __float__(self):events.append('float');return 1.25\nprint(float(Value()),events)", "1.25 ['float']\n" },
        { "helper-record-sum", "def amount(row):return float(row.split(',')[1])\nprint(sum(amount(row) for row in ['a,1.25','b,2.5','c,-0.75']))", "3.0\n" },
        { "no-argument", "print(float())", "0.0\n" },
        { "native-numbers", "print(float(True),float(7),float(1.25))", "1.0 7.0 1.25\n" },
        { "expanded-call", "print(float(*('1.25',)),float(*(b'2.5',)))", "1.25 2.5\n" },
        { "captured-callable", "def value():\n global float\n float=lambda value:9.0\n return '1.25'\nprint(float(value()),float('2.5'))", "1.25 9.0\n" },
        { "invalid-byte", "try:float(b'\\xff')\nexcept ValueError:print('bad')", "bad\n" },
        { "noncallable", "events=[]\nfloat=[]\ndef value():events.append('value');return '1.25'\ntry:float(value())\nexcept TypeError:print(events)", "['value']\n" },
        { "arity-effects", "events=[]\ndef value():events.append('value');return '1.25'\ntry:float(value(),value())\nexcept TypeError:print(events)", "['value', 'value']\n" },
        { "index-fallback", "class Value:\n def __index__(self):return 7\nprint(float(Value()))", "7.0\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task FloatPreservesPythonCallsAndEffects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
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
    }
}
