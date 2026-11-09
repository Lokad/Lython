using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DirectDictionarySetDefaultTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task DistinctReceiversPreserveDefaultsAndCleanup(int width, bool asynchronous)
    {
        var syntax = LythonFrontend.Compile($"for items in [left,right]: result=items.setdefault(key(){(width == 2 ? ",fallback" : "")})\nreturn result");
        Assert.NotNull(syntax.Script);
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax.Script!));
        var root = new LythonMemoryBudget(1024 * 1024);
        using var owner = root.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var key = new KeyProbe(PyString.FromString("x", context.MemoryGovernor, null));
        var marker = new object();
        var left = new PyDict(context.MemoryGovernor);
        var right = new PyDict(context.MemoryGovernor);
        left.SetItem(key.Value, marker);
        context.Variables["left"] = left;
        context.Variables["right"] = right;
        context.Variables["key"] = key;
        context.Variables["fallback"] = marker;
        var pending = asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context) : default;
        if (asynchronous) Assert.True(pending.IsCompleted);
        var result = asynchronous ? await pending : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
        Assert.Same(width == 1 ? PyNone.Instance : marker, result);
        Assert.Equal(2, key.Calls);

        Assert.Same(marker, left.GetItem(key.Value));
        Assert.Same(result, right.GetItem(key.Value));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    private sealed class KeyProbe(object value) : LythonRuntime.ICallable
    {
        public object Value { get; } = value;
        public int Calls { get; private set; }
        public object? Frame { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Frame = context.CurrentExecutableFrame;
            return Value;
        }
    }
}
