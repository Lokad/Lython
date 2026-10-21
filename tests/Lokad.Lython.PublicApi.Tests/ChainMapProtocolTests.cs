using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ChainMapProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyMergingUsesStoredHashesWithoutCallingTheGuestHasherAgain(bool asynchronous)
    {
        using var script=new LythonEngine().Compile("""
            from collections import ChainMap
            enabled=[False]
            class Key:
                def __hash__(self):
                    if enabled[0]: raise ValueError('rehash')
                    return 1
                def __eq__(self,other): return True
            a=Key()
            b=Key()
            c=ChainMap({a:1},{b:2})
            enabled[0]=True
            print(len(c),len(list(c)),len(list(c.keys())))
            print(list(c)[0] is b)
            """);
        using var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost());
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("1 1 1\nTrue\n",result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapsIsLiveReplaceableStateAndCopiesHaveTheirOwnLists(bool asynchronous)
    {
        using var script=new LythonEngine().Compile("""
            from collections import ChainMap
            c=ChainMap({'a':1})
            maps=c.maps
            print(maps is c.maps)
            maps.append({'b':2})
            print(list(c),c['b'])
            copy=c.copy()
            child=c.new_child({'c':3})
            parent=c.parents
            print(copy.maps is c.maps,child.maps[1] is maps[0],parent.maps[0] is maps[1])
            c.maps=[{'z':9}]
            print(list(c),list(maps[0]),list(copy),list(child))
            c.maps=[]
            print(len(c),bool(c),list(c))
            try: c['x']=1
            except IndexError: print('empty')
            """);
        using var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost());
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("True\n['b', 'a'] 2\nFalse True True\n['z'] ['a'] ['b', 'a'] ['b', 'a', 'c']\n0 False []\nempty\n",result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MergedKeysUsePythonEqualityAndKeepFirstKeyIdentity(bool asynchronous)
    {
        using var script=new LythonEngine().Compile("""
            from collections import ChainMap
            class Key:
                def __hash__(self): return 1
                def __eq__(self,other): return True
            a=Key()
            b=Key()
            c=ChainMap({a:1},{b:2})
            print(len(c),len(list(c)),len(list(c.keys())))
            print(list(c)[0] is b)
            print(list(c.values()))
            nan=float('nan')
            c=ChainMap({nan:1},{nan:2})
            print(len(c),list(c)[0] is nan)
            c=ChainMap({a:1},{1:2})
            print(len(c),isinstance(list(c)[0],int))
            c=ChainMap({1:1},{a:2})
            print(len(c),isinstance(list(c)[0],int))
            return 7
            """);
        using var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost());
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("1 1 1\nTrue\n[1]\n1 True\n1 True\n1 False\n",result.StandardOutput);
        Assert.Equal(new BigInteger(7),result.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AliasesOfOneMapsListRemainUsableWithinTheSharedBudget(bool asynchronous)
    {
        var budget=new LythonMemoryBudget(32768);
        using var script=new LythonEngine().Compile("""
            from collections import ChainMap
            c=ChainMap({'a':1})
            aliases=[c.maps for _ in range(32)]
            aliases[-1].append({'b':2})
            return [aliases[0] is c.maps,c['b']]
            """);
        using(var result=asynchronous
            ?await script.RunAsync(new MockLythonHost(),new LythonRunOptions { MemoryBudget=budget })
            :script.Run(new MockLythonHost(),new LythonRunOptions { MemoryBudget=budget }))
        {
            Assert.True(result.Success,result.Failure?.Message);
            Assert.Equal(new List<object?> { true,new BigInteger(2) },result.ReturnValue);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
}
