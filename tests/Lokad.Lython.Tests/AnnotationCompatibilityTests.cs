using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class AnnotationCompatibilityTests
{
    [Fact]
    public void FutureAnnotationsAndModernAnnotationShapes_AreAcceptedAsNoOpMetadata()
    {
        var source =
            """
from __future__ import annotations

def choose(values: list[str], fallback: tuple[str, str] | None = None) -> dict[str, int]:
    result: dict[str, int] = {"count": len(values)}
    return result

write_text("/out.txt", str(choose(["a", "b"])))
""";

        var compiled = new LythonEngine().Compile(source);

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Empty(compiled.Diagnostics);

        var host = new MockLythonHost();
        var result = compiled.Run(host, options: null);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{'count': 2}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FutureAnnotations_TolerateArgparseNamespaceAndPathTypes()
    {
        var source =
            """
from __future__ import annotations

import argparse
from pathlib import Path

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--lang", required=True, choices=("fr", "de"))
    parser.add_argument("--repo-root", default=None)
    return parser.parse_args()

def build_root(raw: Path | None) -> Path:
    return Path(raw).resolve() if raw else Path(__file__).resolve().parent

args = parse_args()
root = build_root(args.repo_root)
write_text("/out.txt", root.as_posix())
""";

        var compiled = new LythonEngine().Compile(source);

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Empty(compiled.Diagnostics);

        var host = new MockLythonHost("/repo");
        var result = compiled.Run(
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/tool.py",
                Args = ["--lang", "fr"]
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FutureAnnotations_TolerateRegexMatchGenericAnnotations()
    {
        var source =
            """
from __future__ import annotations
import re

def replacer(match: re.Match[str]) -> str:
    return match.group("label")

pat = re.compile(r"(?P<label>[a-z]+)")
write_text("/out.txt", pat.sub(replacer, "ab cd"))
""";

        var compiled = new LythonEngine().Compile(source);

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Empty(compiled.Diagnostics);

        var host = new MockLythonHost();
        var result = compiled.Run(host, options: null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ab cd", host.ReadText("/out.txt"));
    }

    [Fact]
    public void AnnotationDiagnostics_ReportHighConfidenceLiteralMismatches()
    {
        var compiled = new LythonEngine().Compile(
            """
x: int = "bad"

def sample(flag: bool = "oops") -> int:
    return "bad"
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA1080");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA1081");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA1082");
    }

    [Fact]
    public void AnnotationDiagnostics_UseAbstractValueFlowForProvableMismatches()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
lines = text.splitlines()

bad_text: int = text
bad_lines: str = lines

def sample(default: int = text) -> list:
    return text
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA1080" && d.Message.Contains("int", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA1080" && d.Message.Contains("list", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA1081");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA1082");
    }

    [Fact]
    public void AnnotationDiagnostics_WidenUnknownBranchValuesInsteadOfGuessing()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

if flag:
    value = Path("/repo/input.txt").read_text()
else:
    value = 1

accepted: str = value
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }
}
