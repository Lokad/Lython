using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class LoweredFunctionInvocationBoundaryTests
{
    private static readonly LythonSourceSpan CallSpan = new(100, 6, 20, 3);

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task IdentityBodyKeepsArgumentsAndCleanup(int width, bool asynchronous)
    {
        {
            var (function, context, expression) = Function(width, new LythonRunOptions
            {
            SourcePath = "/lowered-function.py"
            });
            var receiver = new object();
                var value = asynchronous
                    ? await function.InvokeAsync(Arguments(width, receiver), CallSpan, context)
                    : function.Invoke(Arguments(width, receiver), CallSpan, context);
                Assert.Same(receiver, value);
            AssertUnwound(context);
        }
    }

    [Theory]
    [InlineData(0,false)]
    [InlineData(0,true)]
    [InlineData(1,false)]
    [InlineData(1,true)]
    [InlineData(2,false)]
    [InlineData(2,true)]
    public async Task InvocationChecksCancellation(int width,bool asynchronous)
    {
        using var cancellation = new CancellationTokenSource();
        var (function, context, _) = Function(width, new LythonRunOptions
        {
            CancellationToken = cancellation.Token
        });
        cancellation.Cancel();
        var error = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await function.InvokeAsync(Arguments(width, new object()), CallSpan, context))
            : Assert.Throws<LythonRuntimeException>(() => function.Invoke(Arguments(width, new object()), CallSpan, context));
        Assert.Equal("execution canceled", error.Message);
        Assert.Same(CallSpan, error.Span);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task AdmissionDenialKeepsFundingExceptionAndSpan(int width, bool asynchronous)
    {
        var (function, context, _) = Function(width, new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 8192, MaxRecursionDepth = 0
        });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var before = context.MemoryGovernor.CurrentAccountedBytes;
        var error = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await function.InvokeAsync(Arguments(width, new object()), CallSpan, context))
            : Assert.Throws<LythonRuntimeException>(() => function.Invoke(Arguments(width, new object()), CallSpan, context));
        Assert.Equal("RecursionError", error.ExceptionType);
        Assert.Same(CallSpan, error.Span);
        Assert.Equal(before, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Same(active, context.Services.CurrentException);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task BodyUsesActualFrameServicesAndMirroredArguments(int width)
    {
        var snapshots = new List<(long Accounted, long Reserved)>();
        foreach (var asynchronous in new[] { false, true })
        {
            var names = Enumerable.Range(0, width).Select(i => $"arg{i}").ToArray();
            var parameters = string.Join(",", new[] { "self" }.Concat(names));
            var definition = Definition($"def f({parameters}):\n return tap({parameters})\n");
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            {
            MaxExecutionMemoryBytes = 8192, SourcePath = "/actual-frame.py"
            });
            var receiver = new object();
            var active = new PyException("ValueError", "outer", PyNone.Instance);
            context.Services.SetCurrentException(active);
            var tap = new RecordingTap(context, active, receiver, width);
            context.Variables["tap"] = tap;
            var function = new PyFunction("f", definition.Parameters, definition.Body, context, [], ScopeDirectiveFactsCollector.ForFunction(definition.Syntax));
            var result = asynchronous
                ? await function.InvokeAsync(Arguments(width, receiver), CallSpan, context)
                : function.Invoke(Arguments(width, receiver), CallSpan, context);
            Assert.Same(receiver, result);
            Assert.Equal(1, tap.Calls);
            Assert.Same(active, context.Services.CurrentException);
            AssertUnwound(context);
            snapshots.Add(tap.Snapshot);
        }
        Assert.Equal(snapshots[0], snapshots[1]);
    }

    private sealed class RecordingTap(LythonRuntime.ExecutionContext closure, PyException active, object receiver, int width) : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public (long Accounted, long Reserved) Snapshot { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Assert.NotSame(closure, context);
            Assert.Same(closure, context.ParentContext);
            Assert.Same(closure.Services, context.Services);
            Assert.Equal(closure.SourcePath, context.SourcePath);
            Assert.Same(active, context.Services.CurrentException);
            Assert.Equal(1, context.Limits.CurrentRecursionDepth);
            Assert.Same(receiver, context.Variables["self"]);
            Assert.Equal(width + 1, context.Variables.Count);
            Assert.Equal(width + 1, arguments.Length);
            Assert.Same(receiver, arguments[0].Value);
            for (var i = 0; i < width; i++) Assert.Equal(i + 7, context.Variables[$"arg{i}"]);
            Snapshot = (context.MemoryGovernor.CurrentAccountedBytes, context.MemoryGovernor.CurrentReservedBytes);
            return receiver;
        }
    }

    private static (PyFunction Function, LythonRuntime.ExecutionContext Context, LoweredExpression Expression) Function(int width, LythonRunOptions options)
    {
        var parameters = string.Join(",", new[] { "self" }.Concat(Enumerable.Range(0, width).Select(i => $"arg{i}")));
        var definition = Definition($"def f({parameters}):\n return self\n");
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options);
        var function = new PyFunction("f", definition.Parameters, definition.Body, context, [], ScopeDirectiveFactsCollector.ForFunction(definition.Syntax));
        return (function, context, Assert.IsType<LoweredReturnStatement>(Assert.Single(definition.Body)).Expression!);
    }

    private static LoweredFunctionDefinitionStatement Definition(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return Assert.IsType<LoweredFunctionDefinitionStatement>(Assert.Single(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements));
    }

    private static CallArgumentValue[] Arguments(int width, object receiver) =>
        new[] { CallArgumentValue.Positional(receiver) }.Concat(Enumerable.Range(0, width).Select(i => CallArgumentValue.Positional(i + 7))).ToArray();

    private static void AssertUnwound(LythonRuntime.ExecutionContext context)
    {
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
