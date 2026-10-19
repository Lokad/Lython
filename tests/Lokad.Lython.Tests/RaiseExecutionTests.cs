using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class RaiseExecutionTests
{
    [Theory]
    [InlineData("raise value")]
    [InlineData("raise value from cause")]
    [InlineData("raise")]
    [InlineData("try:raise value\nfinally:cleanup()")]
    [InlineData("try:work()\nexcept ValueError:raise")]
    public void OrdinaryRaiseStaysExecutableWithoutMirroring(string source)
    {
        var code = Compile(source);
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
    }

    [Fact]
    public void HelperRaiseStaysExecutableWithoutMirroring()
    {
        var code = Compile("def fail(value):\n raise value\nreturn fail(error)");
        var helper = Assert.IsType<ExecutableCodeObject>(Assert.Single(code.Functions).CodeObject);
        Assert.Empty(helper.StatementFallbacks);
        Assert.Empty(helper.ExpressionFallbacks);
        Assert.False(helper.RequiresLocalVariableMirroring);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
}
