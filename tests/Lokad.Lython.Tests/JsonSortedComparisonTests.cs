using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Text;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class JsonSortedComparisonTests
{
    [Fact]
    public void SortingPreservesExistingStructuralComparisonHooks()
    {
        const string source = """
            import json
            events=[]
            class Same:
                def __hash__(self):return 1
                def __eq__(self,other):
                    events.append('equal')
                    return True
            first=Same()
            second=Same()
            print(json.dumps({((first,),'b'):1,((second,),'a'):2},skipkeys=True,sort_keys=True),bool(events))
            """;
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
                // Tuple comparison still reaches __eq__ before ordering its text
                // suffix; skipkeys then omits both keys. Exact event counts are
                // a separate sorted-item-pair compatibility issue.
                Assert.Equal("{} True\n", Encoding.UTF8.GetString(context.State.StandardOutput.WrittenSpan));
                Assert.Null(PyStructuralGuard.AmbientContext);
                Assert.False(PyStructuralGuard.GuestDispatchSuppressed);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
