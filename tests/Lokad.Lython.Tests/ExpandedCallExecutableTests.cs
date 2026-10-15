using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class ExpandedCallExecutableTests
{
    [Theory]
    [InlineData("target(*values)")]
    [InlineData("target(**keywords)")]
    [InlineData("target(1,*values,flag=2,**keywords)")]
    [InlineData("target(*values,flag=marker)")]
    public void OrdinaryExpansionDoesNotForceFallbackOrLocalMirroring(string expression)
    {
        var syntax = LythonFrontend.Compile($"def invoke(target,values,keywords,marker):\n    return {expression}").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax));
        var body = Assert.Single(code.EntryPoint.Functions).CodeObject;
        Assert.NotNull(body);
        Assert.Empty(body.ExpressionFallbacks);
        Assert.Empty(body.StatementFallbacks);
        Assert.False(body.RequiresLocalVariableMirroring);
        Assert.False(code.EntryPoint.RequiresLocalVariableMirroring);
    }
}
