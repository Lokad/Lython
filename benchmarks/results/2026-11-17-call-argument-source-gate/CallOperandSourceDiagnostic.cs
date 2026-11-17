using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class CallOperandSourceDiagnostic
{
    [Fact]
    public void ObserveCurrentArgumentStaging()
    {
        const string notes = "D:/code/Lython/.git/agent-notes/review-20261031";
        using var catalog = JsonDocument.Parse(File.ReadAllText(notes + "/json-tokens-r469/catalog.json", Encoding.UTF8));
        var rows = new List<object>();
        foreach (var item in catalog.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            if (id is not ("scripts.text-normalization.helper-list" or "scripts.report-render.helper-list"
                or "scripts.numeric-squared-error.large" or "calls.positional.large")) continue;
            var frontend = LythonFrontend.Compile(item.GetProperty("source").GetString()!, regexProvider: Utf8RegexProvider.Instance);
            Assert.NotNull(frontend.Script);
            var executable = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script));
            Capture(executable.EntryPoint);

            void Capture(ExecutableCodeObject code)
            {
                for (var pc = 0; pc < code.Instructions.Length; pc++)
                {
                    var instruction = code.Instructions[pc];
                    if (instruction.OpCode != ExecutableOpCode.Call) continue;
                    var site = code.CallSites[instruction.CallSiteIndex];
                    var tail = 0;
                    for (var i = pc - 1; i >= 0 && tail < site.ArgumentCount; i--)
                    {
                        var leaf = code.Instructions[i];
                        if (leaf.OpCode == ExecutableOpCode.LoadConst || leaf.OpCode == ExecutableOpCode.LoadLocal
                            && !code.RequiresLocalVariableMirroring && !code.CapturedLocalSlots.Contains(leaf.LocalSlot)) tail++;
                        else break;
                    }
                    rows.Add(new
                    {
                        workload = id, function = code.Name, code.IsFunctionScope, code.RequiresLocalVariableMirroring,
                        instruction = pc, site.ArgumentCount, site.IsPositionalOnly, site.HasReceiver,
                        adjacentPureArgumentLoads = tail,
                        precedingInstructions = code.Instructions.Skip(Math.Max(0, pc - 5)).Take(Math.Min(5, pc)).Select(x => x.OpCode.ToString()).ToArray()
                    });
                }
                foreach (var function in code.Functions) Capture(function.CodeObject);
            }
        }
        Directory.CreateDirectory(notes + "/call-argument-staging-r470");
        File.WriteAllText(notes + "/call-argument-staging-r470/current-shapes.json",
            JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }) + "\n", Encoding.UTF8);
    }
}
