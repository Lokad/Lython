using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 3) throw new ArgumentException("DLL, source directory, new output file required.");
var dll = Path.GetFullPath(args[0]);
var sourceRoot = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args[2]);
if (File.Exists(output)) throw new IOException("Preserve inventory evidence.");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    var dependency = Path.Combine(Path.GetDirectoryName(dll)!, name.Name + ".dll");
    return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
var types = assembly.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
var methods = types.SelectMany(t => t.GetMethods(Declared).Cast<MethodBase>().Concat(t.GetConstructors(Declared))
        .Concat(t.TypeInitializer is { } initializer ? new MethodBase[] { initializer } : []))
    .DistinctBy(Key).OrderBy(Key, StringComparer.Ordinal).ToArray();
var methodMap = methods.ToDictionary(Key);
var dispatch = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
var mapFailures = new List<object>();
foreach (var type in types.Where(t => !t.IsInterface))
{
    foreach (var contract in type.GetInterfaces())
    {
        try
        {
            var map = type.GetInterfaceMap(contract);
            for (var i = 0; i < map.InterfaceMethods.Length; i++)
                if (methodMap.ContainsKey(Key(map.TargetMethods[i]))) AddDispatch(Key(map.InterfaceMethods[i]), Key(map.TargetMethods[i]));
        }
        catch (ArgumentException ex) { mapFailures.Add(new { type = type.FullName, contract = contract.ToString(), error = ex.Message }); }
    }
    foreach (var method in type.GetMethods(Declared).Where(m => m.IsVirtual))
        AddDispatch(Key(method.GetBaseDefinition()), Key(method));
}
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public).Where(f => f.FieldType == typeof(OpCode))
    .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
var nodes = new List<Node>();
var unresolved = new List<object>();
foreach (var method in methods)
{
    var body = method.GetMethodBody();
    var calls = new List<Call>();
    var bytes = body?.GetILAsByteArray() ?? [];
    var offset = 0;
    while (offset < bytes.Length)
    {
        var start = offset;
        ushort code = bytes[offset++];
        if (code == 0xfe) code = (ushort)(0xfe00 | bytes[offset++]);
        var opcode = opcodes[code];
        if (opcode.OperandType == OperandType.InlineMethod)
        {
            var token = BitConverter.ToInt32(bytes, offset);
            try
            {
                var callee = method.Module.ResolveMethod(token,
                    method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null)!;
                var potential = dispatch.GetValueOrDefault(Key(callee))?.Order(StringComparer.Ordinal).ToArray() ?? [];
                var contract = callee.DeclaringType;
                var admission = contract?.FullName == "Lokad.Lython.Runtime.MemoryGovernor" &&
                    callee.Name is "Reserve" or "EnsureCanReserve" or "ReserveTemporary";
                var opaqueDispatch = contract?.IsInterface == true || contract?.IsSubclassOf(typeof(MulticastDelegate)) == true;
                calls.Add(new(start, opcode.Name!, Key(callee), Name(callee), admission, opaqueDispatch, potential));
            }
            catch (Exception ex) when (ex is ArgumentException or BadImageFormatException or FileNotFoundException or TypeLoadException)
            {
                unresolved.Add(new { method = Name(method), offset = start, token, error = ex.GetType().Name + ": " + ex.Message });
            }
        }
        offset += OperandSize(opcode.OperandType, bytes, offset);
    }
    if (offset != bytes.Length) throw new InvalidDataException("IL extent mismatch: " + Name(method));
    // Compiler-generated entry wrappers start their state machines through
    // framework builders. Include MoveNext explicitly rather than treating
    // an async/iterator wrapper with no direct Reserve as pressure-free.
    var stateType = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
        ?? method.GetCustomAttribute<AsyncIteratorStateMachineAttribute>()?.StateMachineType
        ?? method.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;
    if (stateType is not null)
        foreach (var entry in stateType.GetMethods(Declared).Where(m => m.Name == "MoveNext" || m.Name.EndsWith(".MoveNext", StringComparison.Ordinal)))
            calls.Add(new(-1, "state-machine-entry", Key(entry), Name(entry), false, false, []));
    nodes.Add(new(Key(method), Name(method), body is not null, method.IsStatic, method.DeclaringType!.IsDefined(typeof(CompilerGeneratedAttribute), false),
        method.IsDefined(typeof(AsyncStateMachineAttribute), false), method.IsDefined(typeof(IteratorStateMachineAttribute), false),
        method.GetParameters().Select(p => new Variable(p.Name!, p.ParameterType.ToString(), References(p.ParameterType, []))).ToArray(),
        body?.LocalVariables.Select(v => new Variable(v.LocalIndex.ToString(), v.LocalType.ToString(), References(v.LocalType, []))).ToArray() ?? [], calls));
}
var direct = nodes.Where(n => n.Calls.Any(c => c.Admission)).Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
var conservative = nodes.Where(n => n.Calls.Any(c => c.Admission || c.OpaqueDispatch)).Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
Expand(direct);
Expand(conservative);
var source = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
    .Where(p => !Path.GetRelativePath(sourceRoot, p).Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin"))
    .Order(StringComparer.Ordinal).Select(p => new { path = Path.GetRelativePath(sourceRoot, p).Replace('\\', '/'), sha256 = Hash(p),
        preflightSpellings = Lines(p, "Reserve(", "ReserveTemporary(", "EnsureCanReserve("),
        registrations = Lines(p, "TrackFreshMutable(", "TrackFreshString(", "TrackCallResult(", "TrackMutable(", "TrackString(", "TrackPrefundedString(") }).ToArray();
var typeRows = types.Select(t => new
{
    name = t.FullName, t.IsValueType, t.IsInterface,
    compilerGenerated = t.IsDefined(typeof(CompilerGeneratedAttribute), false),
    snapshotOwner = !t.IsInterface && t.GetInterfaces().Any(i => i.FullName == "Lokad.Lython.Runtime.IPyOwnershipSnapshot"),
    finalizableOwner = !t.IsInterface && t.GetInterfaces().Any(i => i.FullName == "Lokad.Lython.Runtime.IPyFinalizableOwnership"),
    baseType = t.BaseType?.ToString(), contracts = t.GetInterfaces().Select(i => i.ToString()).Order(StringComparer.Ordinal),
    fields = t.GetFields(Declared).Select(f => new { f.Name, type = f.FieldType.ToString(), f.IsStatic, f.IsInitOnly,
        containsReferences = References(f.FieldType, []), opaqueDelegate = typeof(Delegate).IsAssignableFrom(f.FieldType) }).OrderBy(f => f.Name, StringComparer.Ordinal)
}).ToArray();
var result = new
{
    schemaVersion = 1, purpose = "Static assembly/source inventory, not a runtime heap traversal or proof of complete live roots",
    dll = new { path = dll, sha256 = Hash(dll), assembly = assembly.FullName, moduleId = assembly.ManifestModule.ModuleVersionId },
    tool = new { runtime = RuntimeInformation.FrameworkDescription, sha256 = Hash(Assembly.GetExecutingAssembly().Location) },
    summary = new { types = types.Length, methods = methods.Length, owners = typeRows.Count(t => t.snapshotOwner), finalizableOwners = typeRows.Count(t => t.finalizableOwner),
        runtimeTypes = types.Count(t => t.Namespace?.StartsWith("Lokad.Lython.Runtime", StringComparison.Ordinal) == true),
        frontendTypes = types.Count(t => t.Namespace?.StartsWith("Lokad.Lython.Frontend", StringComparison.Ordinal) == true),
        referenceFields = typeRows.Sum(t => t.fields.Count(f => !f.IsStatic && f.containsReferences)),
        opaqueDelegateFields = typeRows.Sum(t => t.fields.Count(f => !f.IsStatic && f.opaqueDelegate)),
        compilerGeneratedTypes = typeRows.Count(t => t.compilerGenerated), directAdmissionMethods = nodes.Count(n => n.Calls.Any(c => c.Admission)),
        transitiveAdmissionMethods = direct.Count, conservativePressureMethods = conservative.Count, unresolvedCalls = unresolved.Count,
        interfaceMapFailures = mapFailures.Count, sourceFiles = source.Length },
    limitations = new[] { "C# local signatures are not live roots", "Virtual/interface expansion is conservative; unresolved calls stay explicit", "External/native-library calls are not analyzed transitively", "Source spellings are location evidence, not semantic call coverage", "No root publication, child traversal, storage release or reclamation has been enabled" },
    source, types = typeRows, methods = nodes.Select(n => new { node = n, reachesAdmission = direct.Contains(n.Id), conservativePressure = conservative.Contains(n.Id) }),
    unresolved, mapFailures
};
File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + "\n");
Console.WriteLine(JsonSerializer.Serialize(result.summary));

