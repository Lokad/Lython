using System.Text.Json;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

// Temporary allocation evidence; removed before delivery.
public sealed class JsonDefaultOptionsDiagnostic
{
    [Fact]
    public void Capture()
    {
        const int count = 128;
        var span = new LythonSourceSpan(0, 1, 1, 1);
        object yes = true, no = false, custom = new();
        var rows = new List<object>();
        foreach (var (name, strict, assigned) in new[]
        {
            ("default", yes, false), ("explicit-true", yes, true),
            ("false", no, true), ("custom", custom, true)
        })
        {
            var values = Enumerable.Repeat<object>(PyNone.Instance, 8).ToArray();
            values[7] = strict;
            var presence = new ArgumentPresence(8);
            presence[7] = assigned;
            var bound = new BoundCallArguments(values, presence);
            var arguments = assigned ? new[] { CallArgumentValue.Keyword("strict", strict) } : [];
            Measure("module-" + name, () => LythonRuntime.JsonDecoderClass.BindModuleLoadOptions(bound, [], span));
            Measure("constructor-" + name, () => LythonRuntime.JsonDecoderClass.BindLoadOptions(arguments, span));
        }
        var path = Environment.GetEnvironmentVariable("LYTHON_JSON_OPTIONS_OUTPUT")
            ?? throw new InvalidOperationException("Diagnostic output path required.");
        File.WriteAllText(path, JsonSerializer.Serialize(new { iterations = count, rows }, new JsonSerializerOptions { WriteIndented = true }));

        void Measure(string name, Func<LythonRuntime.JsonLoadOptions> create)
        {
            LythonRuntime.JsonLoadOptions? last = null;
            for (var i = 0; i < 512; i++) last = create();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < count; i++) last = create();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(last);
            rows.Add(new { name, allocatedBytes = allocated });
        }
    }
}
