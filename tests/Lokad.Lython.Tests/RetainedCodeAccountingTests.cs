using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// MG22: deferred module code owns its retained syntax nodes once per module
// load: definitions count their full subtree (nested definitions included),
// while executed top-level statements contribute only lambdas. Counts reuse
// the shared statement/expression traversals; unknown shapes fail loud.
public sealed class RetainedCodeAccountingTests
{
    private static IReadOnlyList<StatementSyntax> TopLevel(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, static d => d.Severity == LythonDiagnosticSeverity.Error);
        return frontend.Script.RequireNotNull().Statements;
    }

    private static LythonRuntime.ExecutionContext NewRoot(out MemoryGovernor governor)
    {
        var root = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        governor = root.MemoryGovernor;
        return root;
    }

    [Fact]
    public void EmptyModuleCostsNothing()
    {
        var root = NewRoot(out var governor);
        LythonRuntime.ChargeDeferredModuleCode(Array.Empty<StatementSyntax>(), governor, null);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void NullGovernorPaysNothing()
    {
        var body = TopLevel("def f():\n    x = 1\n");
        LythonRuntime.ChargeDeferredModuleCode(body, null, null);
    }

    [Fact]
    public void FlatAssignmentHoldsStatementPlusLiteral()
    {
        // The bound name is a plain string, not a node: one assignment owns
        // its statement and its literal.
        var body = TopLevel("def f():\n    x = 1\n");
        Assert.Equal(3, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void TopLevelStatementsAreExcluded()
    {
        var body = TopLevel(string.Concat(Enumerable.Repeat("x = 1\n", 50)));
        Assert.Equal(0, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void NestedBodiesCountDeeply()
    {
        var body = TopLevel("if a:\n    x = 1\n    if b:\n        y = 2\ndef f():\n    z = 3\n");
        // Full subtree: if + a + x + 1 + inner if + b + y + 2.
        Assert.Equal(8, LythonRuntime.CountSyntaxSubtree(body[0]));
        // Deferred pass keeps only the def subtree: def + z + 3.
        Assert.Equal(3, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void ClassBodyExcludedButNestedCounted()
    {
        var body = TopLevel("class K:\n    a = 1\n    def m(self):\n        z = 3\n");
        // The executed class body pays nothing; the method owns def + z + 3.
        Assert.Equal(3, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void ConditionalDefsCountConservatively()
    {
        var body = TopLevel("if c:\n    def f():\n        x = 1\n");
        // Counted whether or not the branch runs: def + x + 1.
        Assert.Equal(3, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void LambdaInAssignmentCounted()
    {
        var body = TopLevel("f = lambda: 1\n");
        Assert.Equal(2, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void LambdaDefaultsCounted()
    {
        var body = TopLevel("f = lambda x=1: x\n");
        // Lambda + default literal + body identifier.
        Assert.Equal(3, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void WideExpressionCountsEveryItem()
    {
        var body = TopLevel("def f():\n    return [" + string.Join((char)44, Enumerable.Repeat("1", 100)) + "]\n");
        // def + return + list display + one literal per item.
        Assert.Equal(103, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void TryExceptElseFinallyCountsEveryBranch()
    {
        var body = TopLevel("def f():\n    try:\n        a = 1\n    except ValueError:\n        b = 2\n    else:\n        c = 3\n    finally:\n        d = 4\n");
        // def + try + four assignment pairs.
        Assert.Equal(10, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void LoopsWithMatchAndElseCountEveryBranch()
    {
        var body = TopLevel(
            "def f():\n"
            + "    for i in x:\n        a = 1\n    else:\n        b = 2\n"
            + "    while c:\n        d = 3\n"
            + "    with e:\n        f = 4\n"
            + "    match g:\n        case 1:\n            h = 5\n");
        // def + for + x + a-pair + b-pair + while + c + d-pair + with + e +
        // f-pair + match + g + pattern + case literal + h-pair.
        Assert.Equal(21, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void MatchPatternsCounted()
    {
        var body = TopLevel("def f():\n    match g:\n        case [a, 2]:\n            h = 5\n");
        // def + match + g + sequence pattern + capture + value pattern +
        // case literal + h-pair.
        Assert.Equal(9, LythonRuntime.CountDeferredModuleCode(body));
    }

    [Fact]
    public void UnpackingReceiversCounted()
    {
        var body = TopLevel("def f():\n    a[0], b = it\n");
        // def + unpacking statement + receiver + index + source value
        // (plain names are strings, not nodes).
        Assert.Equal(5, LythonRuntime.CountDeferredModuleCode(body));
    }


    [Fact]
    public void FundedBodiesCommitOnce()
    {
        var root = NewRoot(out var governor);
        var body = TopLevel("def f():\n    x = 1\n");
        LythonRuntime.ChargeDeferredModuleCode(body, governor, null);
        Assert.Equal(3 * 64, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedChargeFailsBeforeRetention()
    {
        var governor = new MemoryGovernor(0);
        var body = TopLevel("def f():\n    x = 1\n");
        var failure = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ChargeDeferredModuleCode(body, governor, null));
        Assert.Equal("MemoryError", failure.ExceptionType);
    }
}
