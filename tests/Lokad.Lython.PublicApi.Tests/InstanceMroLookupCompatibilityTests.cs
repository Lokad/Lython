using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class InstanceMroLookupCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["inherited method replacement and shadowing", """
            class Base:
                def method(self): return 'base'
            class Left(Base): pass
            class Right(Base):
                def method(self): return 'right'
            class C(Left,Right): pass
            c=C()
            print(c.method())
            Right.method=lambda self:'new'
            print(c.method())
            del Right.method
            print(c.method())
            c.method=lambda:'own'
            Base.method=lambda self:'base-new'
            print(c.method())
            del c.method
            print(c.method())
            """, "right\nnew\nbase\nown\nbase-new\n"];
        yield return ["live descriptor type mutation", """
            class D:
                def __get__(self,obj,owner): return 'descriptor'
            class C: value=D()
            c=C()
            c.value='own'
            print(c.value)
            def setter(self,obj,value): pass
            D.__set__=setter
            print(c.value)
            del D.__set__
            print(c.value)
            C.value='replacement'
            del c.value
            print(c.value)
            """, "own\ndescriptor\nown\nreplacement\n"];
        yield return ["getter mutation and reentry", """
            events=[]
            class D:
                def __get__(self,obj,owner):
                    events.append('get')
                    owner.value='replacement'
                    print('nested',obj.value)
                    return 'first'
            class C: value=D()
            c=C()
            print(c.value)
            print(c.value)
            print(events)
            """, "nested replacement\nfirst\nreplacement\n['get']\n"];
        yield return ["getter failure and fresh fallback", """
            class D:
                def __get__(self,obj,owner):
                    owner.value=37
                    raise ValueError('sentinel')
            class C:
                value=D()
                def __getattr__(self,name): return 'fallback:'+name
            c=C()
            try: print(c.value)
            except ValueError as e: print(str(e))
            print(c.value)
            del C.value
            print(c.value)
            """, "sentinel\n37\nfallback:value\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CurrentMroValuesAndDescriptorEffectsMatchPython(string name, string source, string expected)
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
