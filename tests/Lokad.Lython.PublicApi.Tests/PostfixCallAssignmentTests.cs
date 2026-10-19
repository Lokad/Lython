using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PostfixCallAssignmentTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["member", """
            events=[]
            class C:pass
            c=C()
            def receiver():events.append('receiver');return c
            def rhs():events.append('rhs');return 3
            receiver().value=rhs()
            print(c.value,events)
            """, "3 ['rhs', 'receiver']\n"];
        yield return ["index", """
            events=[]
            items=[0,0]
            def receiver():events.append('receiver');return items
            def index():events.append('index');return 1
            def rhs():events.append('rhs');return 3
            receiver()[index()]=rhs()
            print(items,events)
            """, "[0, 3] ['rhs', 'receiver', 'index']\n"];
        yield return ["slice", """
            events=[]
            items=[0,1,2,3]
            def receiver():events.append('receiver');return items
            def bound(name,value):events.append(name);return value
            def rhs():events.append('rhs');return [8,9]
            receiver()[bound('start',0):bound('stop',4):bound('step',2)]=rhs()
            print(items,events)
            """, "[8, 1, 9, 3] ['rhs', 'receiver', 'start', 'stop', 'step']\n"];
        yield return ["chained_first_call", """
            events=[]
            class C:pass
            c=C()
            items=[0]
            def receiver():events.append('member');return c
            def sequence():events.append('sequence');return items
            def index():events.append('index');return 0
            def rhs():events.append('rhs');return 3
            receiver().value=sequence()[index()]=rhs()
            print(c.value,items,events)
            """, "3 [3] ['rhs', 'member', 'sequence', 'index']\n"];
        yield return ["chained_after_name_control", """
            events=[]
            class C:pass
            c=C()
            def receiver():events.append('receiver');return c
            def rhs():events.append('rhs');return [3]
            alias=receiver().value=rhs()
            print(alias is c.value,events)
            """, "True ['rhs', 'receiver']\n"];
        yield return ["unpacking_control", """
            events=[]
            class C:pass
            c=C()
            items=[0]
            def receiver():events.append('member');return c
            def sequence():events.append('sequence');return items
            def index():events.append('index');return 0
            def rhs():events.append('rhs');return 3,4
            receiver().value,sequence()[index()]=rhs()
            print(c.value,items,events)
            """, "3 [4] ['rhs', 'member', 'sequence', 'index']\n"];
        yield return ["annotated_control", """
            events=[]
            class C:pass
            c=C()
            def receiver():events.append('receiver');return c
            def rhs():events.append('rhs');return 3
            receiver().value:int=rhs()
            print(c.value,events)
            """, "3 ['rhs', 'receiver']\n"];
        yield return ["augmented_control", """
            events=[]
            items=[1]
            def receiver():events.append('receiver');return items
            def index():events.append('index');return 0
            def rhs():events.append('rhs');return 3
            receiver()[index()]+=rhs()
            print(items,events)
            """, "[4] ['receiver', 'index', 'rhs']\n"];
        yield return ["parenthesized_control", """
            events=[]
            class C:pass
            c=C()
            def receiver():events.append('receiver');return c
            def rhs():events.append('rhs');return 3
            (receiver()).value=rhs()
            print(c.value,events)
            """, "3 ['rhs', 'receiver']\n"];
        yield return ["mapping_factory", """
            events=[]
            class C:pass
            c=C()
            def receiver():events.append('receiver');return c
            def rhs():events.append('rhs');return 3
            {'factory':receiver}['factory']().value=rhs()
            print(c.value,events)
            """, "3 ['rhs', 'receiver']\n"];
        yield return ["nested_calls_and_arguments", """
            events=[]
            class C:pass
            c=C()
            def outer(a,b):events.append(a+b);return lambda:c
            def rhs():events.append('rhs');return 3
            outer('a','b')().value=rhs()
            print(c.value,events)
            """, "3 ['rhs', 'ab']\n"];
        yield return ["globals_mutation", """
            value=1
            def mutate():
                globals()['value']=9
                return 2
            class Runner:
                def run(self):return mutate()+value,value
            print(Runner().run())
            """, "(11, 9)\n"];
        yield return ["rhs_failure", """
            events=[]
            def receiver():events.append('receiver');return [0]
            def index():events.append('index');return 0
            def rhs():events.append('rhs');raise ValueError('rhs')
            try:receiver()[index()]=rhs()
            except ValueError:pass
            print(events)
            """, "['rhs']\n"];
        yield return ["receiver_failure", """
            events=[]
            def receiver():events.append('receiver');raise ValueError('receiver')
            def index():events.append('index');return 0
            def rhs():events.append('rhs');return 3
            try:receiver()[index()]=rhs()
            except ValueError:pass
            print(events)
            """, "['rhs', 'receiver']\n"];
        yield return ["index_failure", """
            events=[]
            def receiver():events.append('receiver');return [0]
            def index():events.append('index');raise ValueError('index')
            def rhs():events.append('rhs');return 3
            try:receiver()[index()]=rhs()
            except ValueError:pass
            print(events)
            """, "['rhs', 'receiver', 'index']\n"];
        yield return ["chained_store_failure", """
            events=[]
            class C:pass
            c=C()
            class Target:
                def __setitem__(self,key,value):events.append('store');raise ValueError('store')
            target=Target()
            def receiver():events.append('member');return c
            def sequence():events.append('sequence');return target
            def rhs():events.append('rhs');return 3
            try:receiver().value=sequence()[0]=rhs()
            except ValueError:pass
            print(c.value,events)
            """, "3 ['rhs', 'member', 'sequence', 'store']\n"];
        yield return ["user_slice_keeps_raw_key_and_value", """
            events=[]
            saved=[]
            class Bound:
                def __index__(self):events.append('coerce');raise AssertionError('must stay raw')
            bound=Bound()
            class Payload:pass
            payload=Payload()
            class Target:
                def __setitem__(self,key,value):events.append('store');saved.append((key,value))
            target=Target()
            def receiver():events.append('receiver');return target
            def rhs():events.append('rhs');return payload
            receiver()[bound:bound:bound]=rhs()
            key,value=saved[0]
            print(isinstance(key,slice),key.start is bound,key.stop is bound,key.step is bound,value is payload,events)
            """, "True True True True True ['rhs', 'receiver', 'store']\n"];
        yield return ["missing_user_slice_setter", """
            events=[]
            class Target:pass
            target=Target()
            def receiver():events.append('receiver');return target
            def rhs():events.append('rhs');return [3]
            try:receiver()[:]=rhs()
            except TypeError:events.append('TypeError')
            print(events)
            """, "['rhs', 'receiver', 'TypeError']\n"];
        yield return ["nested_member_index_slice", """
            class C:pass
            c=C()
            c.items=[[0,1,2]]
            def receiver():return c
            receiver().items[0][1:]=[8,9]
            print(c.items)
            """, "[[0, 8, 9]]\n"];
        yield return ["literal_container_alias", """
            items=[0]
            {'items':items}['items'][0]=3
            print(items)
            """, "[3]\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AssignmentMatchesPython(string name, string source, string expected)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("f() = 1", 0, 3)]
    [InlineData("f(): int = 1", 0, 3)]
    [InlineData("f().g() = 1", 0, 7)]
    [InlineData("f().g(): int = 1", 0, 7)]
    public void BareCallsRemainInvalidAssignmentTargets(string source, int start, int length)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.False(script.IsValid);
        var diagnostic = Assert.Single(script.Diagnostics, d => d.Code == "LA1068");
        Assert.NotNull(diagnostic);
        var span = Assert.IsType<LythonSourceSpan>(diagnostic.Span);
        Assert.Equal(start, span.Start);
        Assert.Equal(length, span.Length);
    }
}
