using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

var core = Assembly.LoadFrom(Path.GetFullPath(args[0]));
using var catalog = JsonDocument.Parse(File.ReadAllBytes(args[1]));
var frontend = core.GetType("Lokad.Lython.Frontend.LythonFrontend", true)!;
var lower = core.GetType("Lokad.Lython.Frontend.LoweredScript", true)!.GetMethod("Lower")!;
var compile = core.GetType("Lokad.Lython.Frontend.ExecutableScript", true)!.GetMethod("Compile")!;
var records = new List<object>();
foreach (var item in catalog.RootElement.GetProperty("cases").EnumerateArray().DistinctBy(c => c.GetProperty("id").GetString()))
{
    var source = item.GetProperty("source").GetString()!;
    var parsed = frontend.GetMethod("Compile")!.Invoke(null, [source, null, null, default(CancellationToken)])!;
    var syntax = Get(parsed, "Script") ?? throw new InvalidDataException("Invalid maintained source.");
    var executable = compile.Invoke(null, [lower.Invoke(null, [syntax])])!;
    var codes = new List<object>();
    Visit(Get(executable, "EntryPoint")!);
    records.Add(new { id = item.GetProperty("id").GetString(), codes });

    void Visit(object code)
    {
        var instructions = ((IEnumerable)Get(code, "Instructions")!).Cast<object>().ToArray();
        var calls = ((IEnumerable)Get(code, "CallSites")!).Cast<object>().ToArray();
        var root = (int?)Get(code, "NativeReturnRoot");
        var nodes = new HashSet<int>();
        if (root is >= 0) Walk(root.Value);
        var callNames = nodes.Where(i => Get(instructions[i], "OpCode")!.ToString() == "Call").Select(i =>
        {
            var member = (int)Get(instructions[i], "NativeSecond")!;
            var call = calls[(int)Get(instructions[i], "CallSiteIndex")!];
            return member < 0 ? "dynamic-unary/" + Get(call, "ArgumentCount") :
                ((IEnumerable)Get(code, "Names")!).Cast<string>().ElementAt((int)Get(instructions[member], "NameIndex")!) + "/" + Get(call, "ArgumentCount");
        }).ToArray();
        codes.Add(new { name = Get(code, "Name"), instructionCount = instructions.Length,
            nativeRoot = root, nativeNodeCount = nodes.Count, callNames,
            ordinaryInstructionBytesSavedIfEdgesRemoved = instructions.Length * 8,
            hypothetical16ByteNodesWith56ByteOwnerOverhead = root is null ? 0 : nodes.Count * 16 + 56 });
        foreach (var binding in (IEnumerable)Get(code, "Functions")!) Visit(Get(binding, "CodeObject")!);
        foreach (var value in (IEnumerable)Get(code, "Constants")!)
            if (value is not null && value.GetType().Name is "ExecutableCreateLambda" or "ExecutableCreateGeneratorExpression") Visit(Get(value, "CodeObject")!);

        void Walk(int index)
        {
            if (index < 0 || !nodes.Add(index)) return;
            var instruction = instructions[index];
            Walk((int)Get(instruction, "NativeFirst")!);
            if (Get(instruction, "OpCode")!.ToString() == "Call")
            {
                var call = calls[(int)Get(instruction, "CallSiteIndex")!];
                foreach (var argument in (IEnumerable)Get(call, "Arguments")!) Walk((int)Get(argument, "NativeSourceInstruction")!);
            }
            else Walk((int)Get(instruction, "NativeSecond")!);
        }
    }
}
Console.WriteLine(JsonSerializer.Serialize(new { coreSHA256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(args[0]))),
    purpose = "Read-only native graph/storage census; no prototype, allocation, CPU or elapsed claim. Hypothetical sizes exclude call inputs already retained and compiler scratch.", records }, new JsonSerializerOptions { WriteIndented = true }));
static object? Get(object value, string property) => value.GetType().GetProperty(property)!.GetValue(value);
