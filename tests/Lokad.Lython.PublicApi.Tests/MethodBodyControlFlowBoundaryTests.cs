using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MethodBodyControlFlowBoundaryTests
{
    [Fact]
    public async Task NestedLoopContinueThroughFinallyAndLoopElse()
        => await AssertOutput("""
            events=[]
            def mark(label,value):events.append((label,value));return value
            class Runner:
             def run(self):
              result=0
              for i in range(3):
               if i==1:
                try:continue
                finally:mark('continue',i)
               while True:
                result+=mark('value',i)
                break
              else:result+=10
              return result
            print(Runner().run(),events)
            """, "12 [('value', 0), ('continue', 1), ('value', 2)]\n");

    [Fact]
    public async Task BreakInInnerLoopElseTargetsOuterLoop()
        => await AssertOutput("""
            events=[]
            class Runner:
             def run(self):
              for i in range(3):
               while False:events.append('body')
               else:
                events.append(i)
                break
              else:events.append('outer else')
              return events
            print(Runner().run())
            """, "[0]\n");

    [Fact]
    public async Task ReturnInsideContextManagerRestoresExceptionAndRunsExit()
        => await AssertOutput("""
            events=[]
            class Scope:
             def __enter__(self):events.append('enter');return self
             def __exit__(self,t,v,b):events.append(t is None)
            class Runner:
             def run(self):
              for i in range(2):
               with Scope():
                if i==1:return 7
              return -1
            print(Runner().run(),events)
            """, "7 ['enter', True, 'enter', True]\n");

    [Fact]
    public async Task ShortCircuitsConditionalAndDeepOperandsKeepEvaluationOrder()
        => await AssertOutput("""
            events=[]
            def mark(x):events.append(x);return x
            class Runner:
             def run(self):
              first=(mark(0) and mark(99)) or mark(2)
              second=mark(3) if mark(1) else mark(98)
              deep=mark(1)+(mark(2)+(mark(3)+(mark(4)+(mark(5)+(mark(6)+(mark(7)+(mark(8)+mark(9))))))))
              return first,second,deep
            print(Runner().run(),events)
            """, "(2, 3, 45) [0, 2, 1, 3, 1, 2, 3, 4, 5, 6, 7, 8, 9]\n");

    [Fact]
    public async Task FiveNestedIteratorsAndOpaqueReturnKeepOuterFinally()
        => await AssertOutput("""
            events=[]
            class Runner:
             def run(self):
              for a in [1]:
               for b in [2]:
                for c in [3]:
                 for d in [4]:
                  for e in [5]:
                   try:return a+b+c+d+e
                   finally:events.append('finally')
              return 0
            print(Runner().run(),events)
            """, "15 ['finally']\n");

    [Fact]
    public async Task LocalsSnapshotMutationLeavesActualLocalsIntact()
        => await AssertOutput("""
            saved=None
            def mutate():saved['value']=9;return 2
            class Runner:
             def run(self):
              global saved
              value=1
              saved=locals()
              result=mutate()+value
              return result,value,saved['value']
            print(Runner().run())
            """, "(3, 1, 9)\n");

    [Fact]
    public async Task GlobalNamespaceMutationIsReadByLaterOperands()
        => await AssertOutput("""
            value=1
            def mutate():
             global value
             value=9
             return 2
            class Runner:
             def run(self):return mutate()+value,value
            print(Runner().run())
            """, "(11, 9)\n");

    private static async Task AssertOutput(string source, string expected)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
