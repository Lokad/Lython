using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: per-call **kwargs key payload is owned like other constructed
/// strings; the kwargs dict backing was already governed.
/// </summary>
public sealed class KwargsKeyAccountingTests
{
    private static Dictionary<string, object> BindKwargs(
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span,
        string name,
        object value)
    {
        var plan = new FunctionBindingPlan(
            "f",
            PythonCallableKind.Function,
            [new LoweredFunctionParameter("kw", FunctionParameterKind.VariadicDictionary, null, null)],
            new Dictionary<string, object>());
        return LythonRuntime.BindFunctionArguments([CallArgumentValue.Keyword(name, value)], span, plan, context);
    }

    [Fact]
    public void KwargsKeysCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var bound = BindKwargs(context, span, "alpha", new BigInteger(1));
        var kwargs = (PyDict)bound["kw"];
        var key = kwargs.Keys.OfType<PyString>().Single();
        Assert.Same(context.MemoryGovernor, key.OwnerMemoryGovernor);
        Assert.Equal(325L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
