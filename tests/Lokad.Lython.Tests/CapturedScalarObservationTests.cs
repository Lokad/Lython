using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CapturedScalarObservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmissionObservesCurrentCounterAndRetainedOldIdentity(bool asynchronous)
    {
        const string source = """
            def analyze():
                processed=0
                def words_from_line(line):
                    nonlocal processed
                    processed+=1
                    return line.lower().split()
                observe(words_from_line)
                count=0
                for line in [payload,payload]:
                    for word in words_from_line(line):count+=1
                return processed,count
            return analyze()
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            context.Variables["payload"] = PyString.FromString("ALPHA BETA", context.MemoryGovernor, null);
            var observer = new ObserveCounter(hook);
            context.Variables["observe"] = observer;
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!));
            try
            {
                var result = asynchronous
                    ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
                var values = Assert.IsType<PyTuple>(result);
                Assert.Equal(new BigInteger(2), values[0]);
                Assert.Equal(new BigInteger(4), values[1]);
                Assert.Equal(BigInteger.One, observer.OldValue);
                var current = Assert.Single(observer.Function!.ClosureCells).Value;
                Assert.Equal(new BigInteger(2), current);
                Assert.Same(current, Assert.Single(observer.Function.ClosureCells).Value);
                Assert.NotSame(observer.OldValue, current);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { hook.AfterGrowth = null; context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class ObserveCounter(AdmissionHookMemoryBudget hook) : LythonRuntime.ICallable
    {
        internal object? OldValue;
        internal PyExecutableFunction? Function;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var function = Assert.IsType<PyExecutableFunction>(Assert.Single(arguments).Value);
            Function = function;
            var cell = Assert.Single(function.ClosureCells);
            hook.AfterGrowth = _ =>
            {
                if (cell.Value is not BigInteger count || count != BigInteger.One) return;
                hook.AfterGrowth = null;
                OldValue = cell.Value;
                Assert.Same(OldValue, cell.Value);
            };
            return PyNone.Instance;
        }
    }
}
