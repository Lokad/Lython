using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryAggregationOrderTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["native_aliases_and_iterator_versions", """
            d={'x':1000,'tail':8}; key='x'; amount=2
            previous=d[key]; cursor=iter(d)
            for i in range(4): d[key]=d.get(key,0)+amount
            print(previous,d[key],next(cursor),list(d))
            alias=d[key]
            print(alias is d[key],previous is d[key])
            key='y'; cursor=iter(d)
            d[key]=d.get(key,0)+amount
            try: next(cursor)
            except RuntimeError: print('changed')
            del d['tail']; d.clear(); key='z'
            for i in range(3): d[key]=d.get(key,0)+1
            print(d)
            """, "1000 1008 x ['x', 'tail']\nTrue False\nchanged\n{'z': 3}\n"];
        yield return ["assignment_receiver_and_key_are_distinct", """
            d={'x':1}; target={'x':10}; key='x'; other='y'; amount=1
            target[key]=d.get(key,0)+amount
            d[other]=d.get(key,0)+3
            print(d,target)
            """, "{'x': 1, 'y': 4} {'x': 2}\n"];
        yield return ["default_rebinds_assignment_target", """
            old={'x':5}; d=old; other={'x':20}; key='x'
            def default():
             globals()['d']=other
             return 0
            d[key]=d.get(key,default())+1
            print(old,other)
            """, "{'x': 5} {'x': 6}\n"];
        yield return ["native_range_and_wide_values", """
            d={}; amount=1
            for key,seed in [('a',36028797018963967),('b',36028797018963968),('c',-36028797018963968),('d',9223372036854775807)]:
             d[key]=seed
             d[key]=d.get(key,0)+amount
            print(list(d.values()))
            """, "[36028797018963968, 36028797018963969, -36028797018963967, 9223372036854775808]\n"];
        yield return ["native_missing_and_promotion", """
            d={'x':2**80}; key='x'; amount=2**80
            d[key]=d.get(key,0)+amount
            key='y'
            d[key]=d.get(key,0)+1
            print(d['x']==2**81,d['y'])
            """, "True 1\n"];
        yield return ["numeric_and_receiver_fallback", """
            d={'x':1.5}; key='x'; amount=2
            d[key]=d.get(key,0)+amount
            print(d[key])
            d={'x':True}; amount=True
            d[key]=d.get(key,0)+amount
            print(d[key],type(d[key]).__name__)
            class D:
             def __init__(self): self.data={}
             def get(self,key,default): return 40
             def __getitem__(self,key): return self.data[key]
             def __setitem__(self,key,value): self.data[key]=value
            d=D(); amount=2
            d[key]=d.get(key,0)+amount
            print(d[key])
            """, "3.5\n2 int\n42\n"];
        yield return ["late_binding_after_get", """
            events=[]
            if False: amount=0
            class D:
             def get(self,key,default):
              events.append('get')
              globals()['amount']=4
              return 3
             def __setitem__(self,key,value): events.append((key,value))
            d=D(); key='x'
            d[key]=d.get(key,0)+amount
            print(events)
            """, "['get', ('x', 7)]\n"];
        yield return ["protocol_key_equal_to_string", """
            events=[]
            class Key:
             def __hash__(self): return hash('x')
             def __eq__(self,other):
              events.append('eq')
              return other=='x'
            custom=Key(); d={custom:5}; key='x'; amount=2
            d[key]=d.get(key,0)+amount
            print(list(d.values()),list(d)[0] is custom,len(events)>0)
            """, "[7] True True\n"];
        yield return ["addition_rebinds_lhs", """
            other={}; events=[]
            class Number:
             def __add__(self,value):
              events.append('add')
              globals()['d']=other
              globals()['key']='after'
              return 9
            d={'before':Number()}; old=d; key='before'; amount=2
            d[key]=d.get(key,0)+amount
            print(events,other, isinstance(old['before'],Number))
            """, "['add'] {'after': 9} True\n"];
        yield return ["finally_loop_and_generator", """
            events=[]
            def generate():
             d={}
             for key in ['a','b','a']:
              try:
               d[key]=d.get(key,0)+1
               if key=='b': continue
               yield d[key]
              finally: events.append(key)
            print(list(generate()),events)
            """, "[1, 2] ['a', 'b', 'a']\n"];
        yield return ["shadowed_comprehension_names", """
            d={}; key='outer'; amount=10
            values=[d.get(key,0)+amount for key in ['a','b'] for amount in [1,2]]
            print(values,key,amount)
            """, "[1, 2, 1, 2] outer 10\n"];
        yield return ["missing_amount_retains_get_effects", """
            events=[]
            if False: amount=0
            class D:
             def get(self,key,default):
              events.append('get')
              return 3
            d=D(); key='x'
            try: d[key]=d.get(key,0)+amount
            except NameError: print(events)
            """, "['get']\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AggregationPreservesNativeAndProtocolEvaluation(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var compiled = new LythonEngine().Compile(source, budget);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        compiled.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
