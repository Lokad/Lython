using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class HostPathScenarioTests
{
    [Fact]
    public void CopySelectedFilesFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "HostPath", "CopySelectedFiles"));
        var host = new MockLythonHost();

        foreach (var file in fixture.InputFiles)
        {
            host.SeedFile(file.Key, file.Value);
        }

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public void ManageFilesFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "HostPath", "ManageFiles"));
        var host = new MockLythonHost();

        foreach (var file in fixture.InputFiles)
        {
            host.SeedFile(file.Key, file.Value);
        }

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public void MkdirWithoutExistingParent_Fails()
    {
        var result = new LythonEngine().Run(
            """
mkdir("/missing/child")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure!.ExceptionType);
        Assert.Contains("Parent directory does not exist", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HostExceptions_AreConvertedToStructuredRuntimeFailures()
    {
        var result = new LythonEngine().Run(
            """
write_text("/out.txt", "payload")
""",
            new ThrowingWriteHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure!.ExceptionType);
        Assert.Contains("Host write_text failed", result.Failure.Message, StringComparison.Ordinal);
        Assert.Contains("disk quota exceeded", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stat_ExposesStableIsoTimestamp()
    {
        var host = new MockLythonHost();
        host.SeedFile("/note.txt", "hello");

        var result = new LythonEngine().Run(
            """
info = stat("/note.txt")
write_text("/out.txt", info.modified_at)
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("1970-01-01T00:00:00Z", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ListdirMissingDirectory_Fails()
    {
        var result = new LythonEngine().Run(
            """
listdir("/missing")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure!.ExceptionType);
        Assert.Contains("Directory does not exist", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveNonEmptyDirectory_Fails()
    {
        var host = new MockLythonHost();
        host.SeedFile("/dir/file.txt", "x");

        var result = new LythonEngine().Run(
            """
remove("/dir")
""",
            host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure!.ExceptionType);
        Assert.Contains("Directory is not empty", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyToExistingDestination_Fails()
    {
        var host = new MockLythonHost();
        host.SeedFile("/src.txt", "a");
        host.SeedFile("/dst.txt", "b");

        var result = new LythonEngine().Run(
            """
copy("/src.txt", "/dst.txt")
""",
            host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure!.ExceptionType);
        Assert.Contains("Destination already exists", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MoveMissingSource_Fails()
    {
        var result = new LythonEngine().Run(
            """
move("/src.txt", "/dst.txt")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure!.ExceptionType);
        Assert.Contains("File does not exist", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StatMissingPath_ReportsNonExistingShape()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
info = stat("/missing.txt")
write_text("/out.txt", str(info.exists) + "|" + str(info.is_file) + "|" + str(info.is_dir) + "|" + str(info.size))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("False|False|False|0", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ExistsAndListdir_SupportRelativePathsAndSortedEntries()
    {
        var host = new MockLythonHost("/work");
        host.SeedFile("/work/b.txt", "b");
        host.SeedFile("/work/a.txt", "a");
        host.SeedFile("/work/nested/x.txt", "x");

        var result = new LythonEngine().Run(
            """
vals = []
vals.append(str(exists(".")))
vals.append(str(exists("a.txt")))
vals.append(str(exists("missing.txt")))
vals.append(str(listdir(".")))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True|True|False|[a.txt, b.txt, nested]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CopyAndMove_UpdateFilesystemState()
    {
        var host = new MockLythonHost("/work");
        host.SeedFile("/work/src.txt", "alpha");

        var result = new LythonEngine().Run(
            """
copy("src.txt", "copy.txt")
move("copy.txt", "moved.txt")
vals = []
vals.append(str(exists("src.txt")))
vals.append(str(exists("copy.txt")))
vals.append(str(exists("moved.txt")))
vals.append(read_text("moved.txt"))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True|False|True|alpha", host.ReadText("/out.txt"));
    }

    [Fact]
    public void RemoveEmptyDirectory_Succeeds()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
mkdir("/empty")
remove("/empty")
write_text("/out.txt", str(exists("/empty")))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenReadLines_AndFileIteration_SupportTextProcessingWorkflows()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "alpha\nbeta\nlast");

        var result = new LythonEngine().Run(
            """
with open("/input.txt", "r") as handle:
    lines = handle.readlines()
    pieces = []
    for line in handle:
        pieces.append(line.upper())

write_text("/out.txt", str(lines) + "|" + "".join(pieces))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("[alpha\n, beta\n, last]|ALPHA\nBETA\nLAST", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OpenWriteLines_AppendsAllStringsWithoutSeparators()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
with open("/output.txt", "w") as handle:
    handle.writelines(["alpha\n", "beta", "\ngamma"])

write_text("/out.txt", read_text("/output.txt"))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("alpha\nbeta\ngamma", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibPath_SupportsCoreAgentScriptPathShapes()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/scripts/tool.py", "x");
        host.SeedFile("/repo/src/content/en/a.md", "a");
        host.SeedFile("/repo/src/content/en/nested/b.md", "b");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

repo = Path(__file__).resolve().parents[1]
src_root = repo / "src" / "content" / "en"
items = sorted(src_root.rglob("*.md"))
rel = items[1].relative_to(src_root).as_posix()
vals = []
vals.append(repo.as_posix())
vals.append(src_root.name)
vals.append(items[0].suffix)
vals.append(items[0].stem)
vals.append(rel)
write_text("/out.txt", "|".join(vals))
""",
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/scripts/tool.py"
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo|en|.md|a|nested/b.md", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibPath_TextIoAndQueries_AreHostMediated()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/input.txt", "alpha\r\nbeta\r\n");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

src = Path("input.txt")
dst = Path("out.txt")
text = src.read_text(encoding="utf-8")
dst.write_text(text, encoding="utf-8", newline="")
vals = []
vals.append(str(src.exists()))
vals.append(str(src.is_file()))
vals.append(str(Path(".").is_dir()))
vals.append(dst.read_text())
write_text("/result.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|alpha\nbeta\n", host.ReadText("/result.txt"));
    }

    [Fact]
    public void PathlibPath_TextEditingScriptPatterns_AreFirstClass()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile(
            "/repo/tool.py",
            "\uFEFF# header\r\ndef render():\r\n    return f\"{{old}} café\"\r\nPLACEHOLDER PLACEHOLDER\r\n");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

path = Path("/repo/tool.py")
text = path.read_text(encoding="utf-8-sig", errors="strict")
needle = '''def render():
    return f"{{old}} café"
'''
replacement = '''def render():
    return f"{{new}} café"
'''

if needle not in text:
    raise SystemExit("needle missing")

start = text.find(needle)
text = text[:start] + replacement + text[start + len(needle):]
text = text.replace("PLACEHOLDER", "done", 1)
written = path.write_text(text, encoding="UTF-8-SIG", errors="strict", newline="")
after = path.read_text(encoding="utf-8-sig")
write_text("/result.txt", str(written == len(text)) + "|" + after)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "True|# header\n" +
            "def render():\n" +
            "    return f\"{{new}} café\"\n" +
            "done PLACEHOLDER\n",
            host.ReadText("/result.txt"));
        Assert.StartsWith("\uFEFF# header\n", host.ReadText("/repo/tool.py"), StringComparison.Ordinal);
    }

    [Fact]
    public void PathlibPath_TextEditingScriptCanExitWhenNeedleIsMissing()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/tool.py", "alpha\n");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

text = Path("/repo/tool.py").read_text()
if "needle" not in text:
    raise SystemExit("needle missing")
""",
            host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure!.ExceptionType);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("needle missing", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PathlibPath_ExplicitRepoRootBranchMatchesScriptLikeTraversal()
    {
        var host = new MockLythonHost("/");
        host.SeedFile("/repo/src/content/en/a.md", "a");
        host.SeedFile("/repo/src/content/en/nested/b.md", "b");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

repo_root = Path("repo").resolve()
src_root = repo_root / "src" / "content" / "en"
items = []
for path in sorted(src_root.rglob("*.md")):
    items.append(path.relative_to(src_root).as_posix())

write_text("/out.txt", repo_root.as_posix() + "|" + str(items) + "|" + Path("FILE.MD").suffix.lower())
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo|[a.md, nested/b.md]|.md", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ImportedModules_ExposeFileOriginForPathlib()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/helper.py", "from pathlib import Path\nvalue = Path(__file__).parent.as_posix()\n");

        var result = new LythonEngine().Run(
            """
import helper
write_text("/out.txt", helper.value)
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibPath_ParentsAndRelativeToCoverDeeperScriptTraversal()
    {
        var host = new MockLythonHost("/repo/site/tools");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

base = Path(__file__).resolve()
top = base.parents[3]
child = top / "content" / "guide" / "intro.md"
rel = child.relative_to(top / "content")
write_text("/out.txt", top.as_posix() + "|" + rel.as_posix())
""",
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/site/tools/nested/script.py"
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo|guide/intro.md", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
with open("/output.txt", "w") as handle:
    handle.readlines()
""",
        "LA3109")]
    [InlineData(
        """
with open("/input.txt", "r") as handle:
    handle.writelines(["x"])
""",
        "LA3110")]
    [InlineData(
        """
with open("/output.txt", "w") as handle:
    handle.writelines([1, 2])
""",
        "LA3112")]
    public void FileHandleExtendedMethods_ReportStaticDiagnostics(string source, string diagnosticCode)
    {
        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == diagnosticCode);
    }

    private sealed class ThrowingWriteHost : ILythonHost
    {
        private readonly MockLythonHost _inner = new();

        public string Cwd => _inner.Cwd;

        public DateTimeOffset LocalNow => _inner.LocalNow;

        public DateTimeOffset UtcNow => _inner.UtcNow;

        public ILythonTextInput? StandardInput => _inner.StandardInput;

        public ILythonTextOutput? StandardOutput => _inner.StandardOutput;

        public ILythonTextOutput? StandardError => _inner.StandardError;

        public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
            => _inner.ReadTextUtf8Async(path, cancellationToken);

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => throw new IOException("disk quota exceeded");

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
            => _inner.ExistsAsync(path, cancellationToken);

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirAsync(path, cancellationToken);

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
            => _inner.MkDirAsync(path, cancellationToken);

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
            => _inner.RemoveAsync(path, cancellationToken);

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.CopyAsync(source, destination, cancellationToken);

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.MoveAsync(source, destination, cancellationToken);

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
            => _inner.StatAsync(path, cancellationToken);
    }
}
