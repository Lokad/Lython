using System.Security.Cryptography;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SigmaContextBoundaryTests
{
    // Independent CPython 3.13.2 UTF-8 hashes: lower, capitalize, swapcase, title.
    public static IEnumerable<object[]> Cases()
    {
        yield return ["\u0345Σ\u0345Σ", new[] {
            "c624f1d9e09f8ae40457f915e1b2c824681bfa482c22725455e145d39ab01cb9",
            "9b364ed5383164e33bec796e02f99e18fffb8ba91148e5ecccbd2b8761c4150a",
            "ce2b07c3f1cc78e2a8da62f017579f6f5d4b5c76c944ca9ea25e383ca85035c3",
            "9b364ed5383164e33bec796e02f99e18fffb8ba91148e5ecccbd2b8761c4150a" }];
        yield return ["𐐀" + new string('\u0345', 257) + "Σ" + new string('\u200d', 257)
            + "Σ" + new string('\u0301', 257) + "/Σ", new[] {
            "220862edf786bdf73a569ead33b769c4c59391a8d9e029278edd907a99fb248a",
            "1f0e91ee1d414cf9d1373829ea3fd8c256928c22fc715caeb3959c0c5f114195",
            "9f2f8f9d81630d061d06e21818787c5f1c567ea7ba0963e8efe6d62f2ac49cd2",
            "60f2e5fe59a320468e5b382da29827913d8a4fe20f556ee1088cab76fc053473" }];
        yield return ["A" + string.Concat(Enumerable.Repeat("Σ" + new string('.', 512) + "\u0345", 64))
            + "Σ😀Σ", new[] {
            "cddef222fb85124b8a88e2e23826ab9e80044b51a037a46524ffd221d70cc037",
            "e3d0fdd92cd402c143ded7f2a5cfea4a342a4b6d8b607c706f5f228816ebd7de",
            "3711eacafa09343c08fbaced14e6c45eb2ec9a0803ade234244c4b29426f021a",
            "f79ef27b50340137f21910654c12f298829b2debf6dcc8d6e4950a07e2817133" }];
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task OriginalContextAndIgnorableOverlapMatchPython(string input, string[] expected)
    {
        using var script = new LythonEngine().Compile(
            "return [source.lower(), source.capitalize(), source.swapcase(), source.title()]");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var options = new LythonRunOptions
        {
            MemoryBudget = budget,
            Globals = new Dictionary<string, object?> { ["source"] = input },
        };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsType<List<object?>>(result.ReturnValue);
                Assert.Equal(expected.Length, values.Count);
                for (var index = 0; index < values.Count; index++)
                {
                    var text = Assert.IsType<string>(values[index]);
                    Assert.Equal(expected[index], Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
                }
                Assert.True(budget.ReservedBytes > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
