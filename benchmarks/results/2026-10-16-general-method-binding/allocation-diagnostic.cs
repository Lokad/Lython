using System.Text.Json;
using Xunit.Abstractions;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class GeneralMethodAllocationDiagnosticTests(ITestOutputHelper output)
{
    [Fact]
    public void CompareExistingReceiverEntryWithPrefixedControl()
    {
        const string source = "class Box:\n def method(self,value,*,flag=0):return value\nbox=Box()\nmethod=box.method";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(64 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
        var method = Assert.IsType<PyBoundMethod>(context.Variables["method"]);
        var function = Assert.IsType<PyFunction>(method.Function);
        var value = new object();
        var span = new LythonSourceSpan(0, 1, 1, 1);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(value)];
        CallArgumentValue[] prefixed = [CallArgumentValue.Positional(method.Receiver), CallArgumentValue.Positional(value)];
        for (var i = 0; i < 32; i++)
        {
            Assert.Same(value, method.Invoke(arguments, span, context));
            Assert.Same(value, function.Invoke(prefixed, span, context));
        }
        const int iterations = 256;
        var samples = new List<object>();
        for (var round = 0; round < 3; round++)
        {
            long bound, control;
            if (round % 2 == 0) { control = Measure(false); bound = Measure(true); }
            else { bound = Measure(true); control = Measure(false); }
            samples.Add(new { iterations, bound, control, difference = bound - control });
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("GENERAL_METHOD_ALLOCATION:" + JsonSerializer.Serialize(samples));

        long Measure(bool receiverEntry)
        {
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++)
            {
                var result = receiverEntry ? method.Invoke(arguments, span, context) : function.Invoke(prefixed, span, context);
                if (!ReferenceEquals(value, result)) throw new InvalidOperationException("Receiver transport changed the result.");
            }
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }
    }
}
