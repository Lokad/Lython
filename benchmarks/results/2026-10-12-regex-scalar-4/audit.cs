using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

var rows = new List<object>();
int failed = 0;
void Check(string name, Action action)
{
    try { action(); rows.Add(new { name, passed = true, error = (string?)null }); }
    catch (Exception error) { failed++; rows.Add(new { name, passed = false, error = error.ToString() }); }
}
void Expect<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Denied(Action action) { try { action(); } catch (Utf8ResourceLimitExceededException) { return; } throw new Exception("Expected resource denial"); }
var captures = new (string Pattern, string Input, int? LastIndex, string? LastGroup, string[] Groups)[]
{
    ("(?P<first>a)(b)", "ab", 2, null, ["ab","a","b"]),
    ("(a)(?P<second>b)", "ab", 2, "second", ["ab","a","b"]),
    ("((a))", "a", 1, null, ["a","a","a"]),
    ("(?P<outer>(a))", "a", 1, "outer", ["a","a","a"]),
    ("(?P<outer>(?P<inner>a))", "a", 1, "outer", ["a","a","a"]),
    ("(a(b)?)+", "aba", 1, null, ["aba","a","b"]),
    ("(a)(?=(b))", "ab", 2, null, ["a","a","b"]),
    ("(?:(a)b|(a)c)", "ac", 2, null, ["ac","","a"]),
    ("(a)?b", "b", null, null, ["b",""]),
    ("😀", "😀", null, null, ["😀"]),
    ("(?P<outer>(😀))", "😀", 1, "outer", ["😀","😀","😀"]),
    ("(?P<a>a)(b)\\1\\2", "abab", 2, null, ["abab","a","b"])
};
foreach (var item in captures) Check("capture/" + item.Pattern, () =>
{
    var regex = new Utf8PythonRegex(item.Pattern);
    var match = regex.SearchDetailedData(Encoding.UTF8.GetBytes(item.Input));
    Expect(true, match.Success); Expect(item.LastIndex, match.LastIndex); Expect(item.LastGroup, match.LastGroup);
    Expect(string.Join("|", item.Groups), string.Join("|", match.Groups.Select(g => g.ValueText)));
});
foreach (var policy in new (string Name, Utf8ResourcePolicy Value)[]
{
    ("work", new() { MaxWorkSteps = 1 }),
    ("depth", new() { MaxDepth = 1 }),
    ("working", new() { MaxWorkingBytes = 1 }),
    ("result", new() { MaxResultBytes = 1 })
}) Check("finite-policy/" + policy.Name, () => { _ = new Utf8PythonRegex("(a+)+b", PythonReCompileOptions.None, TimeSpan.FromSeconds(1), policy.Value); });
Check("cursor/retained-owned-snapshot-and-alias-position", () =>
{
    var regex = new Utf8PythonRegex("(?P<x>.)");
    var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("😀ab"));
    var alias = cursor;
    Expect(true, cursor.MoveNext()); var first = cursor.Current;
    Expect("😀", first.Value.ValueText);
    Expect(true, alias.MoveNext()); Expect("a", cursor.Current.Value.ValueText);
    cursor.Dispose(); cursor.Dispose();
    Expect("😀", first.Value.ValueText); Expect(1, first.LastIndex); Expect("x", first.LastGroup);
    try { alias.MoveNext(); } catch (ObjectDisposedException) { return; }
    throw new Exception("Disposed alias remained usable");
});
Check("cursor/empty-progress-and-eof", () =>
{
    using var cursor = new Utf8PythonRegex(".*?").CreateMatchCursor(Encoding.UTF8.GetBytes("😀a"));
    var values = new List<string>();
    while (cursor.MoveNext()) { values.Add(cursor.Current.Value.ValueText); if (values.Count > 5) throw new Exception("Cursor failed to progress"); }
    Expect("|😀||a|", string.Join("|", values)); Expect(false, cursor.MoveNext());
});
Check("cursor/live-cancel-between-advances", () =>
{
    using var cts = new CancellationTokenSource();
    using var cursor = new Utf8PythonRegex(".").CreateMatchCursor(Encoding.UTF8.GetBytes("abcdef"), 0, cts.Token);
    Expect(true, cursor.MoveNext()); cts.Cancel();
    try { cursor.MoveNext(); } catch (OperationCanceledException error) { Expect(cts.Token, error.CancellationToken); return; }
    throw new Exception("Live cancellation ignored");
});
Check("work/denial", () => Denied(() => new Utf8PythonRegex("a", PythonReCompileOptions.None, TimeSpan.FromSeconds(1), new() { MaxWorkSteps = 1 }).SearchDetailedData("aaaa"u8)));
Check("result/denial", () => Denied(() => new Utf8PythonRegex("(a)", PythonReCompileOptions.None, TimeSpan.FromSeconds(1), new() { MaxResultBytes = 1 }).SearchDetailedData("a"u8)));
Check("working/denial", () => Denied(() => new Utf8PythonRegex("(a)", PythonReCompileOptions.None, TimeSpan.FromSeconds(1), new() { MaxWorkingBytes = 1 }).SearchDetailedData("a"u8)));
Check("depth/denial", () => Denied(() => new Utf8PythonRegex("(?P<x>😀+)(?P<y>ab)", PythonReCompileOptions.None, TimeSpan.FromSeconds(1), new() { MaxDepth = 1 }).SearchDetailedData("😀😀ab"u8)));
Check("cursor/shared-work-exhaustion", () =>
{
    using var cursor = new Utf8PythonRegex(".", PythonReCompileOptions.None, TimeSpan.FromSeconds(1), new() { MaxWorkSteps = 256 }).CreateMatchCursor(Encoding.UTF8.GetBytes(new string('a', 100)));
    int found = 0;
    try { while (cursor.MoveNext()) found++; } catch (Utf8ResourceLimitExceededException) { if (found > 0 && found < 100) return; throw new Exception($"Unexpected admission {found}"); }
    throw new Exception("Shared work cap did not exhaust");
});
var unsafeReferences = new List<string>();
foreach (var assembly in new[] { typeof(Utf8PythonRegex).Assembly, typeof(Utf8ResourcePolicy).Assembly })
{
    using var stream = File.OpenRead(assembly.Location);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    foreach (var handle in metadata.MemberReferences)
    {
        var member = metadata.GetMemberReference(handle);
        if (member.Parent.Kind != HandleKind.TypeReference) continue;
        var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        var ns = metadata.GetString(type.Namespace);
        var typeName = metadata.GetString(type.Name);
        var methodName = metadata.GetString(member.Name);
        if ((ns == "System.Runtime.CompilerServices" && typeName == "Unsafe") || methodName.Contains("Unsafe", StringComparison.Ordinal))
            unsafeReferences.Add(assembly.GetName().Name + ":" + ns + "." + typeName + "." + methodName);
    }
}
var assemblies = new[] { typeof(Utf8PythonRegex).Assembly, typeof(Utf8ResourcePolicy).Assembly }.Select(a => new {
    name = a.GetName().Name, informationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))
});
Console.WriteLine(JsonSerializer.Serialize(new { total = rows.Count, failed, assemblies, unsafeApiReferences = unsafeReferences.Distinct().Order().ToArray(), rows }, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;
