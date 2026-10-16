using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ComprehensionLambdaExecutableTests
{
    [Theory]
    [InlineData("result=[lambda:value for value in range(3)]")]
    [InlineData("def make(values):return [lambda:value for value in values]")]
    [InlineData("value='outer'\nresult=[lambda:value for value in range(3)]")]
    [InlineData("def make():\n global value\n return [lambda:value for value in range(3)]")]
    [InlineData("result=[lambda saved=value:lambda:value+saved for value in range(3)]")]
    [InlineData("result=[[lambda:(value,inner) for inner in range(2)] for value in range(3)]")]
    [InlineData("result={value:lambda:value for value in range(3)}")]
    [InlineData("result={lambda:value for value in range(3)}")]
    public void BodiesAndCallerStayExecutableWithoutFallbackMirroring(string source)
    {
        var syntax = LythonFrontend.Compile(source).Script.RequireNotNull();
        Check(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint);

        static void Check(ExecutableCodeObject code)
        {
            Assert.Empty(code.ExpressionFallbacks);
            Assert.Empty(code.StatementFallbacks);
            Assert.False(code.RequiresLocalVariableMirroring);
            foreach (var function in code.Functions) Check(Assert.IsType<ExecutableCodeObject>(function.CodeObject));
            foreach (var lambda in code.Constants.OfType<ExecutableCreateLambda>())
                Check(Assert.IsType<ExecutableCodeObject>(lambda.CodeObject));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedBodiesUseCompiledClosuresWithoutMirroringTheCaller(bool asynchronous)
    {
        var syntax = LythonFrontend.Compile("global result\nresult=[lambda:value for value in range(3)]").Script.RequireNotNull();
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
        var functions = Assert.IsType<PyList>(context.Variables["result"]);
        Assert.Equal(3, functions.Count);
        foreach (var value in functions)
        {
            var function = Assert.IsType<PyExecutableFunction>(value);
            var result = asynchronous ? await function.InvokeAsync([], syntax.Statements[0].Span, context)
                : function.Invoke([], syntax.Statements[0].Span, context);
            Assert.Equal(new BigInteger(2), result);
        }
        Assert.False(context.MirrorLocalStores);
        Assert.False(context.TryGetInlinedBinding("value", out _));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedCellCaptureAdmitsBeforePublishingNamespaceOrRetentionMarks(bool deny)
    {
        const long cap = 1024 * 1024;
        var syntax = LythonFrontend.Compile("def make(offset):return [lambda:(offset,value) for value in range(2)]").Script.RequireNotNull();
        var module = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var outer = Assert.Single(module.Functions).CodeObject.RequireNotNull();
        var lambda = Assert.Single(outer.Constants.OfType<ExecutableCreateLambda>()).CodeObject.RequireNotNull();
        var facts = Assert.Single(outer.Constants.OfType<ExecutableStartComprehension>()).ScopeFacts;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var values = new object[outer.LocalNames.Count];
        var cells = new LythonRuntime.ExecutableCell?[values.Length];
        var parentCell = new LythonRuntime.ExecutableCell(new BigInteger(7));
        cells[outer.LocalNameToSlot["offset"]] = parentCell;
        context.EnterExecutableSlots(new LythonRuntime.ExecutableFrameState(outer, values, cells, null));
        var previousClosure = context.FunctionClosureContext;
        var scope = context.EnterComprehension(facts);
        var held = Lokad.Lython.Runtime.Text.PyString.FromString("held", context.MemoryGovernor, null);
        scope.Bindings.Variables["value"] = held;
        var governor = context.MemoryGovernor;
        var before = governor.CurrentAccountedBytes;
        var padding = deny ? cap - before : 0;
        if (deny) { governor.Reserve(padding, null); governor.Commit(padding); }
        var capture = typeof(LythonRuntime).GetMethod("CaptureExecutableClosures", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ExecutableCodeObject, LythonRuntime.ExecutionContext, LythonSourceSpan, LythonRuntime.ExecutableCell[]>>();
        LythonRuntime.ExecutableCell[]? captured = null;
        long charged = 0;
        try
        {
            if (deny)
            {
                var failure = Assert.Throws<LythonRuntimeException>(() => capture(lambda, context, syntax.Statements[0].Span));
                Assert.Equal("MemoryError", failure.ExceptionType);
                Assert.False(parentCell.RetentionCharged);
                Assert.False(scope.Bindings.MirrorLocalStores);
                Assert.False(context.MirrorLocalStores);
                Assert.Equal(cap, governor.CurrentAccountedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                Assert.Same(held, scope.Bindings.Variables["value"]);
            }
            else
            {
                captured = capture(lambda, context, syntax.Statements[0].Span);
                Assert.Same(parentCell, captured[lambda.ClosureNameToSlot["offset"]]);
                Assert.Same(held, captured[lambda.ClosureNameToSlot["value"]].Value);
                Assert.True(parentCell.RetentionCharged);
                Assert.True(scope.Bindings.MirrorLocalStores);
                Assert.False(context.MirrorLocalStores);
                charged = governor.CurrentAccountedBytes;
                Assert.True(charged > before);
                scope.Bindings.Variables["value"] = new BigInteger(2);

            }
        }
        finally
        {
            if (deny) governor.Release(padding);
            scope.Dispose();
            context.LeaveExecutableSlots(null);
        }
        Assert.Same(previousClosure, context.FunctionClosureContext);
        Assert.False(context.TryGetInlinedBinding("value", out _));
        Assert.Null(context.CurrentExecutableFrame);
        if (!deny)
        {
            Assert.Equal(new BigInteger(2), captured![lambda.ClosureNameToSlot["value"]].Value);
            Assert.Equal(charged, governor.CurrentAccountedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusedConstructionRestoresBindingsAndThePriorTarget(bool asynchronous)
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=[lambda:value for value in range(10000)]").Script.RequireNotNull();
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        var original = new PyList([true], context.MemoryGovernor, null);
        context.Variables["target"] = original;
        var previousClosure = context.FunctionClosureContext;
        var failure = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
        });
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Same(original, context.Variables["target"]);
        Assert.Same(previousClosure, context.FunctionClosureContext);
        Assert.False(context.TryGetInlinedBinding("value", out _));
        Assert.False(context.MirrorLocalStores);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

}
