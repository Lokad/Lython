using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
var assemblyPath = Path.GetFullPath(args[0]);
var assembly = Assembly.LoadFrom(assemblyPath);
var lookup = assembly.GetType("Lokad.Lython.Runtime.Text.PyStringOps", true)!
    .GetMethod("GetSigmaProperties", BindingFlags.NonPublic | BindingFlags.Static)!
    .CreateDelegate<Func<int,int>>();
var expected = File.ReadAllBytes(args[1]);
if (expected.Length != 0x110000) throw new Exception("Missing frozen Unicode domain.");
var actual = new byte[expected.Length];
for (var scalar = 0; scalar < expected.Length; scalar++)
{
    var flags = lookup(scalar);
    if (flags != expected[scalar]) throw new Exception($"Property mismatch at {scalar:X}.");
    actual[scalar] = (byte)flags;
}
foreach (var scalar in new[] { -1, int.MinValue, 0x110000, int.MaxValue })
    if (lookup(scalar) != 0) throw new Exception("Out-of-domain lookup changed.");
Console.WriteLine(JsonSerializer.Serialize(new { positionsChecked = expected.Length,
    flagsSHA256 = Convert.ToHexString(SHA256.HashData(actual)),
    assemblySHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
    allEquivalent = true, unsafeCode = false }));
