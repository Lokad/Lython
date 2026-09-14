using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// MG22: retained lowered bodies own their deep statement count once per run:
// every nested statement list counts (including deferred nested def/class
// bodies), aliases and re-executed sites share the first reservation, and
// distinct bodies pay beside each other.
public sealed class RetainedCodeAccountingTests
{
    private static LythonRuntime.ExecutionContext NewRoot(out MemoryGovernor governor)
    {
        var root = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        governor = root.MemoryGovernor;
        return root;
    }

    private static LythonRuntime.ExecutionContext NewModule(LythonRuntime.ExecutionContext root)
        => LythonRuntime.ExecutionContext.CreateModule(root, null, "helper");

    private static IReadOnlyList<LoweredStatement> LowerBody(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, static d => d.Severity == LythonDiagnosticSeverity.Error);
        return LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements;
    }

    [Fact]
    public void EmptyBodyCostsNothing()
    {
        var root = NewRoot(out var governor);
        LythonRuntime.ChargeRetainedCode(Array.Empty<LoweredStatement>(), NewModule(root), governor, null);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void NullGovernorPaysNothing()
    {
        var root = NewRoot(out _);
        var body = LowerBody("x = 1\n");
        LythonRuntime.ChargeRetainedCode(body, root, null, null);
    }

    [Fact]
    public void FlatBodyChargesPerStatement()
    {
        var root = NewRoot(out var governor);
        var body = LowerBody(string.Concat(Enumerable.Repeat("x = 1\n", 50)));
        Assert.Equal(50, LythonRuntime.CountRetainedStatements(body));
        LythonRuntime.ChargeRetainedCode(body, NewModule(root), governor, null);
        Assert.Equal(50 * 64, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void NestedBodiesCountDeeply()
    {
        var body = LowerBody("if a:\n    x = 1\n    if b:\n        y = 2\ndef f():\n    z = 3\n");
        // if + x + inner if + y + def + z.
        Assert.Equal(6, LythonRuntime.CountRetainedStatements(body));
    }

    [Fact]
    public void TryExceptElseFinallyCountsEveryBranch()
    {
        var body = LowerBody("try:\n    a = 1\nexcept ValueError:\n    b = 2\nelse:\n    c = 3\nfinally:\n    d = 4\n");
        // try + a + b + c + d.
        Assert.Equal(5, LythonRuntime.CountRetainedStatements(body));
    }

    [Fact]
    public void LoopsWithMatchAndElseCountEveryBranch()
    {
        var body = LowerBody(
            "for i in x:\n    a = 1\nelse:\n    b = 2\n"
            + "while c:\n    d = 3\n"
            + "with e:\n    f = 4\n"
            + "match g:\n    case 1:\n        h = 5\n");
        // for + a + b + while + d + with + f + match + h.
        Assert.Equal(9, LythonRuntime.CountRetainedStatements(body));
    }

    [Fact]
    public void SharedBodyPaysOnce()
    {
        var root = NewRoot(out var governor);
        var body = LowerBody(string.Concat(Enumerable.Repeat("x = 1\n", 10)));
        LythonRuntime.ChargeRetainedCode(body, NewModule(root), governor, null);
        Assert.Equal(10 * 64, governor.CurrentCommittedBytes);
        LythonRuntime.ChargeRetainedCode(body, NewModule(root), governor, null);
        Assert.Equal(10 * 64, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DistinctBodiesPayEach()
    {
        var root = NewRoot(out var governor);
        var first = LowerBody(string.Concat(Enumerable.Repeat("x = 1\n", 10)));
        var second = LowerBody(string.Concat(Enumerable.Repeat("x = 1\n", 10)));
        LythonRuntime.ChargeRetainedCode(first, NewModule(root), governor, null);
        LythonRuntime.ChargeRetainedCode(second, NewModule(root), governor, null);
        Assert.Equal(2 * 10 * 64, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void MainScriptBodiesStayHostOwned()
    {
        var root = NewRoot(out var governor);
        var body = LowerBody(string.Concat(Enumerable.Repeat("x = 1\n", 100)));
        Assert.False(LythonRuntime.IsImportRetainedCode(root));
        Assert.True(LythonRuntime.IsImportRetainedCode(NewModule(root)));
        LythonRuntime.ChargeRetainedCode(body, root, governor, null);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }
}
