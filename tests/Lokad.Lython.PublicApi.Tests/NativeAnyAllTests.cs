using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeAnyAllTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "bytes", "for payload in [b'',b'\\x00',b'\\x01\\x00',b'\\x01\\x02']:print(any(payload),all(payload))", "False True\nFalse False\nTrue False\nTrue True\n" },
        { "ranges", "for values in [range(0),range(0,1),range(1,4),range(-2,2),range(3,0,-1),range(10**30,10**30+3)]:print(any(values),all(values))", "False True\nFalse False\nTrue True\nTrue False\nTrue True\nTrue True\n" },
        { "alias-shadow", "choose=any\nany=lambda values:'guest'\nprint(choose(b'\\x01'),any(b'\\x01'))", "True guest\n" },
        { "argument-arity-effects", "events=[]\ndef value():events.append('argument');return b'1'\ntry:all(value(),value())\nexcept TypeError:print(events)\ntry:any()\nexcept TypeError:print('arity')", "['argument', 'argument']\narity\n" },
        { "guest-iterator", "class Values:\n def __iter__(self):\n  print('iter')\n  yield 0\n  yield 1\nprint(any(Values()),all(Values()))", "iter\niter\nTrue False\n" },
        { "guest-truth", "events=[]\nclass Value:\n def __bool__(self):events.append('guest');return True\nprint(any([0,Value()]),all([1,Value()]),events)", "True True ['guest', 'guest']\n" },
        { "binary-helper", "def valid(payload):return bool(payload) and all(payload)\nfor payload in [b'',b'\\x01\\x02',b'\\x01\\x00']:print(valid(payload))", "False\nTrue\nFalse\n" },
        { "one-shot-iterator", "values=iter(b'\\x00\\x01');print(any(values),list(values))\nvalues=iter(b'\\x01\\x00\\x02');print(all(values),list(values))", "True []\nFalse [2]\n" },
        { "high-bytes", "for payload in [b'\\xff',b'\\x80\\xff',b'\\xff\\x00']:print(any(payload),all(payload))", "True True\nTrue True\nTrue False\n" },
        { "boundary-short-circuit", "for position in [0,63,64]:\n zeros=bytes([0]*position+[1]+[0]*(64-position))\n ones=bytes([1]*position+[0]+[1]*(64-position))\n print(any(zeros),all(ones))", "True False\nTrue False\nTrue False\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task AggregatesPreservePythonCallsAndEffects(string name, string source, string expected)
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
