using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class AssertionExecutionTests
{
    [Theory]
    [InlineData("assert True\nreturn 7")]
    [InlineData("assert value,message()\nreturn 7")]
    [InlineData("try:assert value,'required'\nfinally:cleanup()\nreturn 7")]
    public void OrdinaryAssertionsStayExecutableWithoutMirroring(string source)
    {
        var code = Compile(source);
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
    }

    [Fact]
    public void OrdinaryHelperAssertionsStayExecutableWithoutMirroring()
    {
        var code = Compile("def validate(value):\n assert value,'required'\n return value\nreturn validate(7)");
        var helper = Assert.IsType<ExecutableCodeObject>(Assert.Single(code.Functions).CodeObject);
        Assert.Empty(helper.StatementFallbacks);
        Assert.Empty(helper.ExpressionFallbacks);
        Assert.False(helper.RequiresLocalVariableMirroring);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
}
