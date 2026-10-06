using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MixedSubscriptSyntaxTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[]
        {
            "tuple_index_control",
            """
            class Indexer:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print(key,value)
             def __delitem__(self,key):print(key)
            x=Indexer()
            print(x[1,2],x[*[1,2],3])
            """,
            "(1, 2) (1, 2, 3)\n"
        };
        yield return new object[]
        {
            "slice_tuple_first",
            """
            class Indexer:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print(key,value)
             def __delitem__(self,key):print(key)
            x=Indexer()
            print(x[1:3,0])
            """,
            "(slice(1, 3, None), 0)\n"
        };
        yield return new object[]
        {
            "slice_tuple_second",
            """
            class Indexer:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print(key,value)
             def __delitem__(self,key):print(key)
            x=Indexer()
            print(x[0,1:3])
            """,
            "(0, slice(1, 3, None))\n"
        };
        yield return new object[]
        {
            "slice_tuple_multiple",
            """
            class Indexer:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print(key,value)
             def __delitem__(self,key):print(key)
            x=Indexer()
            print(x[1:3,::-1])
            """,
            "(slice(1, 3, None), slice(None, None, -1))\n"
        };
        yield return new object[]
        {
            "slice_tuple_singleton",
            """
            class Indexer:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print(key,value)
             def __delitem__(self,key):print(key)
            x=Indexer()
            print(x[1:3,])
            """,
            "(slice(1, 3, None),)\n"
        };
        yield return new object[]
        {
            "slice_tuple_store",
            """
            class Indexer:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print(key,value)
             def __delitem__(self,key):print(key)
            x=Indexer()
            x[1:3,0]=7
            """,
            "(slice(1, 3, None), 0) 7\n"
        };
        yield return new object[]
        {
            "slice_tuple_delete",
            """
            class Indexer:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print(key,value)
             def __delitem__(self,key):print(key)
            x=Indexer()
            del x[:,0]
            """,
            "(slice(None, None, None), 0)\n"
        };
        yield return new object[]
        {
            "all_slice_positions",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            print(x[:,0],x[0,:],x[1:3:2,],x[::,::-1,0:])
            print(x[...,0:1,*[2,3]],x[*[2],::],x[::,*[],0],x[*[]])
            """,
            "(slice(None, None, None), 0) (0, slice(None, None, None)) (slice(1, 3, 2),) (slice(None, None, None), slice(None, None, -1), slice(0, None, None))\n(Ellipsis, slice(0, 1, None), 2, 3) (2, slice(None, None, None)) (slice(None, None, None), 0) ()\n"
        };
        yield return new object[]
        {
            "slice_builtin_shadowing",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            slice=lambda *args:None
            print(x[1:2,0])
            """,
            "(slice(1, 2, None), 0)\n"
        };
        yield return new object[]
        {
            "bounds_remain_raw",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            class Bound:
             def __index__(self):raise AssertionError('coerced')
            b=Bound()
            key=x[b:[1]:{'a':2},0]
            print(key[0].start is b,key[0].stop,key[0].step)
            """,
            "True [1] {'a': 2}\n"
        };
        yield return new object[]
        {
            "tuple_slice_dictionary_keys",
            """
            x={(slice(1,3),0):7}
            print(x[1:3,0])
            x[:,1]=8
            print(x[:,1])
            del x[1:3,0]
            print(len(x))
            """,
            "7\n8\n1\n"
        };
        yield return new object[]
        {
            "exactly_once_augmented",
            """
            events=[]
            class Box:
             def __getitem__(self,key):events.append(('get',key));return 4
             def __setitem__(self,key,value):events.append(('set',key,value))
            box=Box()
            def mark(label,value):events.append(label);return value
            def receiver():events.append('receiver');return box
            receiver()[mark('start',1):mark('stop',3):mark('step',2),mark('index',0)]+=mark('value',5)
            print(events)
            """,
            "['receiver', 'start', 'stop', 'step', 'index', ('get', (slice(1, 3, 2), 0)), 'value', ('set', (slice(1, 3, 2), 0), 9)]\n"
        };
        yield return new object[]
        {
            "nested_assignment_targets",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            a,x[a:a+1,0],*rest=(1,7,8,9)
            print(a,rest)
            first=x[2:4,0]=6
            print(first)
            for x[:,0],tail in [(7,8),(9,10)]:print(tail)
            del x[:,0],x[0,::]
            """,
            "set (slice(1, 2, None), 0) 7\n1 [8, 9]\nset (slice(2, 4, None), 0) 6\n6\nset (slice(None, None, None), 0) 7\n8\nset (slice(None, None, None), 0) 9\n10\ndel (slice(None, None, None), 0)\ndel (0, slice(None, None, None))\n"
        };
        yield return new object[]
        {
            "with_tuple_slice_target",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            class Manager:
             def __enter__(self):return 7
             def __exit__(self,*args):print('exit')
            with Manager() as x[:,0]:print('body')
            """,
            "set (slice(None, None, None), 0) 7\nbody\nexit\n"
        };
        yield return new object[]
        {
            "generator_read_operands",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            def generate():
             return (yield 'receiver')[(yield 'start'):(yield 'stop'):(yield 'step'),(yield 'index')]
            g=generate()
            print(next(g),g.send(x),g.send(1),g.send(3),g.send(2))
            try:g.send(0)
            except StopIteration as e:print(e.value)
            """,
            "receiver start stop step index\n(slice(1, 3, 2), 0)\n"
        };
        yield return new object[]
        {
            "generator_store_operands",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            def generate():
             (yield 'receiver')[(yield 'start'):(yield 'stop'),(yield 'index')]=(yield 'value')
             yield 'done'
            g=generate()
            print(next(g),g.send(7),g.send(x),g.send(1),g.send(3))
            print(g.send(0))
            """,
            "value receiver start stop index\nset (slice(1, 3, None), 0) 7\ndone\n"
        };
        yield return new object[]
        {
            "generator_augmented_operands",
            """
            class Box:
             def __getitem__(self,key):print('get',key);return 4
             def __setitem__(self,key,value):print('set',key,value)
            x=Box()
            def generate():
             (yield 'receiver')[(yield 'start'):(yield 'stop'),(yield 'index')]+=(yield 'value')
             yield 'done'
            g=generate()
            print(next(g),g.send(x),g.send(1),g.send(3))
            print(g.send(0),g.send(5))
            """,
            "receiver start stop index\nget (slice(1, 3, None), 0)\nset (slice(1, 3, None), 0) 9\nvalue done\n"
        };
        yield return new object[]
        {
            "generator_delete_operands",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            def generate():
             del (yield 'receiver')[(yield 'start'):(yield 'stop'),(yield 'index')]
             yield 'done'
            g=generate()
            print(next(g),g.send(x),g.send(1),g.send(3))
            print(g.send(0))
            """,
            "receiver start stop index\ndel (slice(1, 3, None), 0)\ndone\n"
        };
        yield return new object[]
        {
            "generator_starred_key",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            def generate():
             return x[(yield 'start'):,*(yield 'items'),::(yield 'step')]
            g=generate()
            print(next(g),g.send(1),g.send([2,3]))
            try:g.send(-1)
            except StopIteration as e:print(e.value)
            """,
            "start items step\n(slice(1, None, None), 2, 3, slice(None, None, -1))\n"
        };
        yield return new object[]
        {
            "generator_key_cleanup",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            def generate():
             try:yield x[(yield 'start'):,*(yield 'items')]
             finally:print('cleanup')
            g=generate()
            print(next(g),g.send(1))
            g.close()
            """,
            "start items\ncleanup\n"
        };
        yield return new object[]
        {
            "grouped_multiline_keys",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            print(x[
             1:
             3,
             ::-1,
            ])
            """,
            "(slice(1, 3, None), slice(None, None, -1))\n"
        };
        yield return new object[]
        {
            "empty_and_scalar_tuple_controls",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            print(x[()],x[1,],x[*[1,2]],x[1:3])
            """,
            "() (1,) (1, 2) slice(1, 3, None)\n"
        };
        yield return new object[]
        {
            "assignment_expression_bounds",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            print(x[(a:=1):(b:=3),0],a,b)
            print(x[a:=7,::],a)
            """,
            "(slice(1, 3, None), 0) 1 3\n(7, slice(None, None, None)) 7\n"
        };
        yield return new object[]
        {
            "lambda_bounds",
            """
            class Box:
             def __getitem__(self,key):return key
             def __setitem__(self,key,value):print('set',key,value)
             def __delitem__(self,key):print('del',key)
            x=Box()
            key=x[lambda:1:lambda:3,0]
            print(key[0].start(),key[0].stop())
            print(x[:lambda:4,0][0].stop())
            """,
            "1 3\n4\n"
        };
    }

    [Fact]
    public async Task CompositeKeysAwaitDelayedItemAndBoundProtocols()
    {
        var compiled = new LythonEngine().Compile("""
            events=[]
            def touch(label,value):
             with open('/value.txt') as f:f.read()
             events.append(label)
             return value
            class Box:
             def __getitem__(self,key):return touch('get',4)
             def __setitem__(self,key,value):touch('set',None)
             def __delitem__(self,key):touch('del',None)
            box=Box()
            def exercise():
             box[touch('start',1):touch('stop',3),touch('index',0)]+=touch('value',5)
             print(box[0:1,0])
             del box[:,0]
             return box[1:2]
            print(exercise(),events)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value.txt", "hello");
        var sync = compiled.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("4\n4 ['start', 'stop', 'index', 'get', 'value', 'set', 'get', 'del', 'get']\n", sync.StandardOutput);
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "hello");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task RetainedSliceTupleKeysObeyMemoryLimit()
    {
        var compiled = new LythonEngine().Compile("""
            class Box:
             def __getitem__(self,key):return key
            box=Box()
            keep=[]
            for i in range(5000):keep.append(box[i:i+1,0])
            """);
        Assert.True(compiled.IsValid);
        const long budget = 32768;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = budget };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.True(result.PeakExecutionMemoryBytes <= budget);
            Assert.True(result.DeniedReservationBytes > 0);
        }
    }

    [Fact]
    public async Task DroppedSliceTupleKeysReclaimCharges()
    {
        var compiled = new LythonEngine().Compile("""
            class Box:
             def __getitem__(self,key):return key
            box=Box()
            for i in range(5000):key=box[i:i+1,0]
            print(key[0].start,key[0].stop,key[1])
            """);
        Assert.True(compiled.IsValid);
        const long budget = 524288;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = budget };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("4999 5000 0\n", result.StandardOutput);
            Assert.True(result.PeakExecutionMemoryBytes <= budget);
        }
    }

    [Fact]
    public async Task StarredKeysFailCollectionLimitBeforeItemProtocol()
    {
        var compiled = new LythonEngine().Compile("""
            class Box:
             def __getitem__(self,key):print('called')
            Box()[:,*range(1000000)]
            """);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxCollectionSize = 64 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Equal(string.Empty, result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("x[*1:2]")]
    [InlineData("x[1:::2]")]
    [InlineData("x[:, ,]")]
    [InlineData("x[**{}]")]
    [InlineData("x[(1:2),0]")]
    [InlineData("x[(yield 1):,0]")]
    [InlineData("x[a:=1:3,0]")]
    [InlineData("x[:a:=1,0]")]
    [InlineData("x[::a:=1,0]")]
    [InlineData("x[a:=1:3]")]
    public void InvalidSubscriptionsFailBeforeEffects(string expression)
    {
        var compiled = new LythonEngine().Compile("print('effect')\n" + expression);
        Assert.False(compiled.IsValid);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CompositeSubscriptionKeysFollowPython(string name, string source, string expected)
    {
        _ = name;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
