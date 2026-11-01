using System.Text;
using System.Text.Json;

// Semantic feasibility only. No Lython execution, timing or production change.
var mismatches = new List<object>();
var checkedScalars = 0;
for (var value = 0; value <= 0x10ffff; value++)
{
    if (!Rune.IsValid(value) || value is 0x130 or 0x3a3) continue;
    var rune = new Rune(value);
    var input = rune.ToString();
    var expected = Rune.ToLowerInvariant(rune).ToString();
    var single = input.ToLowerInvariant();
    var mixed = ("A" + input + "z").ToLowerInvariant();
    if (single != expected || mixed != "a" + expected + "z")
        mismatches.Add(new { scalar = value, expected, single, mixed });
    checkedScalars++;
}
Console.WriteLine(JsonSerializer.Serialize(new {
    purpose = "Pinned BCL scalar/mixed-context equivalence gate, no performance evidence",
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    checkedScalars, excludedPythonExpansionOrContextScalars = new[] { 0x130, 0x3a3 }, mismatches,
    result = mismatches.Count == 0 ? "Equivalent within this gate" : "Not equivalent; investigate before selecting any runtime path"
}, new JsonSerializerOptions { WriteIndented = true }));
