using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class FormattedStringExecutableTests
{
    [Theory]
    [InlineData("f\"[{record['name']:*^7.1s}] {record['amount']:>9,d} {record['active']!s} {None}\"", "[***é***]   -12,345 True None")]
    [InlineData("f\"a{f'b{record[\"width\"]:04d}c'}d\"", "ab0009cd")]
    [InlineData("f\"P{record['name']!r}Q{record['name']!a}R\"", "P'é😀'Q'\\xe9\\U0001f600'R")]
    [InlineData("f\"{record['amount']:>{record['width']}}\"", "   -12345")]
    [InlineData("f\"{record['name']}\"", "é😀")]
    [InlineData("f\"| {record['name']:<20.12s} | {record['state']:^8s} | {record['amount']:>8,d} |\\n\"", "| é😀                   |  ready   |  -12,345 |\n")]
    public void NativeRecordFormattingCompletesWithoutInvocationFrame(string expression, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new() { MemoryBudget = owner });
            var frontend = LythonFrontend.Compile("def render(record):return " + expression);
            Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
            LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint, context);
            var function = Assert.IsType<PyExecutableFunction>(context.Variables["render"]);
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var record = new PyDict(context.MemoryGovernor, span);
            record.SetItem(PyString.FromString("name"), PyString.FromString("é😀", context.MemoryGovernor, span));
            record.SetItem(PyString.FromString("amount"), new System.Numerics.BigInteger(-12345));
            record.SetItem(PyString.FromString("active"), true);
            record.SetItem(PyString.FromString("width"), new System.Numerics.BigInteger(9));
            record.SetItem(PyString.FromString("state"), PyString.FromString("ready", context.MemoryGovernor, span));
            var call = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(
                LythonFrontend.Compile("render(record)").Script!)).EntryPoint.CallSites);
            Assert.True(function.CodeObject.CanInlineInstructions);
            for (var pass = 0; pass < 2; pass++)
            {
                Assert.True(function.TryStartNativeCall(call, [record], null, context, span, false,
                    borrow: false, out var value, out var frame));
                Assert.Null(frame);
                var text = Assert.IsType<PyString>(value);
                Assert.Equal(expected, text.AsString());
                if (expression == "f\"{record['name']}\"")
                {
                    Assert.True(record.TryGetValue(PyString.FromString("name"), out var original));
                    Assert.Same(original, value);
                }
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
            }
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormattedOutputDenialPreservesTargetAndReleasesReservations(bool asynchronous)
    {
        var syntax = LythonFrontend.Compile("global target\ntarget=f'{left}{right}'",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        Context Create()
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) },regexProvider:Utf8RegexProvider.Instance);
            context.Variables["left"] = PyString.FromString(new string('a', 4096), context.MemoryGovernor, null);
            context.Variables["right"] = PyString.FromString(new string('b', 4096), context.MemoryGovernor, null);
            context.Variables["target"] = PyNone.Instance;
            return context;
        }
        void Run(Context context)
        {
            if (asynchronous) LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).GetAwaiter().GetResult();
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        }
        var complete = Create();
        Run(complete);
        var denied = Create();
        var padding = 1024 * 1024 - complete.MemoryGovernor.PeakAccountedBytes + 1;
        Assert.True(padding > 0);
        denied.MemoryGovernor.Reserve(padding, null);
        denied.MemoryGovernor.Commit(padding);
        var failure = Assert.Throws<LythonRuntimeException>(() => Run(denied));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Same(PyNone.Instance, denied.Variables["target"]);
        Assert.Null(denied.CurrentExecutableFrame);
        Assert.Equal(0, denied.State.CurrentInterpreterDepth);
        Assert.Equal(0, denied.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData("f'{value}'")]
    [InlineData("f'prefix:{value!r:>{width}}:suffix'")]
    [InlineData("f'{value=}'")]
    [InlineData("f'{f\"{value}\"}'")]
    public void OrdinaryFieldsDoNotForceFallbackOrLocalMirroring(string expression)
    {
        var syntax = LythonFrontend.Compile($"def render(value,width):\n    return {expression}",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax));
        var body = Assert.Single(code.EntryPoint.Functions).CodeObject;
        Assert.NotNull(body);
        Assert.Empty(body.StatementFallbacks);
        Assert.False(body.RequiresLocalVariableMirroring);
        Assert.False(code.EntryPoint.RequiresLocalVariableMirroring);
    }
}
