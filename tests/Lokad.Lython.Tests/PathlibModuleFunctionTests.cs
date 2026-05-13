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
vals.append(base.name)
vals.append(base.suffix)
vals.append(base.stem)
vals.append(base.parent.as_posix())
vals.append(str(base.parents))
vals.append(str(base.parts))
vals.append(str(base.is_absolute()))
vals.append(base.joinpath("nested", "guide.txt").as_posix())
vals.append(base.with_name("intro.md").as_posix())
vals.append(base.with_suffix(".txt").as_posix())
vals.append(base.relative_to("/repo").as_posix())
vals.append(str(base.match("*.md")))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("page.md|.md|page|/repo/docs|[/repo/docs, /repo, /]|(/, repo, docs, page.md)|True|/repo/docs/page.md/nested/guide.txt|/repo/docs/intro.md|/repo/docs/page.txt|docs/page.md|True", host.ReadText("/out.txt"));
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
dst.write_text(src.read_text(encoding="utf-8"), encoding="utf-8", newline="")
items = sorted(Path("/repo/docs").glob("*.md"))
tree = sorted(Path("/repo/docs").rglob("*.md"))
renamed = Path("/repo/docs/out.md").rename(Path("/repo/docs/final.md"))
Path("/repo/docs/final.md").unlink()
Path("/repo/newdir").mkdir()
with src.open(encoding="utf-8") as f:
    first = f.readline().rstrip()
vals = []
vals.append(first)
vals.append(str([item.name for item in items]))
vals.append(str([item.relative_to(Path("/repo/docs")).as_posix() for item in tree]))
vals.append(str(renamed.name))
vals.append(str(Path("/repo/newdir").is_dir()))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha|[a.md, out.md]|[a.md, out.md, sub/b.md]|final.md|True", host.ReadText("/out.txt"));
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
dst.write_text(src.read_text(encoding="utf-8"), encoding="utf-8", newline="")
with Path("/repo/docs/opened.md").open("w", encoding="utf-8") as f:
    f.write("opened")
with Path("/repo/docs/opened.md").open(encoding="utf-8") as f:
    opened = f.read()
items = sorted(Path("/repo/docs").glob("*.md"))
tree = sorted(Path("/repo/docs").rglob("*.md"))
renamed = Path("/repo/docs/out.md").rename(Path("/repo/docs/final.md"))
Path("/repo/docs/final.md").unlink()
Path("/repo/newdir").mkdir()
vals = []
vals.append(opened)
vals.append(str([item.name for item in items]))
vals.append(str([item.relative_to(Path("/repo/docs")).as_posix() for item in tree]))
vals.append(str(renamed.name))
vals.append(str(Path("/repo/newdir").exists()))
vals.append(str(Path("/repo/newdir").is_dir()))
vals.append(str(Path("/repo/docs/a.md").is_file()))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal("opened|[a.md, opened.md, out.md]|[a.md, opened.md, out.md, sub/b.md]|final.md|True|True|True", host.ReadText("/out.txt"));
    }
}
