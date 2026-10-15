using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PositionalCallTransportTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task CompiledPositionalCallsDoNotBorrowThePackagedArgumentPool(int width, bool asynchronous)
    {
        var parameters = string.Join(",", Enumerable.Range(0, width).Select(i => "a" + i));
        var arguments = string.Join(",", Enumerable.Repeat("value", width));
        var frontend = LythonFrontend.Compile($"def f({parameters}):\n    return a0\nreturn f({arguments})\n");
        var script = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var marker = new object();
        context.Variables["value"] = marker;
        var field = typeof(LythonRuntime).GetField(width == 1 ? "_pooledOneCallArgument" : width == 2 ? "_pooledTwoCallArguments" : "_pooledThreeCallArguments",
            BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        var sentinel = Enumerable.Repeat(CallArgumentValue.Positional(marker), width).ToArray();
        field.SetValue(null, sentinel);
        try
        {
            object? result;
            if (asynchronous)
            {
                var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(script.EntryPoint, context);
                Assert.True(pending.IsCompleted);
                result = await pending;
            }
            else result = LythonRuntime.ExecuteExecutableCodeObject(script.EntryPoint, context, default, [], null);
            Assert.Same(marker, result);
            Assert.Same(sentinel, field.GetValue(null));
            foreach (var argument in sentinel) Assert.Same(marker, argument.Value);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { field.SetValue(null, previous); }
    }
}
