using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

// Temporary native allocation evidence; removed before delivery.
public sealed class JsonNumberScratchDiagnostic
{
    [Fact]
    public void Capture()
    {
        const int count = 128;
        var rows = new List<object>();
        Measure("small-int", "7");
        Measure("float", "-0.0");
        Measure("slow-float", "0." + new string('1', 512) + "e-100");
        Measure("overflow-float", "1e9999");
        Measure("large-int", new string('9', 128));
        Measure("callback", "-0", callback: true);
        var path = Environment.GetEnvironmentVariable("LYTHON_JSON_NUMBER_OUTPUT")
            ?? throw new InvalidOperationException("Diagnostic output path required.");
        File.WriteAllText(path, JsonSerializer.Serialize(new { iterations = count, rows }, new JsonSerializerOptions { WriteIndented = true }));

        void Measure(string name, string source, bool callback = false)
        {
            var budget = new LythonMemoryBudget(16 * 1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn json").Script!));
            var module = LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null)!;
            var method = module.GetType().GetMethod("ParseJsonText", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var parse = method.CreateDelegate<Func<PyString, LythonRuntime.JsonLoadOptions, Context, LythonSourceSpan, object>>(module);
            var input = PyString.FromString(source, context.MemoryGovernor, null);
            var capture = new NumericCapture();
            var options = callback ? new LythonRuntime.JsonLoadOptions(null, null, capture, null, null, true) : LythonRuntime.JsonLoadOptions.Default;
            var span = new LythonSourceSpan(0, 1, 1, 1);
            object? last = null;
            for (var i = 0; i < 512; i++) last = parse(input, options, context, span);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < count; i++) last = parse(input, options, context, span);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(last);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            rows.Add(new { name, allocatedBytes = allocated });
            last = null;
            context.Variables.Clear();
        }
    }

    private sealed class NumericCapture : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, Context context) => args[0].Value;
        public ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, Context context) => ValueTask.FromResult(args[0].Value);
    }
}
