using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AugmentedIntegerCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["promotion and mixed operands", """
            for x,y in [(127,1),(-128,-1),(2**64-1,1),(-2**64+1,-1),(2**128-1,1)]:
                x += y
                print(x,type(x).__name__)
            x=True
            x += True
            x += 1.25
            print(x)
            x=7
            x -= 2
            x *= 3
            print(x)
            """, "128 int\n-129 int\n18446744073709551616 int\n-18446744073709551616 int\n340282366920938463463374607431768211456 int\n3.25\n15\n"];
        yield return ["rich fallback and order", """
            events=[]
            class Left:
                def __iadd__(self,other):
                    events.append('iadd')
                    return NotImplemented
                def __add__(self,other):
                    events.append('add')
                    return NotImplemented
            class Right:
                def __radd__(self,other):
                    events.append('radd')
                    return 41
            def rhs():
                events.append('rhs')
                return Right()
            value=Left()
            value += rhs()
            value += 1
            print(events,value)
            items=[1]
            alias=items
            items += [2]
            print(items is alias,items)
            """, "['rhs', 'iadd', 'add', 'radd'] 42\nTrue [1, 2]\n"];
        yield return ["failure preserves the target", """
            value=17
            old=value
            try:
                value += 'bad'
            except TypeError:
                print(value is old,value)
            """, "True 17\n"];
        yield return ["generator suspension and captured target", """
            def outer():
                total=2**64-1
                def read():return total
                def generate():
                    nonlocal total
                    total += 1
                    yield read()
                    total += -2**64
                    yield read()
                return generate(),read
            gen,read=outer()
            print(next(gen),read())
            print(next(gen),read())
            print(list(gen))
            """, "18446744073709551616 18446744073709551616\n0 0\n[]\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task IntegerAugmentationAndFallbackMatchPython(string name, string source, string expected)
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
