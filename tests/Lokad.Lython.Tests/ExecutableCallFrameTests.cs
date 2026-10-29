using System.Diagnostics;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExecutableCallFrameTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryGuestRecursionDoesNotGrowTheClrCallStack(bool asynchronous)
    {
        const string source = "def descend(n):\n if n==0:return sample()\n return descend(n-1)\nreturn descend(depth)";
        var code = Compile(source);
        var counts = new List<int>();
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var depth in new[] { 1, 64 })
        {
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                context.Variables["depth"] = new BigInteger(depth);
                context.Variables["sample"] = new Sample(counts);
                var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                Assert.Same(PyNone.Instance, result);
                Unwound(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(2, counts.Count);
        Assert.InRange(counts[1] - counts[0], -12, 12);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryCallerRunsFinallyAndRetainsItsGuestTraceback(bool asynchronous)
    {
        const string source = """
def descend(n):
 try:
  if n==0:raise ValueError('leaf')
  return descend(n-1)
 finally:events.append(n)
descend(12)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var original = new PyException("LookupError", "outer", PyNone.Instance);
            context.Services.SetCurrentException(original);
            var events = new PyList([], context.MemoryGovernor, null);
            context.Variables["events"] = events;
            var code = Compile(source);
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            });
            Assert.Equal("ValueError", failure.ExceptionType);
            var guest = Assert.IsType<PyException>(failure.OriginalPythonException);
            Assert.Equal("leaf", Assert.IsType<PyString>(Assert.Single(guest.GetArgs())).AsString());
            Assert.Equal(Enumerable.Range(0, 13).Select(i => new BigInteger(i)), events.Cast<BigInteger>());
            Assert.Equal(13, failure.Frames.Count);
            Assert.All(failure.Frames, frame => Assert.Equal("descend", frame.FunctionName));
            Assert.Same(original, context.Services.CurrentException);
            Unwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    private static void Unwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Sample(List<int> counts) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Empty(arguments);
            Assert.True(context.TryResolveOwnLocal("n", out var value));
            Assert.Equal(BigInteger.Zero, value);
            // Managed test diagnostic only; no runtime stack telemetry/admission.
            counts.Add(new StackTrace().FrameCount);
            return PyNone.Instance;
        }
    }
}
