using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FunctoolsCacheBorrowTests
{
    [Fact]
    public void ReentrantClearRetainsFundingUntilEqualityHasFinished()
    {
        const string source = """
            import functools
            enabled=[False]
            @functools.lru_cache(maxsize=2)
            def f(x): return 7
            class E:
                def __hash__(self): return 1
                def __eq__(self,other):
                    if enabled[0]:
                        enabled[0]=False
                        observe()
                        f.cache_clear()
                        observe()
                    return True
            f(E())
            enabled[0]=True
            f(E())
            """;
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var observations = new List<long>();
            context.Variables["observe"] = new Observe(observations);
            _ = LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            Assert.Equal(2, observations.Count);
            Assert.Equal(observations[0], observations[1]);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Observe(List<long> observations) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            observations.Add(context.MemoryGovernor.CurrentCommittedBytes);
            return PyNone.Instance;
        }
    }
}
