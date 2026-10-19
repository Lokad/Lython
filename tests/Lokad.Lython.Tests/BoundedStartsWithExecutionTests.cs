using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class BoundedStartsWithExecutionTests
{
    [Theory]
    [InlineData("return text.startswith(prefix,start)", 2)]
    [InlineData("return text.startswith(prefix,start,end)", 3)]
    public void PositionalBoundsKeepReceiverSeparate(string source, int arguments)
    {
        var code = Compile(source);
        var call = Assert.Single(code.CallSites);
        Assert.True(call.HasReceiver);
        Assert.Equal(arguments, call.ArgumentCount);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Empty(code.StatementFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
    }

    [Theory]
    [InlineData("return text.startswith(prefix,start=1)")]
    [InlineData("method=text.startswith\nreturn method(prefix,start,end)")]
    public void KeywordAndExposedMethodsRetainGeneralBinding(string source)
        => Assert.False(Assert.Single(Compile(source).CallSites).HasReceiver);

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
}
