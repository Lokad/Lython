using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Text;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class JsonSortedComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SortingPreservesItemPairComparisonHooksAndFailures(bool failSecondEquality)
    {
        const string source = """
            import json
            events=[]
            class Same:
                def __hash__(self):return 1
                def __eq__(self,other):
                    events.append('equal')
                    if FAIL and len(events)==2:raise ValueError('second comparison')
                    return True
            first=Same()
            second=Same()
            try:print(json.dumps({((first,),'b'):1,((second,),'a'):2},skipkeys=True,sort_keys=True),events)
            except ValueError as error:print(str(error),events)
            """;
        var expected = failSecondEquality ? "second comparison ['equal', 'equal']\n" : "{} ['equal', 'equal']\n";
        AssertExecution(source.Replace("FAIL", failSecondEquality ? "True" : "False"), expected);
    }

    [Fact]
    public void KeysThatBecomeEqualRequireValueOrderingBeforeSkipping()
    {
        const string source = """
            import json
            equal=False
            events=[]
            class Same:
                def __hash__(self):return 1
                def __eq__(self,other):
                    events.append('equal')
                    return equal
            first=Same()
            second=Same()
            mapping={((first,),):2,((second,),):None}
            events.clear()
            equal=True
            try:print(json.dumps(mapping,skipkeys=True,sort_keys=True))
            except TypeError:print('value comparison failed',events)
            """;
        AssertExecution(source, "value comparison failed ['equal']\n");
    }

    private static void AssertExecution(string source, string expected)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.NotNull(frontend.Script);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            try
            {
                using (PyStructuralGuard.PushAmbient(context, new LythonSourceSpan(0, 1, 1, 1)))
                {
                    _ = LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                    Assert.Same(context, PyStructuralGuard.AmbientContext);
                    Assert.False(PyStructuralGuard.GuestDispatchSuppressed);
                }
                Assert.Equal(expected, Encoding.UTF8.GetString(context.State.StandardOutput.WrittenSpan));
                Assert.Null(PyStructuralGuard.AmbientContext);
                Assert.False(PyStructuralGuard.GuestDispatchSuppressed);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
