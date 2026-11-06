using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Counts are observable Python callbacks, never interpreter checkpoints/fuel.
public sealed class HashQueryCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var operation in new[] { "table[key]=1", "table.get(key)", "key in table", "set().add(key)", "{key:1}", "hash(key)" })
        {
            yield return ["earlier tuple hash precedes later unhashability: " + operation, """
                events=[]
                class Key:
                 def __hash__(self):
                  events.append('hash')
                  return 1
                key=tuple([Key(),[]])
                table={}
                try:
                 OPERATION
                except TypeError:print(events)
                """.Replace("OPERATION", operation), "['hash']\n"];
        }
        yield return ["earlier tuple callback error wins over later unhashability", """
            events=[]
            class Key:
             def __hash__(self):
              events.append('hash')
              raise ValueError('first')
            key=tuple([Key(),[]])
            try:{}[key]=1
            except (TypeError,ValueError) as error:print(type(error).__name__,events)
            """, "ValueError ['hash']\n"];
        yield return ["nested tuple and slice hashes preserve left-to-right effects", """
            from collections import namedtuple
            Pair=namedtuple('Pair','first second')
            events=[]
            class Key:
             def __hash__(self):
              events.append('hash')
              return 1
            for key in [((Key(),),[]),slice((Key(),),[]),Pair(Key(),[])]:
             try:{}[key]=1
             except TypeError:print(events)
             events.clear()
            """, "['hash']\n['hash']\n['hash']\n"];
        yield return ["earlier hash can disable the later element hash", """
            events=[]
            class Later:
             def __hash__(self):
              events.append('later')
              return 2
            class First:
             def __hash__(self):
              events.append('first')
              Later.__hash__=None
              return 1
            key=tuple([First(),Later()])
            try:{}[key]=1
            except TypeError:print(events)
            """, "['first']\n"];
        yield return ["empty set queries still invoke hashing", """
            events=[]
            class Key:
             def __hash__(self):
              events.append('hash')
              return 17
            key=Key()
            print(key in set(),events)
            """, "False ['hash']\n"];
        yield return ["custom set queries match retained native items", """
            class Key:
             def __hash__(self):return hash(1)
             def __eq__(self,other):return isinstance(other,int) and other==1
            key=Key()
            s={1}
            print(key in s)
            s.add(key)
            print(len(s),next(iter(s)) is key)
            s.remove(key)
            print(len(s))
            """, "True\n1 False\n0\n"];
        yield return ["set equality clearing its candidate does not return a stale match", """
            class Key:
             armed=False
             def __hash__(self):return 17
             def __eq__(self,other):
              if self.armed:s.clear()
              return True
            stored=Key()
            query=Key()
            for operation in ['contains','add','remove']:
             s={stored}
             stored.armed=True
             if operation=='contains':print(query in s,len(s))
             elif operation=='add':
              s.add(query)
              print(len(s))
             else:
              try:s.remove(query)
              except KeyError:print('KeyError',len(s))
            """, "False 0\n0\nKeyError 0\n"];
        yield return ["structural collisions do not compare different Python hashes", """
            from dataclasses import dataclass
            events=[]
            @dataclass(unsafe_hash=True,eq=False)
            class Key:
             value:int=0
            def key_hash(self):return self.tag
            def key_eq(self,other):
             events.append('eq')
             return False
            Key.__hash__=key_hash
            Key.__eq__=key_eq
            stored=Key()
            stored.tag=17
            query=Key()
            query.tag=18
            d={stored:1}
            s={stored}
            events.clear()
            print(d.get(query),query in s,d.pop(query,'missing'))
            s.discard(query)
            d[query]=2
            s.add(query)
            print(len(d),len(s),d.pop(query))
            s.remove(query)
            print(len(d),len(s),events)
            """, "None False missing\n2 2 2\n1 1 []\n"];
        yield return ["a changed query hash cannot reuse the stored identity hash", """
            class Key:
             tag=17
             def __hash__(self):return self.tag
            key=Key()
            d={key:1}
            s={key}
            key.tag=18
            print(d.get(key,'missing'),key in s,d.pop(key,'missing'))
            try:s.remove(key)
            except KeyError:print('KeyError')
            print(len(d),len(s))
            """, "missing False missing\nKeyError\n1 1\n"];
        yield return ["dictionary queries and removals", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            d={key:7}
            print(d[key],d.get(key),key in d)
            print(d.pop(key))
            d[key]=8
            del d[key]
            print(events)
            """, "7 7 True\n7\n['hash', 'hash', 'hash', 'hash', 'hash', 'hash', 'hash']\n"];
        yield return ["set queries and removals", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            s={key}
            print(key in s)
            s.add(key)
            s.remove(key)
            s.add(key)
            s.discard(key)
            print(events)
            """, "True\n['hash', 'hash', 'hash', 'hash', 'hash', 'hash']\n"];
        yield return ["stored set pop has no hash callback", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            s={key}
            key.fail=True
            print(s.pop() is key,len(s),events)
            """, "True 0 ['hash']\n"];
        yield return ["native dictionary copies reuse stored hashes", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            d={key:7}
            copied=d.copy()
            constructed=dict(d)
            print(len(copied),len(constructed),events)
            """, "1 1 ['hash']\n"];
        yield return ["tuple key queries", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            d={(key,):7}
            print(d[(key,)],d.get((key,)),(key,) in d)
            del d[(key,)]
            print(events)
            """, "7 7 True\n['hash', 'hash', 'hash', 'hash', 'hash']\n"];
        yield return ["dictionary callback failures preserve storage", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            d={key:7}
            key.fail=True
            def drop():del d[key]
            for action in [lambda:d[key],lambda:d.get(key),lambda:key in d,lambda:d.pop(key),drop]:
             try:action()
             except ValueError as error:print(str(error))
            key.fail=False
            print(len(d),d[key])
            """, "hash failed\nhash failed\nhash failed\nhash failed\nhash failed\n1 7\n"];
        yield return ["set callback failures preserve storage", """
            events=[]
            class Key:
             fail=False
             def __hash__(self):
              if self.fail:raise ValueError('hash failed')
              events.append('hash')
              return 17
            key=Key()
            s={key}
            key.fail=True
            for action in [lambda:key in s,lambda:s.remove(key),lambda:s.discard(key)]:
             try:action()
             except ValueError as error:print(str(error))
            key.fail=False
            print(len(s))
            """, "hash failed\nhash failed\nhash failed\n1\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task QueriesInvokeHashAndPropagateFailures(string name, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
        using var synchronous = script.Run(new MockLythonHost());
        Assert.True(synchronous.Success, name + ": " + synchronous.Failure?.Message);
        Assert.Equal(expected, synchronous.StandardOutput);
        using var asynchronous = await script.RunAsync(new MockLythonHost());
        Assert.True(asynchronous.Success, name + ": " + asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.StandardOutput);
    }
}
