using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class CsvWriterowsPipelineTests
{
        private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    public static IEnumerable<object[]> Cases()
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var exposed in new[] { false, true })
        foreach (var shape in new[] { "pair", "empty", "wide", "callback-fail" })
            yield return [asynchronous, exposed, shape];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task BulkCallsPreservePublicationAndOrderedPrefix(bool asynchronous, bool exposed, string shape)
    {
        var source = exposed ? "method=writer.writerows\nreturn method(rows)" : "return writer.writerows(rows)";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string outcome;
        string[] events;
        using (var owner = budget.CreateScope())
        {
            var (context, trace) = Prepare(owner, shape);
            try
            {
                Assert.Same(PyNone.Instance, await Run(code, context, asynchronous));
                Assert.NotEqual("callback-fail", shape);
                outcome = "None";
            }
            catch (LythonRuntimeException error)
            {
                Assert.Equal("callback-fail", shape);
                Assert.Equal("ValueError", error.ExceptionType);
                outcome = error.ExceptionType + ":" + error.Message;
            }
            events = trace.Events.ToArray();
            var pulls = events.Count(x => x.StartsWith("pull:", StringComparison.Ordinal));
            Assert.Equal(shape == "empty" ? 0 : shape == "wide" ? 65 : 2, pulls);
            Assert.Equal(shape == "empty" ? 0 : shape == "wide" ? 65 : 2, trace.Writes);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(shape == "callback-fail" ? "ValueError:callback failed" : "None", outcome);
        var count = shape == "empty" ? 0 : shape == "wide" ? 65 : 2;
        Assert.Equal(Enumerable.Range(0,count).SelectMany(i=>new[]{"pull:"+i,"write:a\n"}),events);
    }

    private static (Context, Trace) Prepare(ILythonMemoryBudget owner, string shape)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var trace = new Trace(context.MemoryGovernor, shape == "callback-fail");
        context.Variables["writer"] = new LythonRuntime.CsvWriterObject(CsvWriterowCallBoundaryTests.Options(), trace, context.MemoryGovernor, Span);
        var text = PyString.FromString("a", context.MemoryGovernor, Span);
        var row = new PyList([text], context.MemoryGovernor, Span);
        var count = shape == "empty" ? 0 : shape == "wide" ? 65 : 3;
        var rows = Enumerable.Repeat<object>(row, count).ToArray();
        if (shape == "pair") Array.Resize(ref rows, 2);
        context.Variables["rows"] = new Rows(rows, trace.Events);
        return (context, trace);
    }

    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Rows(object[] values, List<string> events) : IEnumerable<object>
    {
        public IEnumerator<object> GetEnumerator()
        {
            for (var i = 0; i < values.Length; i++)
            {
                events.Add("pull:" + i);
                yield return values[i];
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Trace(MemoryGovernor governor, bool fail) : LythonRuntime.ICallable
    {
        internal readonly List<string> Events = [];
        internal int Writes;
        private readonly PyString _ignored = PyString.FromString("ignored", governor, Span);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var text = (PyString)arguments[0].Value;
            Assert.Same(governor, text.OwnerMemoryGovernor);
            Events.Add("write:" + text.AsString());
            Writes++;
            if (fail && Writes == 2) throw new LythonRuntimeException("ValueError", "callback failed", span);
            return _ignored;
        }
    }
}
