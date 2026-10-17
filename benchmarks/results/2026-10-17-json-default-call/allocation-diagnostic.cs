using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

// Temporary allocation evidence; remove before delivery.
public sealed class JsonDefaultCallDiagnostic
{
    [Fact]
    public void Capture()
    {
        const int count = 128;
        var rows = new List<object>();
        foreach (var asynchronous in new[] { false, true })
        {
            Measure("positional", [CallArgumentValue.Positional(PyString.FromString("7"))]);
            Measure("keyword", [CallArgumentValue.Keyword("s", PyString.FromString("7"))]);
            Measure("explicit-cls", [CallArgumentValue.Positional(PyString.FromString("7")), CallArgumentValue.Keyword("cls", PyNone.Instance)]);
            Measure("hook", [CallArgumentValue.Positional(PyString.FromString("7")), CallArgumentValue.Keyword("parse_int", new CaptureHook())]);

            void Measure(string name, CallArgumentValue[] arguments)
            {
                var budget = new LythonMemoryBudget(16 * 1024 * 1024);
                using var owner = budget.CreateScope();
                var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn json").Script!));
                var module = LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null)!;
                var span = new LythonSourceSpan(0, 1, 1, 1);
                Assert.True(PyMemberAccess.TryResolve(module, "loads", context, span, out var value));
                var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
                object? last = null;
                for (var i = 0; i < 512; i++) last = Invoke();
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < count; i++) last = Invoke();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                GC.KeepAlive(last);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                rows.Add(new { name, asynchronous, allocatedBytes = allocated });
                last = null;
                context.Variables.Clear();
                object Invoke() => asynchronous ? callable.InvokeAsync(arguments, span, context).GetAwaiter().GetResult() : callable.Invoke(arguments, span, context);
            }
        }
        var path = Environment.GetEnvironmentVariable("LYTHON_JSON_CALL_OUTPUT") ?? throw new InvalidOperationException("Diagnostic output required");
        File.WriteAllText(path, JsonSerializer.Serialize(new { iterations = count, rows }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class CaptureHook : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context) => arguments[0].Value;
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context) => new(arguments[0].Value);
    }
}
