using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

var operations = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);
var images = args.Select((path, i) => new AssemblyLoadContext("inspection-" + i, true)
    .LoadFromAssemblyPath(Path.GetFullPath(path))).ToArray();
string TypeName(Type? type) => type?.ToString() ?? "void";
string Identity(MemberInfo member) => member is Type type ? TypeName(type)
    : TypeName(member.DeclaringType) + "::" + member;
string[] Body(MethodBase method)
{
    var body = method.GetMethodBody();
    if (body is null) return ["no-body"];
    var rows = new List<string> { $"impl:{method.MethodImplementationFlags};attrs:{method.Attributes};stack:{body.MaxStackSize};init:{body.InitLocals}",
        "locals:" + string.Join(";", body.LocalVariables.Select(v => $"{v.LocalIndex}:{TypeName(v.LocalType)}:{v.IsPinned}")) };
    foreach (var e in body.ExceptionHandlingClauses)
        rows.Add($"exception:{e.Flags}:{e.TryOffset}:{e.TryLength}:{e.HandlerOffset}:{e.HandlerLength}:" +
            (e.Flags == ExceptionHandlingClauseOptions.Clause ? TypeName(e.CatchType) : e.Flags == ExceptionHandlingClauseOptions.Filter ? e.FilterOffset.ToString() : ""));
    var bytes = body.GetILAsByteArray()!;
    var types = method.DeclaringType?.GetGenericArguments();
    var methods = method.IsGenericMethod ? method.GetGenericArguments() : null;
    for (var i = 0; i < bytes.Length;)
    {
        short code = bytes[i++];
        if (code == 0xFE) code = (short)(0xFE00 | bytes[i++]);
        var op = operations[code];
        string value;
        switch (op.OperandType)
        {
            case OperandType.InlineNone: value = ""; break;
            case OperandType.ShortInlineI: case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineVar:
                value = bytes[i++].ToString(); break;
            case OperandType.InlineVar: value = BitConverter.ToUInt16(bytes, i).ToString(); i += 2; break;
            case OperandType.InlineI: case OperandType.InlineBrTarget:
                value = BitConverter.ToInt32(bytes, i).ToString(); i += 4; break;
            case OperandType.InlineI8: value = BitConverter.ToInt64(bytes, i).ToString(); i += 8; break;
            case OperandType.ShortInlineR: value = BitConverter.ToSingle(bytes, i).ToString("R", CultureInfo.InvariantCulture); i += 4; break;
            case OperandType.InlineR: value = BitConverter.ToDouble(bytes, i).ToString("R", CultureInfo.InvariantCulture); i += 8; break;
            case OperandType.InlineSwitch:
                var count = BitConverter.ToInt32(bytes, i); i += 4;
                value = string.Join(',', Enumerable.Range(0, count).Select(n => BitConverter.ToInt32(bytes, i + n * 4)));
                i += 4 * count; break;
            default:
                var token = BitConverter.ToInt32(bytes, i); i += 4;
                value = op.OperandType == OperandType.InlineString ? method.Module.ResolveString(token)
                    : op.OperandType == OperandType.InlineSig ? Convert.ToHexString(method.Module.ResolveSignature(token))
                    : Identity(method.Module.ResolveMember(token, types, methods)!);
                break;
        }
        rows.Add(op.Name + " " + value);
    }
    // Adding a source method renumbers compiler-generated closures/state machines.
    // Compare common method bodies with those reference-name ordinals elided;
    // retain the original unnormalized inventory separately. This is structural
    // evidence, not a proof about execution paths or native generated code.
    return rows.Select(row => row.StartsWith("ldstr ", StringComparison.Ordinal) ? row
        : Regex.Replace(row, @"(?<=\|)\d+(?=_)|(?<=b__)\d+(?=_)|(?<=DisplayClass)\d+(?=_)|(?<=d__)\d+", "#")).ToArray();
}
Dictionary<string, string[]> Inventory(Assembly image) => image.GetTypes().SelectMany(t =>
    t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Cast<MethodBase>().Concat(t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)))
    .ToDictionary(Identity, Body);
var a = Inventory(images[0]); var b = Inventory(images[1]);
var changed = a.Keys.Intersect(b.Keys).Where(k => !a[k].SequenceEqual(b[k])).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new
{
    source = "Semantic IL/build-policy inventory only; no executed-path, JIT-code or CPU equivalence claim.",
    images = images.Select((assembly, i) => new { path = args[i], sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[i]))),
        attributes = assembly.GetCustomAttributesData().Where(x => x.AttributeType.Name is "DebuggableAttribute" or "TargetFrameworkAttribute" or "AssemblyMetadataAttribute" or "AssemblyInformationalVersionAttribute").Select(x => x.ToString()).ToArray() }),
    beforeMethods = a.Count, candidateMethods = b.Count,
    identicalBodies = a.Keys.Intersect(b.Keys).Count() - changed.Length,
    removedMethods = a.Keys.Except(b.Keys).ToArray(), addedMethods = b.Keys.Except(a.Keys).ToArray(),
    changedBodies = changed.Select(k => new { method = k, before = a[k], candidate = b[k] }),
}, new JsonSerializerOptions { WriteIndented = true }));
