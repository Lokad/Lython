using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SequenceFallbackCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "getitem-sequence-cursor-and-permanent-exhaustion", "class Sequence:\n    def __init__(self):\n        self.end=3\n    def __getitem__(self,index):\n        print('get',index)\n        if index>=self.end:\n            raise IndexError\n        return index*2\ns=Sequence()\ni=iter(s)\nprint(iter(i) is i,next(i),list(i),next(i,'empty'))\ns.end=5\nprint(next(i,'empty'))\nprint(list(s))\n", "get 0\nget 1\nget 2\nget 3\nTrue 0 [2, 4] empty\nempty\nget 0\nget 1\nget 2\nget 3\nget 4\nget 5\n[0, 2, 4, 6, 8]\n" };
        yield return new object[] { "getitem-sequence-recoverable-errors-and-stopiteration", "class Sequence:\n    def __init__(self):\n        self.failed=False\n    def __getitem__(self,index):\n        print('get',index)\n        if index==1 and not self.failed:\n            self.failed=True\n            raise RuntimeError('retry')\n        if index>=2:\n            raise StopIteration\n        return index\ni=iter(Sequence())\nprint(next(i))\ntry:\n    next(i)\nexcept RuntimeError:\n    print('retry')\nprint(next(i),next(i,'end'),next(i,'end'))\n", "get 0\n0\nget 1\nretry\nget 1\nget 2\n1 end end\n" };
        yield return new object[] { "iteration-type-slot-priority-and-shadowing", "class Sequence:\n    def __getitem__(self,index):\n        if index>=2:\n            raise IndexError\n        return index\ns=Sequence()\ns.__getitem__=lambda index:99\ns.__iter__=lambda:iter([99])\nprint(list(s))\nclass Disabled(Sequence):\n    __iter__=None\nclass Iterated(Sequence):\n    def __iter__(self):\n        return iter([7,8])\nclass Pretend:\n    def __getattr__(self,name):\n        if name=='__iter__':\n            return lambda:iter([99])\n        raise AttributeError(name)\nfor value in [Disabled(),Iterated(),Pretend()]:\n    try:\n        print(list(value))\n    except TypeError:\n        print('TypeError')\n", "[0, 1]\nTypeError\n[7, 8]\nTypeError\n" };
        yield return new object[] { "iteration-descriptor-binding-and-slot-refresh", "class Sequence:\n    @property\n    def __getitem__(self):\n        print('bind item')\n        def get(index):\n            if index>=2:\n                raise IndexError\n            return index+3\n        return get\nprint(list(Sequence()))\nclass Iterated:\n    @property\n    def __iter__(self):\n        print('bind iter')\n        return lambda:iter([9])\nprint(list(Iterated()))\nclass Mutable:\n    def __getitem__(self,index):\n        return index\ni=iter(Mutable())\nprint(next(i))\ndef replacement(self,index):\n    if index>=2:\n        raise IndexError\n    return 10+index\nMutable.__getitem__=replacement\nprint(list(i))\n", "bind item\nbind item\nbind item\n[3, 4]\nbind iter\n[9]\n0\n[11]\n" };
        yield return new object[] { "sequence-fallback-standard-library-composition", "import collections\nclass Sequence:\n    def __getitem__(self,index):\n        if index>=3:\n            raise IndexError\n        return index+1\ns=Sequence()\nprint(list(s),tuple(s),sorted(set(s)),list(collections.deque(s)))\nprint([x*2 for x in s],sum(s),list(zip(s,s)))\nprint(*s)\n", "[1, 2, 3] (1, 2, 3) [1, 2, 3] [1, 2, 3]\n[2, 4, 6] 6 [(1, 1), (2, 2), (3, 3)]\n1 2 3\n" };
        yield return new object[] { "iterator-next-slot-descriptors-and-shadowing", "class Iterator:\n    def __init__(self):\n        self.index=0\n    def __iter__(self):\n        return self\n    @property\n    def __next__(self):\n        print('bind next')\n        def advance():\n            if self.index>=2:\n                raise StopIteration\n            self.index+=1\n            return self.index\n        return advance\ni=Iterator()\nprint(iter(i) is i)\nprint(next(i),list(i),next(i,'end'))\nclass Plain:\n    def __iter__(self):\n        return self\n    def __next__(self):\n        raise StopIteration\nplain=Plain()\nplain.__next__=lambda:99\nprint(next(plain,'end'))\n", "True\nbind next\nbind next\nbind next\nbind next\n1 [2] end\nend\n" };
        yield return new object[] { "iterator-next-slot-presence-validation", "class Invalid:\n    __next__=None\n    def __iter__(self):\n        return self\ni=Invalid()\nprint(iter(i) is i)\ntry:\n    next(i)\nexcept TypeError:\n    print('TypeError')\nclass Pretend:\n    def __iter__(self):\n        return self\n    def __getattr__(self,name):\n        if name=='__next__':\n            return lambda:1\n        raise AttributeError(name)\ntry:\n    iter(Pretend())\nexcept TypeError:\n    print('TypeError')\n", "True\nTypeError\nTypeError\n" };
    }
    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task MatchesPython(string name, string source, string expected)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
