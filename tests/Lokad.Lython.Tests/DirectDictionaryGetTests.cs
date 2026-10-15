using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DirectDictionaryGetTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task DistinctReceiversAvoidMethodAndArgumentPackaging(int width, bool asynchronous)
    {
        var (code, context) = Prepare($"for items in [left,right]: result=items.get(key(){(width == 2 ? ",fallback" : "")})\nreturn result\n");
        var key = new KeyProbe(PyString.FromString("x", context.MemoryGovernor, null));
        var marker = new object();
        var left = new PyDict(context.MemoryGovernor);
        left.SetItem(key.Value, marker);
        var right = new PyDict(context.MemoryGovernor);
        context.Variables["left"] = left;
        context.Variables["right"] = right;
        context.Variables["key"] = key;
        context.Variables["fallback"] = marker;
        var field = typeof(LythonRuntime).GetField(width == 1 ? "_pooledOneCallArgument" : "_pooledTwoCallArguments", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previous = field.GetValue(null);
        var sentinel = Enumerable.Repeat(CallArgumentValue.Positional(marker), width).ToArray();
        field.SetValue(null, sentinel);
        try
        {
            var pending = asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context) : default;
            if (asynchronous) Assert.True(pending.IsCompleted);
            var result = asynchronous ? await pending : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
            Assert.Same(width == 1 ? PyNone.Instance : marker, result);
            Assert.Equal(2, key.Calls);
            Assert.Same(sentinel, field.GetValue(null));
            Assert.All(sentinel, item => Assert.Same(marker, item.Value));
            var caches = (Array)key.Frame!.GetType().GetField("_memberCaches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(key.Frame)!;
            Assert.All(caches.Cast<object?>(), value => Assert.Null(value));
            Assert.Single(left);
            Assert.Empty(right);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { field.SetValue(null, previous); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallEntryKeepsEveryFuelAndSpanBoundary(bool asynchronous)
    {
        for (var fuel = 1; fuel <= 9; fuel++)
        {
            var (code, context) = Prepare("return items.get(key)\n", new LythonRunOptions { MaxExecutionSteps = fuel });
            var key = PyString.FromString("x", context.MemoryGovernor, null);
            var items = new PyDict(context.MemoryGovernor);
            var marker = new object();
            items.SetItem(key, marker);
            context.Variables["items"] = items;
            context.Variables["key"] = key;
            if (fuel == 9)
                Assert.Same(marker, asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
            else
            {
                var error = asynchronous
                    ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
                    : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
                Assert.Equal("RuntimeError", error.ExceptionType);
                Assert.Equal(fuel + 1, context.Limits.ExecutionStepCount);
                var instructions = code.EntryPoint.Blocks[code.EntryPoint.EntryBlockIndex].InstructionArray;
                Assert.Same(instructions[fuel <= 4 ? fuel - 1 : fuel == 8 ? 4 : 3].Span, error.Span);
            }
            Assert.Single(items);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedCandidateStoragePrecedesGuestEquality(bool asynchronous)
    {
        const long limit = 64 * 1024;
        var (initial, context) = Prepare("class Key:\n    def __hash__(self): return hash('x')\n    def __eq__(self,other):\n        equality()\n        return True\nd={Key():7}\n", new LythonRunOptions { MaxExecutionMemoryBytes = limit });
        var equality = new KeyProbe(PyNone.Instance);
        context.Variables["equality"] = equality;
        LythonRuntime.ExecuteExecutableCodeObject(initial.EntryPoint, context, default, [], null);
        context.Variables["key"] = PyString.FromString("x", context.MemoryGovernor, null);
        var source = LythonFrontend.Compile("return d.get(key)\n");
        var code = ExecutableScript.Compile(LoweredScript.Lower(source.Script.RequireNotNull()));
        var dictionary = Assert.IsType<PyDict>(context.Variables["d"]);
        var baseline = context.MemoryGovernor.CurrentAccountedBytes;
        using (context.MemoryGovernor.ReserveTemporary(limit - baseline, null))
        {
            var error = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
                : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, equality.Calls);
            Assert.Single(dictionary);
        }
        Assert.Equal(baseline, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static (ExecutableScript, LythonRuntime.ExecutionContext) Prepare(string source, LythonRunOptions? options = null)
    {
        var frontend = LythonFrontend.Compile(source);
        return (ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())), new(new MockLythonHost(), options ?? new()));
    }

    private sealed class KeyProbe(object value) : LythonRuntime.ICallable
    {
        internal object Value { get; } = value;
        internal int Calls;
        internal LythonRuntime.ExecutableFrameState? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Frame = context.CurrentExecutableFrame;
            return Value;
        }
    }
}
