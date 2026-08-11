using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CallBindingSubsystemTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    private static readonly LythonCallableSignature OptionalDemoSignature = LythonCallableSignature.Create(
        "demo",
        ["first", "second", "third"],
        requiredCount: 1);
    private static readonly LythonCallableSignature RequiredDemoSignature = LythonCallableSignature.Create("demo", ["first"]);
    private static readonly LythonCallableSignature NoParameterNamesDemoSignature = LythonCallableSignature.Create("demo");

    [Fact]
    public void BindNamedArguments_CombinesPositionalKeywordAndOptionalTail()
    {
        var result = CallBinder.BindNamedArguments(
            [
                CallArgumentValue.Positional(1),
                CallArgumentValue.Keyword("third", 3)
            ],
            Span,
            OptionalDemoSignature,
            PythonCallableKind.Builtin);

        Assert.Equal([1, (object)PyNone.Instance, 3], result);
    }

    [Fact]
    public void BindNamedArguments_RejectsDuplicateKeywordBinding()
    {
        var ex = Assert.Throws<LythonRuntimeException>(() => CallBinder.BindNamedArguments(
            [
                CallArgumentValue.Positional(1),
                CallArgumentValue.Keyword("first", 2)
            ],
            Span,
            RequiredDemoSignature,
            PythonCallableKind.Builtin));

        Assert.Equal("TypeError", ex.ExceptionType);
        Assert.Contains("multiple values", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BindNamedArguments_RejectsUnexpectedKeyword()
    {
        var ex = Assert.Throws<LythonRuntimeException>(() => CallBinder.BindNamedArguments(
            [CallArgumentValue.Keyword("nope", 1)],
            Span,
            LythonCallableSignature.Create("demo", ["value"]),
            PythonCallableKind.Method));

        Assert.Equal("TypeError", ex.ExceptionType);
        Assert.Contains("unexpected keyword", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BindNamedArguments_RejectsKeywordWhenParameterNamesAreMissing()
    {
        var ex = Assert.Throws<LythonRuntimeException>(() => CallBinder.BindNamedArguments(
            [CallArgumentValue.Keyword("value", 1)],
            Span,
            NoParameterNamesDemoSignature,
            PythonCallableKind.Builtin));

        Assert.Equal("TypeError", ex.ExceptionType);
        Assert.Contains("does not accept keyword arguments", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BindNamedArguments_TracksParametersBeyondInlinePresenceCapacity()
    {
        var parameterNames = Enumerable.Range(0, 65).Select(index => $"arg{index}").ToArray();
        var signature = LythonCallableSignature.Create("wide", parameterNames, requiredCount: 0);

        var result = CallBinder.BindNamedArgumentsWithPresence(
            [CallArgumentValue.Keyword("arg64", 64)],
            Span,
            signature,
            PythonCallableKind.Builtin);

        Assert.Equal(65, result.Values.Length);
        Assert.Equal(64, result.Values[64]);
        Assert.True(result.Assigned[64]);
        Assert.False(result.Assigned[63]);
    }

    [Fact]
    public void CallExpansion_ProducesSameArgumentsForRawAndLoweredCalls()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
        context.Variables["items"] = new PyList([new BigInteger(1), new BigInteger(2)]);
        var mapping = new PyDict();
        mapping.SetItem(PyString.FromString("name"), PyString.FromString("lokad"));
        context.Variables["mapping"] = mapping;

        var raw = CallExpansion.ExpandRawArguments(
            [
                new CallArgumentSyntax(CallArgumentForm.Positional, new StringLiteralExpressionSyntax("head", Span)),
                new CallArgumentSyntax(CallArgumentForm.StarredList, new IdentifierExpressionSyntax("items", Span)),
                new CallArgumentSyntax(CallArgumentForm.StarredDictionary, new IdentifierExpressionSyntax("mapping", Span)),
            ],
            context,
            InvokeEvaluateExpression);

        var lowered = CallExpansion.ExpandLoweredArguments(
            [
                new LoweredCallArgument(CallArgumentForm.Positional, LoweredScript.LowerStandaloneExpression(new StringLiteralExpressionSyntax("head", Span))),
                new LoweredCallArgument(CallArgumentForm.StarredList, LoweredScript.LowerStandaloneExpression(new IdentifierExpressionSyntax("items", Span))),
                new LoweredCallArgument(CallArgumentForm.StarredDictionary, LoweredScript.LowerStandaloneExpression(new IdentifierExpressionSyntax("mapping", Span))),
            ],
            context,
            InvokeEvaluateLoweredExpression);

        Assert.Equal(raw, lowered);
    }

    [Fact]
    public void CallExpansion_RejectsNonStringDictionaryKeysForRawAndLoweredCalls()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
        var mapping = new PyDict();
        mapping.SetItem(new BigInteger(1), PyString.FromString("bad"));
        context.Variables["mapping"] = mapping;

        var rawEx = Assert.Throws<LythonRuntimeException>(() => CallExpansion.ExpandRawArguments(
            [new CallArgumentSyntax(CallArgumentForm.StarredDictionary, new IdentifierExpressionSyntax("mapping", Span))],
            context,
            InvokeEvaluateExpression));

        var loweredEx = Assert.Throws<LythonRuntimeException>(() => CallExpansion.ExpandLoweredArguments(
            [new LoweredCallArgument(CallArgumentForm.StarredDictionary, LoweredScript.LowerStandaloneExpression(new IdentifierExpressionSyntax("mapping", Span)))],
            context,
            InvokeEvaluateLoweredExpression));

        Assert.Equal("TypeError", rawEx.ExceptionType);
        Assert.Equal(rawEx.ExceptionType, loweredEx.ExceptionType);
        Assert.Equal(rawEx.Message, loweredEx.Message);
    }

    [Fact]
    public void KeywordOnlyParameters_RejectMissingKeywordOnlyArgument()
    {
        var result = new LythonEngine().Run(
            """
def f(*args, name):
    return name

f("a")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        var failure = result.Failure.RequireNotNull();
        Assert.Equal("TypeError", failure.ExceptionType);
        Assert.Contains("name", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeywordOnlyParameters_RejectUnexpectedPositionalBinding()
    {
        var result = new LythonEngine().Run(
            """
def f(*, name):
    return name

f("a")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Code == "LA3148");
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("too many positional arguments", StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal("TypeError", result.Failure.ExceptionType);
            Assert.Contains("positional", result.Failure.Message, StringComparison.Ordinal);
        }
    }

    private static object InvokeEvaluateExpression(ExpressionSyntax expression, LythonRuntime.ExecutionContext context)
    {
        var method = typeof(LythonRuntime).GetMethod("EvaluateExpression", BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        return method.Invoke(null, [expression, context]).RequireNotNull();
    }

    private static object InvokeEvaluateLoweredExpression(LoweredExpression expression, LythonRuntime.ExecutionContext context)
    {
        var method = typeof(LythonRuntime).GetMethod("EvaluateLoweredExpression", BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        return method.Invoke(null, [expression, context]).RequireNotNull();
    }
}
