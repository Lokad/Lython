using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryAggregationOrderTests
{
    public static IEnumerable<object[]> Cases()
    {
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
        using var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics));
        using var sync = compiled.Run(new MockLythonHost());
        using var asyncResult = await compiled.RunAsync(new MockLythonHost());
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
