using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeCsvRowConversionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    private static readonly ExecutableCodeObject Code = ExecutableScript.Compile(
        LoweredScript.Lower(LythonFrontend.Compile("return writer.writerow(row)",regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    public static IEnumerable<object[]> Cases()
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var shape in new[] { "list", "tuple", "empty-list", "empty-tuple", "wide-list", "wide-tuple" })
            yield return [asynchronous, shape];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NativeRowsPreserveValuesAndPublication(bool asynchronous, string shape)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, sink) = Prepare(owner, shape);
            Assert.Same(sink.ReturnValue, await Run(context, asynchronous));
            Assert.Single(sink.Writes);
            var expected = shape.StartsWith("empty", StringComparison.Ordinal) ? "\n"
                : string.Join(',', Enumerable.Repeat("a", shape.StartsWith("wide", StringComparison.Ordinal) ? 65 : 2)) + "\n";
            Assert.Equal(expected, sink.Writes[0]);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);

    }

    private static (Context, Sink) Prepare(ILythonMemoryBudget owner, string shape)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var sink = new Sink(context.MemoryGovernor);
        var writer = new LythonRuntime.CsvWriterObject(CsvWriterowCallBoundaryTests.Options(), sink, context.MemoryGovernor, Span);
        var text = PyString.FromString("a", context.MemoryGovernor, Span);
        var count = shape.StartsWith("empty", StringComparison.Ordinal) ? 0
            : shape.StartsWith("wide", StringComparison.Ordinal) ? 65 : 2;
        var cells = Enumerable.Repeat<object>(text, count).ToArray();
        context.Variables["row"] = shape.EndsWith("tuple", StringComparison.Ordinal)
            ? new PyTuple(cells, context.MemoryGovernor, Span) : new PyList(cells, context.MemoryGovernor, Span);
        context.Variables["writer"] = writer;
        return (context, sink);
    }

    private static ValueTask<object?> Run(Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(Code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(Code, context, default, null));

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Sink(MemoryGovernor governor) : LythonRuntime.ICallable
    {
        internal readonly PyString ReturnValue = PyString.FromString("saved", governor, Span);
        internal readonly List<string> Writes = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var text = (PyString)arguments[0].Value;
            Assert.Same(governor, text.OwnerMemoryGovernor);
            Writes.Add(text.AsString());
            return ReturnValue;
        }
    }
}
