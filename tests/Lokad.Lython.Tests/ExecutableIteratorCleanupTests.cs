using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ExecutableIteratorCleanupTests
{
    [Theory]
    [InlineData(false, "result=[1//0 for value in source]", true)]
    [InlineData(true, "result=[1//0 for value in source]", true)]
    [InlineData(false, "for value in source:\n    1//0", true)]
    [InlineData(true, "for value in source:\n    1//0", true)]
    [InlineData(false, "for value in source:\n    break", false)]
    [InlineData(true, "for value in source:\n    break", false)]
    public void AbandonmentDisposesNativeIterationBeforeReturning(bool asynchronous, string source, bool fails)
    {
        var syntax = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var context = new Context(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var iterable = new OwnedIterable(context.MemoryGovernor);
        context.Variables["source"] = iterable;
        void Run()
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        }
        if (fails) Assert.Equal("ZeroDivisionError", Assert.Throws<LythonRuntimeException>(Run).ExceptionType);
        else Run();
        Assert.Equal(1, iterable.Disposals);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.False(context.TryGetInlinedBinding("value", out _));
        Assert.Null(context.CurrentExecutableFrame);
    }

    private sealed class OwnedIterable(MemoryGovernor governor) : IPyIterableValue
    {
        public int Disposals { get; private set; }
        public IEnumerable<object> Iterate()
        {
            using var storage = governor.ReserveTemporary(4096, null);
            try
            {
                yield return BigInteger.One;
                yield return new BigInteger(2);
            }
            finally { Disposals++; }
        }
    }
}
