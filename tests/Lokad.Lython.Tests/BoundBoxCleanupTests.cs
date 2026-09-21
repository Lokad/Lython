using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// R19: bound-values boxes rented from the pool return on every exit path.
// Arity failures inside BindFunctionArguments, recursion/budget failures in
// EnterInvocationFrame, and the frameless dataclass dunders all hand the box
// back; otherwise each failure strands one pooled array (losing the pool slot
// and retaining argument refs until GC). Pool reuse is observed through array
// identity, so the tests are exact instead of timing-sensitive.
public sealed class BoundBoxCleanupTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    private static FunctionBindingPlan TwoParameterPlan() => new(
        "f",
        PythonCallableKind.Function,
        [
            new LoweredFunctionParameter("a", FunctionParameterKind.Positional, null, null),
            new LoweredFunctionParameter("b", FunctionParameterKind.Positional, null, null),
        ],
        new Dictionary<string, object>());

    [Fact]
    public void FailingBindsReturnRentedBox()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var plan = TwoParameterPlan();
        var first = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)], Span, plan, context).Values;
        LythonRuntime.ReturnBoundValues(first);

        // One argument for two required parameters: the rent already happened,
        // so a leaked box would strand the pooled array here.
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            LythonRuntime.BindFunctionArguments([CallArgumentValue.Positional(1)], Span, plan, context));
        Assert.Equal("TypeError", failure.ExceptionType);

        var second = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)], Span, plan, context).Values;
        LythonRuntime.ReturnBoundValues(second);
        Assert.Same(first, second);
    }

    [Fact]
    public void FrameEntryFailuresReturnRentedBox()
    {
        var host = new MockLythonHost();
        // The frame hangs off the closure's guards, so the closure itself
        // carries the zero recursion cap that trips frame entry.
        var closure = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxRecursionDepth = 0 });
        var parameters = new List<LoweredFunctionParameter>
        {
            new("a", FunctionParameterKind.Positional, null, null),
            new("b", FunctionParameterKind.Positional, null, null),
        };
        var fun = new PyFunction(
            "f",
            parameters,
            [],
            closure,
            new Dictionary<string, object>(),
            ScopeDirectiveFacts.Empty);
        var plan = TwoParameterPlan();
        var box = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)], Span, plan, closure).Values;
        LythonRuntime.ReturnBoundValues(box);

        // Zero recursion depth trips inside EnterInvocationFrame, after the
        // Invoke rent: the box must come back before the error propagates.
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            fun.Invoke([CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)], Span, closure));
        Assert.Equal("RecursionError", failure.ExceptionType);

        var again = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)], Span, plan, closure).Values;
        LythonRuntime.ReturnBoundValues(again);
        Assert.Same(box, again);
    }

    [Fact]
    public void DataclassInitsReturnRentedBox()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var fields = new List<DataclassFieldSpec>
        {
            new("x", PyNone.Instance, DataclassFieldKind.Normal, false, PyNone.Instance, false, PyNone.Instance, true, true, true, null, false, PyNone.Instance, true),
            new("y", PyNone.Instance, DataclassFieldKind.Normal, false, PyNone.Instance, false, PyNone.Instance, true, true, true, null, false, PyNone.Instance, true),
        };
        var init = new PyDataclass.DataclassInitMethod("P", fields, null);
        var type = new PyType("P", [], new Dictionary<string, object>());
        var plan = new FunctionBindingPlan(
            "P.__init__",
            PythonCallableKind.Function,
            [
                new LoweredFunctionParameter("self", FunctionParameterKind.Positional, null, null),
                new LoweredFunctionParameter("x", FunctionParameterKind.Positional, null, null),
                new LoweredFunctionParameter("y", FunctionParameterKind.Positional, null, null),
            ],
            new Dictionary<string, object>());
        var box = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2), CallArgumentValue.Positional(3)],
            Span, plan, context).Values;
        LythonRuntime.ReturnBoundValues(box);

        var instance = new PyInstance(type, context.MemoryGovernor, null);
        init.Invoke(
            [CallArgumentValue.Positional(instance), CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)],
            Span, context);

        var again = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2), CallArgumentValue.Positional(3)],
            Span, plan, context).Values;
        LythonRuntime.ReturnBoundValues(again);
        Assert.Same(box, again);
        GC.KeepAlive(instance);
    }
}
