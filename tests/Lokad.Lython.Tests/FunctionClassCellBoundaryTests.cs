using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class FunctionClassCellBoundaryTests
{
    private static readonly LythonSourceSpan Span = new(50, 6, 5, 3);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ModuleCallsKeepTheirOwnContextAndEntryBoundary(bool imported, bool mirror)
    {
        var root = Root();
        var closure = imported ? Context.CreateModule(root, "/imported.py", "imported") : root;
        var receiver = new object();
        var frame = Enter(closure, [Parameter("value", FunctionParameterKind.Positional)], [receiver], mirror);
        try
        {
            Assert.Same(closure, frame.ParentContext);
            Assert.Same(closure.Services, frame.Services);
            Assert.Equal(closure.SourcePath, frame.SourcePath);
            Assert.Equal("f", frame.FunctionName);
            Assert.Null(frame.ImplicitSuperClassCell);
            Assert.Null(frame.ImplicitSuperReceiver);
            Assert.False(frame.MissingImplicitSuperClassCell);
            Assert.Equal(mirror, frame.Frame.ContainsVariable("value"));
            if (mirror) Assert.Same(receiver, frame.Variables["value"]);
            Assert.Equal(1, root.Limits.CurrentRecursionDepth);
            Assert.Equal(1, root.Limits.ExecutionStepCount);
        }
        finally { frame.LeaveFunctionCall(); }
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
    }

    [Theory]
    [InlineData((int)FunctionParameterKind.PositionalOnly)]
    [InlineData((int)FunctionParameterKind.Positional)]
    [InlineData((int)FunctionParameterKind.KeywordOnly)]
    [InlineData((int)FunctionParameterKind.VariadicList)]
    [InlineData((int)FunctionParameterKind.VariadicDictionary)]
    public void OwnedModuleFunctionsKeepTheirFirstDeclaredReceiver(int parameterKind)
    {
        var kind = (FunctionParameterKind)parameterKind;
        var root = Root();
        var owner = new PyType("Owner", [], []);
        var receiver = new object();
        var other = new object();
        var parameters = new[] { Parameter("receiver", kind), Parameter("other", FunctionParameterKind.KeywordOnly) };
        // Variadic slots follow named slots even when declared first.
        var values = kind is FunctionParameterKind.VariadicList or FunctionParameterKind.VariadicDictionary
            ? new[] { other, receiver } : new[] { receiver, other };
        var frame = Enter(root, parameters, values, false, owner);
        try
        {
            Assert.Null(frame.ImplicitSuperClassCell);
            Assert.True(frame.MissingImplicitSuperClassCell);
            Assert.Same(receiver, frame.ImplicitSuperReceiver);
        }
        finally { frame.LeaveFunctionCall(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParameterlessFunctionsKeepNoImplicitReceiver(bool owned)
    {
        var root = Root();
        var frame = Enter(root, [], [], false, owned ? new PyType("Owner", [], []) : null);
        try
        {
            Assert.Null(frame.ImplicitSuperReceiver);
            Assert.Null(frame.ImplicitSuperClassCell);
            Assert.False(frame.MissingImplicitSuperClassCell);
        }
        finally { frame.LeaveFunctionCall(); }
    }

    [Fact]
    public void NestedFunctionsRespectLiveClassNamespaceBarriersAcrossCalls()
    {
        var cellScope = ClassClosure();
        var closure = new Context(cellScope, ScopeDirectiveFacts.Empty) { FunctionName = "outer" };
        var receiver = new object();
        AssertBinding();
        closure.Variables["__class__"] = new object();
        AssertBinding(blocked: true);
        closure.Variables.Remove("__class__");
        AssertBinding();

        void AssertBinding(bool blocked = false)
        {
            var frame = Enter(closure, [Parameter("receiver", FunctionParameterKind.Positional)], [receiver], false);
            try
            {
                Assert.Same(blocked ? null : cellScope.ClassCell, frame.ImplicitSuperClassCell);
                Assert.Same(blocked ? null : receiver, frame.ImplicitSuperReceiver);
                Assert.False(frame.MissingImplicitSuperClassCell);
            }
            finally { frame.LeaveFunctionCall(); }
        }
    }

    [Theory]
    [InlineData("def f(receiver):\n    global __class__\n    return super()\n")]
    [InlineData("def f(receiver):\n    __class__ = 7\n    return super()\n")]
    public void CalleeScopeBarriersKeepUnavailableSuperBinding(string source)
    {
        var closure = ClassClosure();
        var definition = Assert.IsType<FunctionDefinitionStatementSyntax>(Assert.Single(Compile(source).Statements));
        var receiver = new object();
        var frame = Enter(closure, [Parameter("receiver", FunctionParameterKind.Positional)], [receiver], false,
            facts: ScopeDirectiveFactsCollector.ForFunction(definition));
        try
        {
            Assert.Null(frame.ImplicitSuperClassCell);
            Assert.True(frame.MissingImplicitSuperClassCell);
            Assert.Same(receiver, frame.ImplicitSuperReceiver);
        }
        finally { frame.LeaveFunctionCall(); }
    }

    [Fact]
    public void CapturedCellKeepsItsIdentityAcrossCompletionAndMutation()
    {
        var closure = ClassClosure();
        var frame = Enter(closure, [Parameter("receiver", FunctionParameterKind.Positional)], [new object()], false);
        try
        {
            var cell = frame.ImplicitSuperClassCell;
            Assert.Same(closure.ClassCell, cell);
            Assert.IsNotType<PyType>(cell!.Value);
            var owner = new PyType("Owner", [], []);
            cell.Value = owner;
            Assert.Same(owner, frame.ImplicitSuperClassCell!.Value);
            cell.Value = PyNone.Instance;
            Assert.Same(PyNone.Instance, frame.ImplicitSuperClassCell.Value);
        }
        finally { frame.LeaveFunctionCall(); }
    }

    private static Context Enter(Context closure, IReadOnlyList<LoweredFunctionParameter> parameters,
        object[] values, bool mirror, PyType? owner = null, ScopeDirectiveFacts? facts = null)
        => PyFunctionBinding.EnterInvocationFrame(closure, facts ?? ScopeDirectiveFacts.Empty,
            new FunctionBindingPlan("f", PythonCallableKind.Function, parameters, new Dictionary<string, object>()),
            new BoundCallArguments(values, default), mirror, owner, Span);

    private static LoweredFunctionParameter Parameter(string name, FunctionParameterKind kind) => new(name, kind, null, null);
    private static Context Root() => new(new MockLythonHost(), new LythonRunOptions { SourcePath = "/root.py" });
    private static Context ClassClosure()
    {
        var definition = Assert.IsType<ClassDefinitionStatementSyntax>(Assert.Single(
            Compile("class C:\n    def f(self):\n        return __class__\n").Statements));
        return Context.CreateClassBody(Root(), definition).FunctionClosureContext;
    }
    private static ScriptSyntax Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return frontend.Script.RequireNotNull();
    }
}
