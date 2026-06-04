using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PathlibModuleFunctionTests
{
    [Fact]
    public void PathlibModule_PurePathHelpers_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

base = Path("/repo", "docs", "page.md")
vals = []
vals.append(Path().as_posix())
vals.append(Path(Path("/repo"), "docs").as_posix())
vals.append(base.name)
vals.append(base.suffix)
vals.append(str(Path("/repo/docs/archive.tar.gz").suffixes))
vals.append(base.stem)
vals.append(base.parent.as_posix())
vals.append(str(base.parents))
vals.append(str(base.parts))
vals.append(str(base.is_absolute()))
vals.append(base.joinpath("nested", "guide.txt").as_posix())
vals.append(base.with_name("intro.md").as_posix())
vals.append(base.with_suffix(".txt").as_posix())
vals.append(base.with_stem("chapter").as_posix())
vals.append(base.relative_to("/repo").as_posix())
vals.append(str(base.is_relative_to("/repo")))
vals.append(str(base.is_relative_to("/other")))
vals.append(Path("docs/page.md").absolute().as_posix())
vals.append(base.anchor + ":" + base.root + ":" + base.drive)
vals.append(str(base.match("*.md")))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(".|/repo/docs|page.md|.md|[.tar, .gz]|page|/repo/docs|[/repo/docs, /repo, /]|(/, repo, docs, page.md)|True|/repo/docs/page.md/nested/guide.txt|/repo/docs/intro.md|/repo/docs/page.txt|/repo/docs/chapter.md|docs/page.md|True|False|/repo/docs/page.md|/:/:|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibModule_HostMediatedTextAndTreeHelpers_HaveDirectCoverage()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/a.md", "alpha\nbeta\n");
        host.SeedFile("/repo/docs/sub/b.md", "bravo");
        host.SeedFile("/repo/docs/c.txt", "skip");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

src = Path("/repo/docs/a.md")
dst = Path("/repo/docs/out.md")
dst.write_text(src.read_text(encoding="utf-8-sig", errors="strict"), encoding="utf-8-sig", errors="strict", newline="")
entries = sorted(Path("/repo/docs").iterdir())
items = sorted(Path("/repo/docs").glob("*.md"))
tree = sorted(Path("/repo/docs").rglob("*.md"))
Path("/repo/deep/nested").mkdir(parents=True, exist_ok=True)
touched = Path("/repo/deep/nested/touched.txt")
touched.touch()
touched.unlink(missing_ok=True)
touched.unlink(missing_ok=True)
Path("/repo/deep/nested").rmdir()
replaced = Path("/repo/docs/c.txt").replace(Path("/repo/docs/replaced.txt"))
renamed = Path("/repo/docs/out.md").rename(Path("/repo/docs/final.md"))
Path("/repo/docs/final.md").unlink()
Path("/repo/newdir").mkdir()
with src.open(encoding="utf-8", errors="strict", newline="") as f:
    first = f.readline().rstrip()
vals = []
vals.append(first)
vals.append(str([item.name for item in entries]))
vals.append(str([item.name for item in items]))
vals.append(str([item.relative_to(Path("/repo/docs")).as_posix() for item in tree]))
vals.append(str(src.stat().st_size))
vals.append(str(src.stat().st_mtime))
vals.append(str(Path("/repo/deep/nested").exists()))
vals.append(replaced.name)
vals.append(str(src.is_symlink()))
vals.append(str(src.samefile(Path("/repo/docs/a.md"))))
vals.append(str(renamed.name))
vals.append(str(Path("/repo/newdir").is_dir()))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("alpha|[a.md, c.txt, out.md, sub]|[a.md, out.md]|[a.md, out.md, sub/b.md]|11|0|False|replaced.txt|False|True|final.md|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public async Task PathlibModule_RunAsync_AwaitsAsynchronousHostOperations()
    {
        var host = new DelayedLythonHost("/repo");
        host.SeedFile("/repo/docs/a.md", "alpha\nbeta\n");
        host.SeedFile("/repo/docs/sub/b.md", "bravo");
        host.SeedFile("/repo/docs/c.txt", "skip");

        var result = await new LythonEngine().RunAsync(
            """
from pathlib import Path

src = Path("/repo/docs/a.md")
dst = Path("/repo/docs/out.md")
dst.write_text(src.read_text(encoding="utf-8-sig", errors="strict"), encoding="utf-8-sig", errors="strict", newline="")
with Path("/repo/docs/opened.md").open("w", encoding="utf-8-sig", errors="strict", newline="") as f:
    f.write("opened")
with Path("/repo/docs/opened.md").open(encoding="utf-8-sig", errors="strict", newline="") as f:
    opened = f.read()
items = sorted(Path("/repo/docs").glob("*.md"))
tree = sorted(Path("/repo/docs").rglob("*.md"))
renamed = Path("/repo/docs/out.md").rename(Path("/repo/docs/final.md"))
Path("/repo/docs/final.md").unlink()
Path("/repo/newdir").mkdir()
Path("/repo/async/new").mkdir(parents=True, exist_ok=True)
touched = Path("/repo/async/new/file.txt")
touched.touch()
vals = []
vals.append(opened)
vals.append(str([item.name for item in items]))
vals.append(str([item.relative_to(Path("/repo/docs")).as_posix() for item in tree]))
vals.append(str(renamed.name))
vals.append(str(Path("/repo/newdir").exists()))
vals.append(str(Path("/repo/newdir").is_dir()))
vals.append(str(Path("/repo/docs/a.md").is_file()))
vals.append(str(touched.stat().st_size))
vals.append(str([item.name for item in sorted(Path("/repo/async/new").iterdir())]))
touched.unlink(missing_ok=True)
Path("/repo/async/new").rmdir()
vals.append(str(Path("/repo/async/new").exists()))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal("opened|[a.md, opened.md, out.md]|[a.md, opened.md, out.md, sub/b.md]|final.md|True|True|True|0|[file.txt]|False", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
from pathlib import Path
Path("/repo/missing.txt").unlink(missing_ok="yes")
""",
        "TypeError",
        "missing_ok")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/a.txt").touch(exist_ok=False)
""",
        "RuntimeError",
        "target already exists")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/newdir").mkdir(parents="yes")
""",
        "TypeError",
        "parents")]
    public void PathlibModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/a.txt", "x");

        var result = new LythonEngine().Run(source, host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
