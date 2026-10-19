using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UserSliceMutationCompatibilityTests
{
    private const string Common = """
        events=[]
        class Bound:
            def __index__(self):raise ValueError('unexpected coercion')
        start=Bound()
        stop=Bound()
        step=Bound()
        class Target:
            def __delitem__(self,key):
                events.append('delete')
                self.saved=key
                print(key.start is start,key.stop is stop,key.step is step)
            def __setitem__(self,key,value):
                events.append(value)
                self.saved=key
                print(key.start is start,key.stop is stop,key.step is step)
        target=Target()
        """ + "\n";

    public static IEnumerable<object[]> Cases()
    {
        yield return ["deletion", """
            del target[start:stop:step]
            print(events)
            print(target.saved.start is start)
            """, "True True True\n['delete']\nTrue\n"];
        yield return ["deletion order", """
            def receiver():
                events.append('receiver')
                return target
            def bound(name,value):
                events.append(name)
                return value
            del receiver()[bound('start',start):bound('stop',stop):bound('step',step)]
            print(events)
            """, "True True True\n['receiver', 'start', 'stop', 'step', 'delete']\n"];
        yield return ["loop target", """
            for target[start:stop:step] in [11,12]:
                events.append('body')
            print(events)
            print(target.saved.stop is stop)
            """, "True True True\nTrue True True\n[11, 'body', 12, 'body']\nTrue\n"];
        yield return ["unpacked loop target", """
            for target[start:stop:step],other in [(7,9)]:
                print(other)
            print(events)
            print(target.saved.step is step)
            """, "True True True\n9\n[7]\nTrue\n"];
        yield return ["starred loop target", """
            for target[start:stop:step],*rest in [(7,8,9)]:
                print(rest)
            print(events)
            print(target.saved.start is start)
            """, "True True True\n[8, 9]\n[7]\nTrue\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RawBoundsOrderingAndEscapedKeysMatchPython(string name, string source, string expected)
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(Common + source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
