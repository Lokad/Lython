using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class SuperDescriptorFundingBoundaryTests
{
    private static readonly LythonSourceSpan Span = new(70, 7, 8, 4);

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public void ImplicitAndExplicitSuperKeepDescriptorChargeAndDenialSpan(int shape, bool denied)
    {
        var context = Root();
        var anchor = new PyType("Base", [], []);
        var derived = new PyType("Derived", [anchor], []);
        var instance = new PyInstance(derived);
        var receiver = shape == 3 ? (object)derived : instance;
        if (shape == 0) context.BindImplicitSuper(new LythonRuntime.ExecutableCell(anchor), receiver);
        CallArgumentValue[] arguments = shape == 0 ? [] : shape == 1
            ? [CallArgumentValue.Positional(anchor)]
            : [CallArgumentValue.Positional(anchor), CallArgumentValue.Positional(receiver)];
        var governor = context.MemoryGovernor;
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        if (denied)
        {
            var pressure = 8192 - governor.CurrentAccountedBytes - 63;
            governor.Reserve(pressure, Span);
            governor.Commit(pressure);
        }
        var before = governor.CurrentCommittedBytes;
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["super"]);
        if (denied)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke(arguments, Span, context));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Same(Span, error.Span);
            Assert.Equal(64, governor.LastDeniedReservationBytes);
            Assert.Equal(before, governor.CurrentCommittedBytes);
        }
        else
        {
            var value = Assert.IsType<PySuper>(callable.Invoke(arguments, Span, context));
            Assert.Same(anchor, value.AnchorType);
            Assert.Same(shape == 1 ? null : receiver, value.BoundObject);
            Assert.Same(shape == 1 ? null : derived, value.BoundType);
            Assert.Equal(before + 64, governor.CurrentCommittedBytes);
        }
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Same(active, context.Services.CurrentException);
        Assert.Equal(1, context.Limits.ExecutionStepCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyAndInvalidClassCellsFailBeforeDescriptorFunding(bool invalid)
    {
        var context = Root();
        var frontend = LythonFrontend.Compile("class C:\n    def method(self):\n        return super()\n");
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var definition = Assert.IsType<ClassDefinitionStatementSyntax>(Assert.Single(frontend.Script.RequireNotNull().Statements));
        var cell = Context.CreateClassBody(context, definition).ClassCell!;
        if (invalid) cell.Value = PyNone.Instance;
        context.BindImplicitSuper(cell, new object());
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["super"]);
        var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke([], Span, context));
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Equal(invalid ? "super(): __class__ is not a supported type" : "super(): empty __class__ cell", error.Message);
        Assert.Same(Span, error.Span);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void MissingCellPrecedesAnOtherwiseValidCellAndReceiver()
    {
        var context = Root();
        var anchor = new PyType("Base", [], []);
        var receiver = new PyInstance(anchor);
        context.BindUnavailableImplicitSuper(receiver);
        context.BindImplicitSuper(new LythonRuntime.ExecutableCell(anchor), receiver);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["super"]);
        var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke([], Span, context));
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Equal("super(): __class__ cell not found", error.Message);
        Assert.Same(Span, error.Span);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
    }

    private static Context Root() => new(new MockLythonHost(), new LythonRunOptions
    {
        MaxExecutionMemoryBytes = 8192, MaxExecutionSteps = 1000
    });
}
