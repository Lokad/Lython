using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Compile-only metadata census. No guest execution or runtime instrumentation.
const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
object? Get(object owner, string property) => owner.GetType().GetProperty(property, Flags)!.GetValue(owner);
object[] Items(object owner, string property) => ((IEnumerable)Get(owner, property)!).Cast<object>().ToArray();
var corePath = Path.GetFullPath(args[0]);
var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);
var engineType = core.GetType("Lokad.Lython.LythonEngine", true)!;
var engine = Activator.CreateInstance(engineType)!;
var compile = engineType.GetMethod("Compile", [typeof(string), core.GetType("Lokad.Lython.ILythonMemoryBudget", true)!])!;
using var catalog = JsonDocument.Parse(File.ReadAllBytes(args[1]));
var cases = new List<object>();
foreach (var item in catalog.RootElement.GetProperty("cases").EnumerateArray())
{
    using var script = (IDisposable)compile.Invoke(engine, [item.GetProperty("source").GetString(), null])!;
    if (!(bool)Get(script, "IsValid")!) throw new InvalidDataException("Census requires valid maintained source.");
    var runner = (Delegate)script.GetType().GetField("_runner", Flags)!.GetValue(script)!;
    var executable = runner.Target!.GetType().GetField("executableScript", Flags)!.GetValue(runner.Target)!;
    var codes = new List<object>();
    var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
    void Visit(object code)
    {
        if (!seen.Add(code)) return;
        var operations = new List<object>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var instruction in Items(code, "Instructions"))
        {
            var op = Get(instruction, "OpCode")!.ToString()!;
            counts[op] = counts.GetValueOrDefault(op) + 1;
            var span = Get(instruction, "Span")!;
            operations.Add(new { op, line = Get(span, "Line") });
        }
        codes.Add(new { name = Get(code, "Name"), locals = Items(code, "LocalNames"),
            requiresMirroring = Get(code, "RequiresLocalVariableMirroring"),
            exceptionRegions = Items(code, "ExceptionRegions").Length,
            native = Get(code, "NativeReturn") is not null, nativeNodes = (Get(code, "NativeReturn") as Array)?.Length, memberCaches = Get(code, "MemberCacheCount"),
            instructionCount = counts.Values.Sum(), operandCapacity = Get(code, "OperandCapacity"),
            retainsBlockGraph = code.GetType().GetProperty("Blocks", Flags) is not null,
            regionDepths = Items(code, "ExceptionRegions").Select(region => Get(region, "StackDepth")), counts, operations });
        foreach (var function in Items(code, "Functions")) Visit(Get(function, "CodeObject")!);
    }
    Visit(Get(executable, "EntryPoint")!);
    cases.Add(new { id = item.GetProperty("id").GetString(), sourceSha256 = item.GetProperty("sourceSha256").GetString(), codes });
}
File.WriteAllText(args[2], JsonSerializer.Serialize(new {
    purpose = "Compile-only emitted instruction census; static counts are not CPU shares or speedup evidence",
    coreSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(corePath))),
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    noGuestExecution = true, noProductionEdits = true, cases
}, new JsonSerializerOptions { WriteIndented = true }));

