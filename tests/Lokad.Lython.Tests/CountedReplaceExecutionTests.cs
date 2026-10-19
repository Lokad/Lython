using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class CountedReplaceExecutionTests
{
    [Theory]
    [InlineData("return text.replace('a','b',1)")]
    [InlineData("return text.replace(old,new,count)")]
    public void ThreePositionalArgumentsKeepReceiverSeparate(string source)
    {
        var code = Compile(source);
        var call = Assert.Single(code.CallSites);
        Assert.True(call.HasReceiver);
        Assert.Equal(3, call.ArgumentCount);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Empty(code.StatementFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
    }

    [Theory]
    [InlineData("return text.replace('a','b',count=1)")]
    [InlineData("method=text.replace\nreturn method('a','b',1)")]
    public void KeywordAndExposedMethodsRetainGeneralBinding(string source)
        => Assert.False(Assert.Single(Compile(source).CallSites).HasReceiver);

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
}
