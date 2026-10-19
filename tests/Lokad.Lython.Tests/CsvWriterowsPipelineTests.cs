using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

// Freeze on the qualified preceding source before changing the bulk pipeline.
public sealed class CsvWriterowsPipelineTests
{
    // Frozen on 6bec0f34 before consolidating the bulk row pipeline.
    private const string FrozenGuards = """{"True:True:callback-fail":{"asynchronous":true,"exposed":true,"shape":"callback-fail","outcome":"ValueError:callback failed","events":["pull:0","write:a\n","pull:1","write:a\n"]},"True:False:empty":{"asynchronous":true,"exposed":false,"shape":"empty","outcome":"None","events":[]},"False:False:wide":{"asynchronous":false,"exposed":false,"shape":"wide","outcome":"None","events":["pull:0","write:a\n","pull:1","write:a\n","pull:2","write:a\n","pull:3","write:a\n","pull:4","write:a\n","pull:5","write:a\n","pull:6","write:a\n","pull:7","write:a\n","pull:8","write:a\n","pull:9","write:a\n","pull:10","write:a\n","pull:11","write:a\n","pull:12","write:a\n","pull:13","write:a\n","pull:14","write:a\n","pull:15","write:a\n","pull:16","write:a\n","pull:17","write:a\n","pull:18","write:a\n","pull:19","write:a\n","pull:20","write:a\n","pull:21","write:a\n","pull:22","write:a\n","pull:23","write:a\n","pull:24","write:a\n","pull:25","write:a\n","pull:26","write:a\n","pull:27","write:a\n","pull:28","write:a\n","pull:29","write:a\n","pull:30","write:a\n","pull:31","write:a\n","pull:32","write:a\n","pull:33","write:a\n","pull:34","write:a\n","pull:35","write:a\n","pull:36","write:a\n","pull:37","write:a\n","pull:38","write:a\n","pull:39","write:a\n","pull:40","write:a\n","pull:41","write:a\n","pull:42","write:a\n","pull:43","write:a\n","pull:44","write:a\n","pull:45","write:a\n","pull:46","write:a\n","pull:47","write:a\n","pull:48","write:a\n","pull:49","write:a\n","pull:50","write:a\n","pull:51","write:a\n","pull:52","write:a\n","pull:53","write:a\n","pull:54","write:a\n","pull:55","write:a\n","pull:56","write:a\n","pull:57","write:a\n","pull:58","write:a\n","pull:59","write:a\n","pull:60","write:a\n","pull:61","write:a\n","pull:62","write:a\n","pull:63","write:a\n","pull:64","write:a\n"]},"False:True:pair":{"asynchronous":false,"exposed":true,"shape":"pair","outcome":"None","events":["pull:0","write:a\n","pull:1","write:a\n"]},"True:True:pair":{"asynchronous":true,"exposed":true,"shape":"pair","outcome":"None","events":["pull:0","write:a\n","pull:1","write:a\n"]},"False:False:pair":{"asynchronous":false,"exposed":false,"shape":"pair","outcome":"None","events":["pull:0","write:a\n","pull:1","write:a\n"]},"False:True:callback-fail":{"asynchronous":false,"exposed":true,"shape":"callback-fail","outcome":"ValueError:callback failed","events":["pull:0","write:a\n","pull:1","write:a\n"]},"True:False:callback-fail":{"asynchronous":true,"exposed":false,"shape":"callback-fail","outcome":"ValueError:callback failed","events":["pull:0","write:a\n","pull:1","write:a\n"]},"True:True:empty":{"asynchronous":true,"exposed":true,"shape":"empty","outcome":"None","events":[]},"False:False:empty":{"asynchronous":false,"exposed":false,"shape":"empty","outcome":"None","events":[]},"True:True:bad-cell":{"asynchronous":true,"exposed":true,"shape":"bad-cell","outcome":"TypeError:CSV rows must contain scalar values.","events":["pull:0","write:a\n","pull:1"]},"False:True:empty":{"asynchronous":false,"exposed":true,"shape":"empty","outcome":"None","events":[]},"True:False:wide":{"asynchronous":true,"exposed":false,"shape":"wide","outcome":"None","events":["pull:0","write:a\n","pull:1","write:a\n","pull:2","write:a\n","pull:3","write:a\n","pull:4","write:a\n","pull:5","write:a\n","pull:6","write:a\n","pull:7","write:a\n","pull:8","write:a\n","pull:9","write:a\n","pull:10","write:a\n","pull:11","write:a\n","pull:12","write:a\n","pull:13","write:a\n","pull:14","write:a\n","pull:15","write:a\n","pull:16","write:a\n","pull:17","write:a\n","pull:18","write:a\n","pull:19","write:a\n","pull:20","write:a\n","pull:21","write:a\n","pull:22","write:a\n","pull:23","write:a\n","pull:24","write:a\n","pull:25","write:a\n","pull:26","write:a\n","pull:27","write:a\n","pull:28","write:a\n","pull:29","write:a\n","pull:30","write:a\n","pull:31","write:a\n","pull:32","write:a\n","pull:33","write:a\n","pull:34","write:a\n","pull:35","write:a\n","pull:36","write:a\n","pull:37","write:a\n","pull:38","write:a\n","pull:39","write:a\n","pull:40","write:a\n","pull:41","write:a\n","pull:42","write:a\n","pull:43","write:a\n","pull:44","write:a\n","pull:45","write:a\n","pull:46","write:a\n","pull:47","write:a\n","pull:48","write:a\n","pull:49","write:a\n","pull:50","write:a\n","pull:51","write:a\n","pull:52","write:a\n","pull:53","write:a\n","pull:54","write:a\n","pull:55","write:a\n","pull:56","write:a\n","pull:57","write:a\n","pull:58","write:a\n","pull:59","write:a\n","pull:60","write:a\n","pull:61","write:a\n","pull:62","write:a\n","pull:63","write:a\n","pull:64","write:a\n"]},"True:False:pair":{"asynchronous":true,"exposed":false,"shape":"pair","outcome":"None","events":["pull:0","write:a\n","pull:1","write:a\n"]},"False:True:bad-cell":{"asynchronous":false,"exposed":true,"shape":"bad-cell","outcome":"TypeError:CSV rows must contain scalar values.","events":["pull:0","write:a\n","pull:1"]},"True:False:bad-cell":{"asynchronous":true,"exposed":false,"shape":"bad-cell","outcome":"TypeError:CSV rows must contain scalar values.","events":["pull:0","write:a\n","pull:1"]},"False:False:callback-fail":{"asynchronous":false,"exposed":false,"shape":"callback-fail","outcome":"ValueError:callback failed","events":["pull:0","write:a\n","pull:1","write:a\n"]},"True:True:wide":{"asynchronous":true,"exposed":true,"shape":"wide","outcome":"None","events":["pull:0","write:a\n","pull:1","write:a\n","pull:2","write:a\n","pull:3","write:a\n","pull:4","write:a\n","pull:5","write:a\n","pull:6","write:a\n","pull:7","write:a\n","pull:8","write:a\n","pull:9","write:a\n","pull:10","write:a\n","pull:11","write:a\n","pull:12","write:a\n","pull:13","write:a\n","pull:14","write:a\n","pull:15","write:a\n","pull:16","write:a\n","pull:17","write:a\n","pull:18","write:a\n","pull:19","write:a\n","pull:20","write:a\n","pull:21","write:a\n","pull:22","write:a\n","pull:23","write:a\n","pull:24","write:a\n","pull:25","write:a\n","pull:26","write:a\n","pull:27","write:a\n","pull:28","write:a\n","pull:29","write:a\n","pull:30","write:a\n","pull:31","write:a\n","pull:32","write:a\n","pull:33","write:a\n","pull:34","write:a\n","pull:35","write:a\n","pull:36","write:a\n","pull:37","write:a\n","pull:38","write:a\n","pull:39","write:a\n","pull:40","write:a\n","pull:41","write:a\n","pull:42","write:a\n","pull:43","write:a\n","pull:44","write:a\n","pull:45","write:a\n","pull:46","write:a\n","pull:47","write:a\n","pull:48","write:a\n","pull:49","write:a\n","pull:50","write:a\n","pull:51","write:a\n","pull:52","write:a\n","pull:53","write:a\n","pull:54","write:a\n","pull:55","write:a\n","pull:56","write:a\n","pull:57","write:a\n","pull:58","write:a\n","pull:59","write:a\n","pull:60","write:a\n","pull:61","write:a\n","pull:62","write:a\n","pull:63","write:a\n","pull:64","write:a\n"]},"False:False:bad-cell":{"asynchronous":false,"exposed":false,"shape":"bad-cell","outcome":"TypeError:CSV rows must contain scalar values.","events":["pull:0","write:a\n","pull:1"]},"False:True:wide":{"asynchronous":false,"exposed":true,"shape":"wide","outcome":"None","events":["pull:0","write:a\n","pull:1","write:a\n","pull:2","write:a\n","pull:3","write:a\n","pull:4","write:a\n","pull:5","write:a\n","pull:6","write:a\n","pull:7","write:a\n","pull:8","write:a\n","pull:9","write:a\n","pull:10","write:a\n","pull:11","write:a\n","pull:12","write:a\n","pull:13","write:a\n","pull:14","write:a\n","pull:15","write:a\n","pull:16","write:a\n","pull:17","write:a\n","pull:18","write:a\n","pull:19","write:a\n","pull:20","write:a\n","pull:21","write:a\n","pull:22","write:a\n","pull:23","write:a\n","pull:24","write:a\n","pull:25","write:a\n","pull:26","write:a\n","pull:27","write:a\n","pull:28","write:a\n","pull:29","write:a\n","pull:30","write:a\n","pull:31","write:a\n","pull:32","write:a\n","pull:33","write:a\n","pull:34","write:a\n","pull:35","write:a\n","pull:36","write:a\n","pull:37","write:a\n","pull:38","write:a\n","pull:39","write:a\n","pull:40","write:a\n","pull:41","write:a\n","pull:42","write:a\n","pull:43","write:a\n","pull:44","write:a\n","pull:45","write:a\n","pull:46","write:a\n","pull:47","write:a\n","pull:48","write:a\n","pull:49","write:a\n","pull:50","write:a\n","pull:51","write:a\n","pull:52","write:a\n","pull:53","write:a\n","pull:54","write:a\n","pull:55","write:a\n","pull:56","write:a\n","pull:57","write:a\n","pull:58","write:a\n","pull:59","write:a\n","pull:60","write:a\n","pull:61","write:a\n","pull:62","write:a\n","pull:63","write:a\n","pull:64","write:a\n"]}}""";
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    public static IEnumerable<object[]> Cases()
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var exposed in new[] { false, true })
        foreach (var shape in new[] { "pair", "empty", "wide", "bad-cell", "callback-fail" })
            yield return [asynchronous, exposed, shape];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task BulkCallsPreserveEveryCheckpointAndOrderedPrefix(bool asynchronous, bool exposed, string shape)
    {
        var source = exposed ? "method=writer.writerows\nreturn method(rows)" : "return writer.writerows(rows)";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string outcome;
        string[] events;
        using (var owner = budget.CreateScope())
        {
            var (context, trace) = Prepare(owner, shape);
            try
            {
                Assert.Same(PyNone.Instance, await Run(code, context, asynchronous));
                Assert.DoesNotContain(shape, new[] { "bad-cell", "callback-fail" });
                outcome = "None";
            }
            catch (LythonRuntimeException error)
            {
                Assert.Contains(shape, new[] { "bad-cell", "callback-fail" });
                Assert.Equal(shape == "bad-cell" ? "TypeError" : "ValueError", error.ExceptionType);
                outcome = error.ExceptionType + ":" + error.Message;
            }
            events = trace.Events.ToArray();
            var pulls = events.Count(x => x.StartsWith("pull:", StringComparison.Ordinal));
            Assert.Equal(shape == "empty" ? 0 : shape == "wide" ? 65 : 2, pulls);
            Assert.Equal(shape == "empty" ? 0 : shape == "bad-cell" ? 1 : shape == "wide" ? 65 : 2, trace.Writes);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using var frozen = JsonDocument.Parse(FrozenGuards);
        var guard = frozen.RootElement.GetProperty($"{asynchronous}:{exposed}:{shape}");
        Assert.Equal(guard.GetProperty("outcome").GetString(), outcome);
        Assert.Equal(guard.GetProperty("events").EnumerateArray().Select(x => x.GetString()), events);
    }

    private static (Context, Trace) Prepare(ILythonMemoryBudget owner, string shape)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
        var trace = new Trace(context.MemoryGovernor, shape == "callback-fail");
        context.Variables["writer"] = new LythonRuntime.CsvWriterObject(CsvWriterowCallBoundaryTests.Options(), trace, context.MemoryGovernor, Span);
        var text = PyString.FromString("a", context.MemoryGovernor, Span);
        var row = new PyList([text], context.MemoryGovernor, Span);
        var count = shape == "empty" ? 0 : shape == "wide" ? 65 : 3;
        var rows = Enumerable.Repeat<object>(row, count).ToArray();
        if (shape == "pair") Array.Resize(ref rows, 2);
        if (shape == "bad-cell") rows[1] = new PyTuple([new object()], context.MemoryGovernor, Span);
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
