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
        var blocks = new List<object>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var block in Items(code, "Blocks"))
        {
            var instructions = Items(block, "Instructions");
            var operations = new List<object>();
            foreach (var instruction in instructions)
            {
                var op = Get(instruction, "OpCode")!.ToString()!;
                counts[op] = counts.GetValueOrDefault(op) + 1;
                var span = Get(instruction, "Span")!;
                var plan = instruction.GetType().GetProperty("InputPlan", Flags)?.GetValue(instruction);
                var inlineLoads = plan is null ? [] : Items(plan, "Loads").Select(x => Get(x, "OpCode")!.ToString()).ToArray();
                operations.Add(new { op, line = Get(span, "Line"), inlineLoads,
                    destinationSlot = plan is null ? -1 : (int)Get(plan, "DestinationSlot")! });
            }
            blocks.Add(new { block = blocks.Count, operations });
        }
        codes.Add(new { name = Get(code, "Name"), locals = Items(code, "LocalNames"),
            requiresMirroring = Get(code, "RequiresLocalVariableMirroring"),
            exceptionRegions = Items(code, "ExceptionRegions").Length,
            native = Get(code, "NativeReturn") is not null, nativeNodes = (Get(code, "NativeReturn") as Array)?.Length, memberCaches = Get(code, "MemberCacheCount"),
            instructionCount = counts.Values.Sum(), counts, blocks });
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

// Current managed I/O calls: source boundary evidence, not timing.
var io = new List<object>();
foreach (var method in typeof(File).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
    .Where(m => m.Name is "WriteAllBytes" or "WriteToFile" or "GetPreallocationSize" or "OpenHandle"))
{
    var il = method.GetMethodBody()?.GetILAsByteArray();
    var calls = new HashSet<string>();
    if (il is not null)
    {
        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] is not (0x28 or 0x6F or 0x73)) continue;
            try
            {
                var target = method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                if (target is not null) calls.Add(target.DeclaringType?.FullName + "::" + target);
            }
            catch (ArgumentException) { }
        }
    }
    io.Add(new { method = method.ToString(), calls, il = il is null ? null : Convert.ToHexString(il) });
}
File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2]))!, "managed-io-calls.json"),
    JsonSerializer.Serialize(io, new JsonSerializerOptions { WriteIndented = true }));
