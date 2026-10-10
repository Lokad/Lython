using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExecutionContextParentBoundaryTests
{
    [Fact]
    public void RootAndImportedModuleHaveIndependentLexicalRoots()
    {
        var root = Root();
        root.Variables["template_only"] = 1;
        var module = Context.CreateModule(root, "/module.py", "module");
        Assert.Null(root.ParentContext);
        Assert.Null(root.Frame.Parent);
        Assert.Null(module.ParentContext);
        Assert.Null(module.Frame.Parent);
        Assert.Same(root.Services, module.Services);
        Assert.Same(root, root.FunctionClosureContext);
        Assert.Same(module, module.FunctionClosureContext);
        Assert.Equal("/module.py", module.SourcePath);
        Assert.False(module.Variables.ContainsKey("template_only"));
        Assert.NotSame(root.Variables, module.Variables);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryAndFunctionChildrenKeepTheActualParentAndIndependentNamespace(bool functionScope)
    {
        var root = Root();
        var child = functionScope ? new Context(root, ScopeDirectiveFacts.Empty) : new Context(root);
        AssertParent(child, root);
        Assert.Same(child, child.FunctionClosureContext);
        child.Variables["local"] = 1;
        Assert.False(root.Variables.ContainsKey("local"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedClassSkipsTheOuterClassNamespaceAndKeepsItsClosureParent(bool classCell)
    {
        var root = Root();
        var outer = Context.CreateClassBody(root, Class(classCell));
        outer.Variables["class_only"] = 1;
        var nested = Context.CreateClassBody(outer, Class(false));
        var lexicalParent = outer.FunctionClosureContext;
        AssertParent(outer, root);
        AssertParent(nested, lexicalParent);
        Assert.NotSame(outer, nested.ParentContext);
        Assert.Same(lexicalParent, nested.FunctionClosureContext);
        Assert.False(nested.Variables.ContainsKey("class_only"));
        if (classCell)
        {
            Assert.NotSame(root, lexicalParent);
            AssertParent(lexicalParent, root);
            Assert.NotNull(outer.ClassCell);
            Assert.Same(outer.ClassCell, lexicalParent.ClassCell);
        }
        else Assert.Same(root, lexicalParent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ComprehensionKeepsItsLookupParentAndClassClosureNamespace(int shape)
    {
        var root = Root();
        var parent = shape == 0 ? root : shape == 1 ? new Context(root, ScopeDirectiveFacts.Empty) : Context.CreateClassBody(root, Class(true));
        var comprehension = Context.CreateComprehension(parent);
        AssertParent(comprehension, parent);
        if (shape == 2)
        {
            var closure = comprehension.FunctionClosureContext;
            Assert.NotSame(comprehension, closure);
            AssertParent(closure, parent.FunctionClosureContext);
            Assert.Same(comprehension.Variables, closure.Variables);
            comprehension.Variables["iteration_value"] = 7;
            Assert.Equal(7, closure.Variables["iteration_value"]);
            Assert.False(parent.Variables.ContainsKey("iteration_value"));
        }
        else Assert.Same(comprehension, comprehension.FunctionClosureContext);
    }

    [Fact]
    public void ClassNonlocalTargetRemainsTheEnclosingFunction()
    {
        var syntax = Compile("def outer():\n    value = 7\n    class C:\n        nonlocal value\n        pass\n");
        var definition = Assert.IsType<FunctionDefinitionStatementSyntax>(Assert.Single(syntax.Statements));
        var classDefinition = Assert.IsType<ClassDefinitionStatementSyntax>(definition.Body[1]);
        var root = Root();
        var function = new Context(root, ScopeDirectiveFactsCollector.ForFunction(definition));
        function.Variables["value"] = 7;
        var body = Context.CreateClassBody(function, classDefinition);
        AssertParent(function, root);
        AssertParent(body, function);
        Assert.True(body.TryGetNonlocalTarget("value", out var target));
        Assert.Same(function, target);
        Assert.Equal(7, target.Variables["value"]);
    }

    private static void AssertParent(Context child, Context expected)
    {
        Assert.Same(expected, child.ParentContext);
        Assert.Same(expected.Frame, child.Frame.Parent);
        Assert.Same(expected.Services, child.Services);
        Assert.Same(expected.SourcePath, child.SourcePath);
    }

    private static Context Root() => new(new MockLythonHost(), new LythonRunOptions { SourcePath = "/root.py" });

    private static ClassDefinitionStatementSyntax Class(bool classCell) => Assert.IsType<ClassDefinitionStatementSyntax>(
        Assert.Single(Compile(classCell ? "class C:\n    def method(self):\n        return __class__\n" : "class C:\n    pass\n").Statements));

    private static ScriptSyntax Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return frontend.Script.RequireNotNull();
    }
}
