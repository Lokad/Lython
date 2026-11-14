using Lokad.Lython.Tests.Harness;
using System.Numerics;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LiteralTupleExecutionTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["nested-values","def f():return (1,'café',b'abc',True,None,(2,3),1.25,2j,...)\na=f();b=f()\nprint(a,a==b,type(a).__name__,type(a[5]).__name__)\n","(1, 'café', b'abc', True, None, (2, 3), 1.25, 2j, Ellipsis) True tuple tuple\n"];
        yield return ["mutable-members","def f(x):return (x,[])\na=f(1);b=f(1)\na[1].append(7)\nprint(a,b,a is b,a[1] is b[1])\n","(1, [7]) (1, []) False False\n"];
        yield return ["call-order","events=[]\ndef value(x):\n events.append(x)\n return x\ndef f():return (value(1),(value(2),value(3)),value(4))\nprint(f(),events)\n","(1, (2, 3), 4) [1, 2, 3, 4]\n"];
        yield return ["starred-generator","events=[]\ndef values():\n events.append('start')\n yield (1,2)\n events.append('end')\nt=(*values(),(3,4))\nprint(t,events)\n","((1, 2), (3, 4)) ['start', 'end']\n"];
        yield return ["conditional-members","def f(flag):return ((1,2),3 if flag else 4,(5,6))\nprint(f(True),f(False))\n","((1, 2), 3, (5, 6)) ((1, 2), 4, (5, 6))\n"];
        yield return ["fresh-containers","def f():return ((1,2),[],{})\na=f();b=f()\na[1].append('x');a[2]['key']=1\nprint(a,b,a[0]==b[0],a[1] is b[1],a[2] is b[2])\n","((1, 2), ['x'], {'key': 1}) ((1, 2), [], {}) True False False\n"];
        yield return ["exception-order","events=[]\ndef fail():\n events.append('fail')\n raise ValueError('stop')\ndef later():\n events.append('later')\n return 3\ntry:t=((1,2),fail(),later())\nexcept ValueError:print(events)\n","['fail']\n"];
        yield return ["loop-fallback","totals={}\nfor x in range(4):\n old=totals.get('key',(0,0,0,0))\n totals['key']=(old[0]+1,old[1]+x,old[2]+2*x,old[3]+3*x)\nprint(totals)\n","{'key': (4, 6, 12, 18)}\n"];
        yield return ["dictionary-keys","a={(1,(2,3)):'one',(True,(2,3)):'two'}\nprint(len(a),a[(1,(2,3))],(1,(2,3)) in a)\n","1 two True\n"];
        yield return ["defaults-and-closures","def make():\n default=(1,(2,3))\n def f(value=default):return value\n return f\na=make();b=make()\nprint(a(),b(),a() is a(),a()==b())\n","(1, (2, 3)) (1, (2, 3)) True True\n"];
        yield return ["function-and-generator","def f():\n yield (1,(2,3))\n yield (4,(5,6))\nprint(list(f()))\n","[(1, (2, 3)), (4, (5, 6))]\n"];
        yield return ["slice-and-concat","t=(1,2,3,4,5)\nprint(t[:],t[::-1],t[1::2],t[4:0:-2],t[8:],t[:0:-2])\nprint(t+(6,7),(8,)+t,t+(),()+t)\n","(1, 2, 3, 4, 5) (5, 4, 3, 2, 1) (2, 4) (5, 3) () (5, 3)\n(1, 2, 3, 4, 5, 6, 7) (8, 1, 2, 3, 4, 5) (1, 2, 3, 4, 5) (1, 2, 3, 4, 5)\n"];
        yield return ["nested-derived-aliases","a=((1,2),(3,4))\nb=a[::-1]+a\nprint(b,b[0] is a[1],b[2] is a[0],a)\n","((3, 4), (1, 2), (1, 2), (3, 4)) True True ((1, 2), (3, 4))\n"];
        yield return ["slice-index-io","class Bound:\n def __index__(self):\n  with open('bound.txt') as source:return int(source.read())\nt=(1,2,3,4)\nprint(t[Bound():],t[:Bound()])\n","(2, 3, 4) (1,)\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task ImmutableAndDynamicTuplesPreservePythonValuesAndEffects(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/bound.txt", "1");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/bound.txt", "1");
            using (var result = script.Run(immediate))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            if (name == "slice-index-io")
            {
                Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.Equal(2, immediate.ClosedFileCount);
                Assert.Equal(2, delayed.ClosedFileCount);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectedConstantsStayFundedAfterCompilationEnds(bool derived)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        var source = derived ? "value=(1000,(2000,3000),'café'); return value[:]+(0,)" : "return (1000,(2000,3000),'café')";
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var retained = budget.ReservedBytes;
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.True(budget.ReservedBytes > retained);
                Assert.NotSame(first.ReturnValue, second.ReturnValue);
            }
            Assert.Equal(retained, budget.ReservedBytes);
            script.Dispose();
            Assert.True(budget.ReservedBytes > 0);
            var tuple = Assert.IsType<object?[]>(first.ReturnValue);
            Assert.Equal(new BigInteger(1000), tuple[0]);
            Assert.Equal(new BigInteger(3000), Assert.IsType<object?[]>(tuple[1])[1]);
            Assert.Equal("café", tuple[2]);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
