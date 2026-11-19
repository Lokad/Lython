using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BindingPlanConstructionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return [false,"empty","def f():return []\nprint(f(),f.__defaults__,f.__kwdefaults__)","[] None None\n"];
        yield return [true,"empty","def f():return []\nprint(f(),f.__defaults__,f.__kwdefaults__)","[] None None\n"];
        yield return [false,"single","def f(x):return x\nprint(f(3),f(x=4))","3 4\n"];
        yield return [true,"single","def f(x):return x\nprint(f(3),f(x=4))","3 4\n"];
        yield return [false,"mixed-default-mutation","def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\nprint(f(1,d=4))\nprint(f(1,7,8,9,10,d=11,e=12,z=13))\nf.__defaults__=(20,30)\nf.__kwdefaults__['e']=50\nprint(f(1,d=40))\nprint(f(1,d=4,a=9,args=10))","[1, 2, 3, (), 4, 5, {}]\n[1, 7, 8, (9, 10), 11, 12, {'z': 13}]\n[1, 20, 30, (), 40, 50, {}]\n[1, 20, 30, (), 4, 50, {'a': 9, 'args': 10}]\n"];
        yield return [true,"mixed-default-mutation","def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\nprint(f(1,d=4))\nprint(f(1,7,8,9,10,d=11,e=12,z=13))\nf.__defaults__=(20,30)\nf.__kwdefaults__['e']=50\nprint(f(1,d=40))\nprint(f(1,d=4,a=9,args=10))","[1, 2, 3, (), 4, 5, {}]\n[1, 7, 8, (9, 10), 11, 12, {'z': 13}]\n[1, 20, 30, (), 40, 50, {}]\n[1, 20, 30, (), 4, 50, {'a': 9, 'args': 10}]\n"];
        yield return [false,"mixed-errors","def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\ntry:f()\nexcept TypeError as error:print(type(error).__name__)\ntry:f(1,2,3,**{'c':4,'d':5})\nexcept TypeError as error:print(type(error).__name__)","TypeError\nTypeError\n"];
        yield return [true,"mixed-errors","def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\ntry:f()\nexcept TypeError as error:print(type(error).__name__)\ntry:f(1,2,3,**{'c':4,'d':5})\nexcept TypeError as error:print(type(error).__name__)","TypeError\nTypeError\n"];
        yield return [false,"unicode-keyword","def f(caf\u00e9,*,\u03bb=2):return caf\u00e9+\u03bb\nprint(f(1),f(1,\u03bb=3))","3 4\n"];
        yield return [true,"unicode-keyword","def f(caf\u00e9,*,\u03bb=2):return caf\u00e9+\u03bb\nprint(f(1),f(1,\u03bb=3))","3 4\n"];
        yield return [false,"lambda","fn=lambda a=3,*,b=4:a+b\nprint(fn(),fn(5,b=6))","7 11\n"];
        yield return [true,"lambda","fn=lambda a=3,*,b=4:a+b\nprint(fn(),fn(5,b=6))","7 11\n"];
        yield return [false,"method","class C:\n def f(self,a=1,*,b=2):return a+b\nprint(C().f(),C().f(3,b=4))","3 7\n"];
        yield return [true,"method","class C:\n def f(self,a=1,*,b=2):return a+b\nprint(C().f(),C().f(3,b=4))","3 7\n"];
        yield return [false,"generator","def f(a=1,*,b=2):yield a+b\ng=f()\nf.__kwdefaults__['b']=9\nprint(list(g),list(f(3,b=4)))","[3] [7]\n"];
        yield return [true,"generator","def f(a=1,*,b=2):yield a+b\ng=f()\nf.__kwdefaults__['b']=9\nprint(list(g),list(f(3,b=4)))","[3] [7]\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ParameterShapesAndMutableDefaultsMatchPython(bool asynchronous, string name, string source, string expected)
        => await Check(asynchronous, name, source, expected);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingBindingErrorMessagesRemainStable(bool asynchronous)
        => await Check(asynchronous, "legacy messages", "def f(a,b=2,/,c=3,*args,d,e=5,**kw):return [a,b,c,args,d,e,kw]\ntry:f()\nexcept TypeError as error:print(str(error))\ntry:f(1,2,3,**{'c':4,'d':5})\nexcept TypeError as error:print(str(error))", "Function 'f' is missing argument 'a'.\nFunction 'f' got multiple values for argument 'c'.\n");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeywordOverflowRetainsForwardedKeysAndValues(bool asynchronous)
        => await Check(asynchronous, "expanded keyword aliases", """
            def collect(**fields):
                return fields
            key = ''.join(['café', '_', 'field'])
            value = []
            result = collect(first=None, **{key: value}, last=1)
            print(list(result))
            print(list(result)[1] is key, result[key] is value)
            """, "['first', 'café_field', 'last']\nTrue True\n");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineKeywordDictionaryKeepsEntriesThroughSuspension(bool asynchronous)
    {
        const string source = """
            calls = []
            def collect(label, **fields):
                calls.append(label)
                with open('text.txt') as input_file:
                    fields['status'] = label + input_file.read()
                return fields
            key = ''.join(['dynamic', '_key'])
            value = []
            fresh = collect('fresh', **{key: None, 'next': 1, key: value})
            named = {key: value}
            other = collect('other', **named)
            def make_record(label):
                return collect(label, **{'fixed': value})
            record = make_record('record')
            repeated = make_record('repeated')
            print(list(fresh), next(iter(fresh)) is key, fresh[key] is value)
            print(other is not named, 'status' not in named, record['fixed'] is value)
            print(record is not repeated, repeated['fixed'] is value)
            print(fresh['status'], other['status'], record['status'], repeated['status'])
            try: collect('unreachable', **{'label': 1, 5: value})
            except TypeError as error: print('keywords must be strings' in str(error))
            try: collect('unreachable', **{'label': 1})
            except TypeError as error: print('multiple values' in str(error))
            print(calls)
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost(); host.SeedFile("/text.txt", "!");
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/text.txt", "!");
        using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("['dynamic_key', 'next', 'status'] True True\nTrue True True\nTrue True\n" +
                "fresh! other! record! repeated!\nTrue\nTrue\n['fresh', 'other', 'record', 'repeated']\n", result.StandardOutput);
            if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BodyLocalKeywordsRemainOverflowAndFactoryDefaultsStayIndependent(bool asynchronous)
        => await Check(asynchronous, "shared declaration, separate defaults", """
            def make(value):
                def f(a=value, /, *args, key=value, **kw):
                    local = a + key
                    return local, args, kw
                return f
            first = make(1)
            second = make(2)
            first.__defaults__ = (10,)
            first.__kwdefaults__['key'] = 20
            print(first(local=7, a=8, args=9, kw=10))
            print(second())
            print(first.__defaults__ is second.__defaults__, first.__kwdefaults__ is second.__kwdefaults__)
            def reject(value):
                local = value
                return local
            try: reject(1, local=2)
            except TypeError: print('unexpected')
            """, "(30, (), {'local': 7, 'a': 8, 'args': 9, 'kw': 10})\n(4, (), {})\nFalse False\nunexpected\n");

    private static async Task Check(bool asynchronous, string name, string source, string expected)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }
}
