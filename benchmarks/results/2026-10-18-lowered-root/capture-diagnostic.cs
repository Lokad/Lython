using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

// Temporary reference-retention evidence;remove before delivery.
public sealed class CompiledRunnerCaptureDiagnostic
{
    [Fact]
    public void Capture()
    {
        using var script = new LythonEngine().Compile("print(7)");
        Assert.True(script.IsValid);
        var runner = (Delegate)typeof(LythonCompiledScript).GetField("_runner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        var fields = runner.Target!.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var loweredFields = fields.Where(f => f.FieldType == typeof(LoweredScript)).ToArray();
        var executable = fields.Select(f => f.GetValue(runner.Target)).OfType<ExecutableScript>().Single();
        var loweredProperty = typeof(ExecutableScript).GetProperty("Lowered");
        var report = new
        {
            compiledRunnerLiveLoweredFields = loweredFields.Count(f => f.GetValue(runner.Target) is not null),
            executableKeepsWholeLoweredModule = loweredProperty?.GetValue(executable) is LoweredScript,
            syntaxFields = fields.Count(f => f.FieldType == typeof(ScriptSyntax) && f.GetValue(runner.Target) is not null),
        };
        var path = Environment.GetEnvironmentVariable("LYTHON_CAPTURE_DIAGNOSTIC") ?? throw new InvalidOperationException("Output path required");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
