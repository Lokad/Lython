using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CounterKeyTraversalTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["both comparison passes preserve effects", """
            from collections import Counter
            events=[]
            class Count:
             def __eq__(self,other):events.append('eq');return True
             def __le__(self,other):events.append('le');return True
            left=Counter(a=Count())
            right=Counter(a=Count())
            print(left==right,events)
            events.clear()
            print(left<=right,events)
            """, "True ['eq', 'eq']\nTrue ['le', 'le']\n"];
        yield return ["binary operations resolve equal custom keys", """
            from collections import Counter
            class Key:
             def __init__(self,n):self.n=n
             def __hash__(self):return self.n
             def __eq__(self,other):return isinstance(other,Key) and self.n==other.n
            left=Counter({Key(1):1})
            right=Counter({Key(1):2,Key(2):3})
            for value in [left+right,left-right,left|right,left&right]:
             print(sorted((key.n,count) for key,count in value.items()))
            """, "[(1, 3), (2, 3)]\n[]\n[(1, 2), (2, 3)]\n[(1, 1)]\n"];
        yield return ["count identity does not replace Python equality", """
            from collections import Counter
            events=[]
            class Count:
             def __eq__(self,other):events.append('eq');return False
            value=Count()
            left=Counter(a=value)
            right=Counter(a=value)
            print(left==right,events)
            events.clear()
            print(left==left,events)
            nan=float('nan')
            print(Counter(a=nan)==Counter(a=nan))
            """, "False ['eq']\nFalse ['eq']\nFalse\n"];
        yield return ["source mutation remains observable", """
            from collections import Counter
            class Count:
             def __eq__(self,other):left.clear();return True
            left=Counter(a=Count())
            right=Counter(a=Count())
            try:print(left==right)
            except RuntimeError:print('mutation')
            print(len(left),len(right))
            """, "mutation\n0 1\n"];
        yield return ["greater comparison preserves operand and iteration order", """
            from collections import Counter
            events=[]
            class Count:
             def __init__(self,n):self.n=n
             def __ge__(self,other):events.append(('ge',self.n,other.n));return True
             def __le__(self,other):events.append(('le',self.n,other.n));return True
            left=Counter(a=Count(1))
            right=Counter(a=Count(2))
            print(left>=right,events)
            events.clear()
            print(left.__ge__(right),events)
            """, "True [('ge', 1, 2), ('ge', 1, 2)]\nTrue [('ge', 1, 2), ('ge', 1, 2)]\n"];
        yield return ["missing counts and all native ordering forms", """
            from collections import Counter
            left=Counter(a=1,b=0)
            right=Counter(a=1,c=0)
            print(left==right,left!=right,left<=right,left>=right,left<right,left>right)
            right['a']=2
            print(left<=right,left<right,right>=left,right>left)
            print(Counter()==Counter(),Counter()<=Counter())
            """, "True False True True False False\nTrue True True True\nTrue True\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task KeysAndComparisonsFollowPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = await script.RunAsync(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