void AddDispatch(string contract, string target)
{
    if (!dispatch.TryGetValue(contract, out var implementations)) dispatch[contract] = implementations = new(StringComparer.Ordinal);
    implementations.Add(target);
}
void Expand(HashSet<string> reachable)
{
    bool changed;
    do
    {
        changed = false;
        foreach (var node in nodes)
            if (!reachable.Contains(node.Id) && node.Calls.Any(c => reachable.Contains(c.Target) || c.Implementations.Any(reachable.Contains)))
                changed |= reachable.Add(node.Id);
    } while (changed);
}
static string Key(MethodBase method) => method.Module.ModuleVersionId + ":" + method.MetadataToken;
static string Name(MethodBase method) => method.DeclaringType + "." + method;
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
static bool References(Type type, HashSet<Type> seen)
{
    if (type.IsPointer || type.IsPrimitive || type.IsEnum) return false;
    if (type.IsByRef) return References(type.GetElementType()!, seen);
    if (!type.IsValueType || type.IsGenericParameter) return true;
    if (!seen.Add(type)) return false;
    var result = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(f => References(f.FieldType, seen));
    seen.Remove(type);
    return result;
}
static object[] Lines(string path, params string[] names) => File.ReadLines(path).Select((text, index) => new { line = index + 1, text = text.Trim() })
    .Where(line => names.Any(name => line.text.Contains(name, StringComparison.Ordinal))).Cast<object>().ToArray();
static int OperandSize(OperandType type, byte[] bytes, int offset) => type switch
{
    OperandType.InlineNone => 0,
    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
    OperandType.InlineVar => 2,
    OperandType.InlineI8 or OperandType.InlineR => 8,
    OperandType.InlineSwitch => checked(4 + BitConverter.ToInt32(bytes, offset) * 4),
    _ => 4
};
sealed record Variable(string Name, string Type, bool ContainsReferences);
sealed record Call(int Offset, string Opcode, string Target, string Name, bool Admission, bool OpaqueDispatch, string[] Implementations);
sealed record Node(string Id, string Name, bool HasBody, bool IsStatic, bool GeneratedType, bool AsyncMethod, bool IteratorMethod, Variable[] Parameters, Variable[] Locals, List<Call> Calls);
