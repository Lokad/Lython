using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class GeneralBoundMethodTransportTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task KeywordMethodsKeepEveryGuardAndBodyEffect(bool asynchronous, bool defaulted)
    {
        const string definitionSource = "class Box:\n def method(self,*,value=7):\n  stamp(self,value)\n  return value\nbox=Box()\nmethod=box.method";
        var definition = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(definitionSource,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var callSource = defaulted ? "return method()" : "return method(value=value)";
        var call = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(callSource,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var marker = new object();
        string[] fullEffects;
        using (var owner = budget.CreateScope())
        {
            var (context, tape) = await Prepare(owner);
            var result = await Execute(call, context);
            if (defaulted) Assert.Equal(new BigInteger(7), result);
            else Assert.Same(marker, result);
            fullEffects = tape.Events.ToArray();
            Assert.Single(fullEffects);
            Unwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(["body:3,1,0"], fullEffects);
        string[] expected = defaulted
            ? ["-:","7,6,1,8:","7,8,1,8:","7,8,1,8:","7,8,1,8:","7,8,1,8:","7,8,1,8:","42,17,3,3:","42,5,3,3:","42,17,3,3:","42,17,3,3:","48,4,3,9:","53,5,3,14:","69,5,4,10:body:3,1,0","0,15,1,1:body:3,1,0"]
            : ["-:","7,6,1,8:","20,5,1,21:","7,19,1,8:","7,19,1,8:","7,19,1,8:","7,19,1,8:","7,19,1,8:","42,17,3,3:","42,5,3,3:","42,17,3,3:","42,17,3,3:","48,4,3,9:","53,5,3,14:","69,5,4,10:body:3,1,0","0,26,1,1:body:3,1,0"];

        async ValueTask<(LythonRuntime.ExecutionContext, MethodEffects)> Prepare(ILythonMemoryBudget owner)
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
            var tape = new MethodEffects(defaulted ? new BigInteger(7) : marker);
            context.Variables["value"] = marker;
            context.Variables["stamp"] = tape;
            await Execute(definition, context);
            Assert.IsType<PyFunction>(Assert.IsType<PyBoundMethod>(context.Variables["method"]).Function);
            tape.Receiver = context.Variables["box"];
            return (context, tape);
        }

        async ValueTask<object?> Execute(ExecutableCodeObject code, LythonRuntime.ExecutionContext context)
            => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

        static void Unwound(LythonRuntime.ExecutionContext context)
        {
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    private sealed class MethodEffects(object expected) : LythonRuntime.ICallable
    {
        internal object? Receiver;
        internal List<string> Events { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.Equal(2, arguments.Length);
            Assert.Same(Receiver, arguments[0].Value);
            Assert.Equal(expected, arguments[1].Value);
            Assert.Null(context.CurrentExecutableFrame);
            Events.Add($"body:{context.Limits.CurrentInterpreterDepth},{context.Limits.CurrentRecursionDepth},{context.MemoryGovernor.CurrentReservedBytes}");
            return PyNone.Instance;
        }
    }

}
