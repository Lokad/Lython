using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class ExecutableCompilationLifetimeTests
{
    [Theory]
    [InlineData(false, "print(7)", "7\n")]
    [InlineData(true, "print(7)", "7\n")]
    [InlineData(false, "def helper(values):return [value+1 for value in values]\nprint(helper([1,2]))", "[2, 3]\n")]
    [InlineData(true, "def helper(values):return [value+1 for value in values]\nprint(helper([1,2]))", "[2, 3]\n")]
    [InlineData(false, "class Box:\n value=7\nprint(Box.value)", "7\n")]
    [InlineData(true, "class Box:\n value=7\nprint(Box.value)", "7\n")]
    [InlineData(false, "print(__annotations__)\nvalue:int=1\nprint(__annotations__['value'] is int,value)", "{}\nTrue 1\n")]
    [InlineData(true, "print(__annotations__)\nvalue:int=1\nprint(__annotations__['value'] is int,value)", "{}\nTrue 1\n")]
    public async Task ExecutableProgramDoesNotKeepSyntaxRootAlive(bool asynchronous, string source, string expected)
    {
        var (program, syntaxRoot) = Compile(source);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(syntaxRoot.TryGetTarget(out _));

        var runtime = new LythonRuntime();
        using var result = asynchronous
            ? await runtime.RunAsync(program, new MockLythonHost(), null)
            : runtime.Run(program, new MockLythonHost(), null);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
        GC.KeepAlive(program);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ExecutableScript Program, WeakReference<ScriptSyntax> Syntax) Compile(string source)
    {
        var syntax = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script.RequireNotNull();
        return (ExecutableScript.Compile(syntax), new WeakReference<ScriptSyntax>(syntax));
    }
}
