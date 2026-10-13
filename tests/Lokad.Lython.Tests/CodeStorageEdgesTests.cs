using System.Collections;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CodeStorageEdgesTests
{
    [Fact]
    public void PreparedFunctionParameterArrayRemainsReachableThroughTheActualFunction()
    {
        var context = Context();
        var code = Compile("def outer():\n    def f(a=(yield 'need')):\n        return a\n    yield f\n");
        LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        var factory = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.Variables["outer"]);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var generator = Assert.IsType<LythonRuntime.PyGenerator>(factory.Invoke([], span, context));
        Assert.True(generator.TryMoveNext(out var request));
        Assert.Equal("need", Assert.IsType<PyString>(request).AsString());
        Assert.True(generator.TryGetMember("send", out var send));
        var payload = PyString.FromString(new string('x', 16000), context.MemoryGovernor, span);
        var function = Assert.IsAssignableFrom<PyFunctionBase>(Assert.IsAssignableFrom<LythonRuntime.ICallable>(send)
            .Invoke([CallArgumentValue.Positional(payload)], span, context));
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 512);
        Assert.True(walk.TryTraverse([function]));
        Assert.Contains(walk.Visited.ToArray(), value => value is LoweredFunctionParameter[]);
        var capture = Assert.Single(walk.Visited.ToArray().OfType<LoweredCapturedExpression>(),
            value => value.Value is PyString { Utf8Bytes.Length: 16000 });
        Assert.Same(payload, capture.Value);
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(walk.TryTraverse([function]));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData("def f():\n    yield 'a'\n")]
    [InlineData("class C:\n    x = 1\n")]
    [InlineData("def f(x=1):\n    try:\n        return x\n    except ValueError:\n        return None\n")]
    [InlineData("def f():\n    match 1:\n        case 1:\n            return 2\n")]
    [InlineData("for a, *b in [(1, 2)]:\n    pass\n")]
    public void ExecutableCodeTraversesDeclaredBindingsAndFallbackBodies(string source)
    {
        var code = Compile(source);
        using var walk = new MemoryStorageWalk(new MemoryGovernor(160000), 2048);
        Assert.True(walk.TryTraverse([code.EntryPoint]));
    }

    [Fact]
    public void NestedFormattedSpecAndClassHeaderCapturesShareOnePayloadIdentity()
    {
        var function = Function("def f():\n    return f'{1:{2}}'\n");
        var formatted = Assert.IsType<LoweredFormattedStringExpression>(Assert.IsType<LoweredReturnStatement>(function.Body[0]).Expression);
        var part = Assert.IsType<LoweredFormattedStringExpressionPart>(formatted.Parts[0]);
        var payload = new PyList();
        var capture = new LoweredCapturedExpression(part.Expression.Syntax, payload);
        var nested = part with { Expression = capture, FormatSpecifierParts = new LoweredFormattedStringPart[] { new LoweredFormattedStringExpressionPart(capture, null, null, null) } };
        var prepared = formatted with { Parts = new LoweredFormattedStringPart[] { nested } };
        var parameter = new LoweredFunctionParameter("a", FunctionParameterKind.Positional, capture, capture);
        var definition = function with { Decorators = new LoweredExpression[] { capture }, Parameters = new[] { parameter }, ReturnAnnotation = capture,
            Body = new LoweredStatement[] { new LoweredReturnStatement(Assert.IsType<LoweredReturnStatement>(function.Body[0]).Syntax, prepared) } };
        var binding = new ExecutableFunctionBinding(definition, null, new Dictionary<string, LoweredExpression> { ["a"] = capture });
        var originalType = Assert.IsType<LoweredClassDefinitionStatement>(Compile("class C:\n    pass\n").Lowered.Statements[0]);
        var type = originalType with { Decorators = new LoweredExpression[] { capture }, Bases = new LoweredExpression[] { capture },
            HeaderArguments = new[] { new LoweredCallArgument(CallArgumentForm.Positional, capture) }, Body = new LoweredStatement[] { definition } };
        // This is a real graph cycle through a captured mutable container.
        payload.Add(type);
        using var walk = new MemoryStorageWalk(new MemoryGovernor(64000), 512);
        Assert.True(walk.TryTraverse([binding, type]), $"{walk.Incomplete}: {walk.UnsupportedType}");
        Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, payload)));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, parameter));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, nested));
    }

    [Fact]
    public void PreparedLambdaParametersAreStoredWithoutEvaluatingTheBody()
    {
        var lambda = Assert.IsType<LoweredLambdaExpression>(Assert.IsType<LoweredReturnStatement>(Compile("return lambda: 1\n").Lowered.Statements[0]).Expression);
        var payload = PyString.FromString("captured");
        var capture = new LoweredCapturedExpression(lambda.Body.Syntax, payload);
        var parameter = new LoweredFunctionParameter("a", FunctionParameterKind.Positional, null, capture);
        lambda = lambda with { Body = capture, PreparedParameters = new[] { parameter } };
        using var walk = new MemoryStorageWalk(new MemoryGovernor(16000), 64);
        Assert.True(walk.TryTraverse([lambda]), $"{walk.Incomplete}: {walk.UnsupportedType}");
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, parameter));
    }

    [Fact]
    public void CapturedComprehensionOverlayVisitsSourceAndFirstClauseWithoutEnumerating()
    {
        var expression = Assert.IsType<LoweredGeneratorExpression>(Assert.IsType<LoweredReturnStatement>(
            Compile("return (x for x in [1, 2])\n").Lowered.Statements[0]).Expression);
        var context = Context();
        var payload = new PyList();
        var capture = typeof(LythonRuntime).GetMethod("CaptureOutermostIterable", BindingFlags.Static | BindingFlags.NonPublic)!;
        var prepared = Assert.IsType<LoweredGeneratorExpression>(capture.Invoke(null,
            [expression, payload, context, new LythonSourceSpan(0, 0, 0, 0)]));
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 512);
        Assert.True(walk.TryTraverse([prepared]));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, prepared.Clauses));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, expression.Clauses));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(walk.TryTraverse([prepared]));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void EmptyCodeRecordsStillStopAtTheFundedWorkBound()
    {
        var function = Function("def f():\n    pass\n");
        var pass = Assert.IsType<LoweredPassStatement>(function.Body[0]);
        var records = Enumerable.Repeat<LoweredStatement>(pass, 10000).ToArray();
        function = function with { Body = records };
        using var walk = new MemoryStorageWalk(new MemoryGovernor(16000), 32);
        Assert.False(walk.TryTraverse([function]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.WorkspaceLimit, walk.Incomplete);
    }

    [Fact]
    public void UnknownLoweredNodesAndOpaqueCollectionsDoNotInvokeTheirProperties()
    {
        using var walk = new MemoryStorageWalk(new MemoryGovernor(16000), 64);
        Assert.False(walk.TryTraverse([new FutureExpression()]));
        Assert.Equal(typeof(FutureExpression), walk.UnsupportedType);
        var definition = Function("def f():\n    return None\n") with { Body = new HostileStatements() };
        Assert.False(walk.TryTraverse([definition]));
        Assert.Equal(typeof(HostileStatements), walk.UnsupportedType);
        Assert.False(walk.TryTraverse([new ExtraStateList()]));
        Assert.Equal(typeof(ExtraStateList), walk.UnsupportedType);
    }

    [Fact]
    public void DeepLoweredTreesUseTheFundedQueueAndStopAtItsWorkLimit()
    {
        var syntax = Assert.IsType<UnaryExpressionSyntax>(Assert.IsType<ReturnStatementSyntax>(Compile("return -1\n").Lowered.Syntax.Statements[0]).Expression);
        LoweredExpression value = new LoweredCapturedExpression(syntax, PyString.FromString("leaf"));
        for (var i = 0; i < 10000; i++) value = new LoweredUnaryExpression(syntax, value);
        var governor = new MemoryGovernor(16000);
        using (var walk = new MemoryStorageWalk(governor, 64))
        {
            var funding = governor.CurrentReservedBytes;
            Assert.False(walk.TryTraverse([value]));
            Assert.Equal(MemoryStorageWalk.IncompleteReason.WorkspaceLimit, walk.Incomplete);
            Assert.Equal(funding, governor.CurrentReservedBytes);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => walk.TryTraverse([value], cancellation.Token));
            Assert.Empty(walk.Visited.ToArray());
        }
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    private static LoweredFunctionDefinitionStatement Function(string source)
        => Assert.IsType<LoweredFunctionDefinitionStatement>(Compile(source).Lowered.Statements[0]);

    private static ExecutableScript Compile(string source)
    {
        var parsed = LythonFrontend.Compile(source);
        Assert.DoesNotContain(parsed.Diagnostics, diagnostic => diagnostic.Severity == LythonDiagnosticSeverity.Error);
        return ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!));
    }

    private static LythonRuntime.ExecutionContext Context()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        context.Variables.Clear();
        return context;
    }

    private sealed record FutureExpression : LoweredExpression
    {
        public override ExpressionSyntax Syntax => throw new InvalidOperationException("unknown syntax getter");
    }

    private sealed class HostileStatements : IReadOnlyList<LoweredStatement>
    {
        public int Count => throw new InvalidOperationException("opaque count");
        public LoweredStatement this[int index] => throw new InvalidOperationException("opaque index");
        public IEnumerator<LoweredStatement> GetEnumerator() => throw new InvalidOperationException("opaque iterator");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ExtraStateList : List<LoweredStatement>;
}
