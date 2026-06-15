using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutionStateSubsystemTests
{
    [Fact]
    public void ExecutionState_OwnsRunLocalCachesAndBuiltinInventory()
    {
        var host = new MockLythonHost();
        var state = new ExecutionState(host, options: null);

        Assert.Same(host, state.Host);
        Assert.Empty(state.ImportedModules);
        Assert.Empty(state.LoadingModules);
        Assert.Contains("str", ExecutionState.BuiltinNames);
        Assert.Contains("repr", ExecutionState.BuiltinNames);
        Assert.Contains("sum", ExecutionState.BuiltinNames);
        Assert.Contains("bytes", ExecutionState.BuiltinNames);
        Assert.Contains("divmod", ExecutionState.BuiltinNames);
        Assert.Contains("TypeError", ExecutionState.BuiltinNames);
    }

    [Theory]
    [InlineData("append_text")]
    [InlineData("basename")]
    [InlineData("copy")]
    [InlineData("cwd")]
    [InlineData("dirname")]
    [InlineData("exists")]
    [InlineData("join_path")]
    [InlineData("listdir")]
    [InlineData("mkdir")]
    [InlineData("move")]
    [InlineData("read_text")]
    [InlineData("remove")]
    [InlineData("stat")]
    [InlineData("write_text")]
    public void ExecutionState_BuiltinInventoryExcludesNonPythonHostHelpers(string name)
    {
        Assert.DoesNotContain(name, ExecutionState.BuiltinNames);
    }

    [Fact]
    public void ExecutionState_BuiltinInventoryMatchesRuntimeInitialVariables()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);

        Assert.Equal(
            ExecutionState.BuiltinNames.Order(StringComparer.Ordinal),
            context.Frame.Variables.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ExecutionContext_SourcePathAddsConditionalFileGlobalOnly()
    {
        var context = new LythonRuntime.ExecutionContext(
            new MockLythonHost("/repo"),
            new LythonRunOptions { SourcePath = "scripts/tool.py" });

        Assert.DoesNotContain("__file__", ExecutionState.BuiltinNames);
        Assert.Equal(
            ExecutionState.BuiltinNames.Order(StringComparer.Ordinal),
            context.Frame.Variables.Keys.Where(static name => name != "__file__").Order(StringComparer.Ordinal));

        var file = Assert.IsType<PyString>(context.Frame.Variables["__file__"]);
        Assert.Equal("/repo/scripts/tool.py", file.AsString());
    }

    [Fact]
    public void ExecutionContext_SourceLessScriptDoesNotExposeFileGlobal()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, options: null);

        Assert.DoesNotContain("__file__", context.Frame.Variables.Keys);

        var result = new LythonEngine().Run("return __file__", host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("NameError", result.Failure!.ExceptionType);
    }
}
