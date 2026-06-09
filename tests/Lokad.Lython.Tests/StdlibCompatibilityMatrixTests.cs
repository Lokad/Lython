using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StdlibCompatibilityMatrixTests
{
    [Fact]
    public void CompiledRegexMethods_SupportKeywordShapedOrdinaryPythonCalls()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
pat = re.compile("a+", re.IGNORECASE)
parts = pat.split("xAaYaa", maxsplit=1)
text, count = pat.subn(repl="x", string="Aa aa", count=1)
write_text("/out.txt", str(parts) + "|" + text + "|" + str(count))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[x, Yaa]|x aa|1", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseParseArgs_SupportsExplicitArgsKeyword()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("--lang", required=True)
args = parser.parse_args(args=["--lang", "fr"])
write_text("/out.txt", args.lang)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("fr", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Argparse_DestOverrideAndPositionalArgsIterable_Work()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("--repo-root", dest="root_path", default=None)
args = parser.parse_args(["--repo-root", "/repo"])
write_text("/out.txt", str(args.root_path))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Argparse_PositionalNargsStarAndSuppressHelp_Work()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("--verbose", action="store_true", help=argparse.SUPPRESS)
parser.add_argument("filenames", nargs="*", help="Filenames to fix")
args = parser.parse_args(["--verbose", "a.py", "b.py"])
write_text("/out.txt", str(args.verbose) + "|" + str(args.filenames))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|[a.py, b.py]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Argparse_PositionalNargsPlus_WorksForDonorShapedFileLists()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("filenames", nargs="+", help="Files to sort")
args = parser.parse_args(["a.txt", "b.txt"])
write_text("/out.txt", str(args.filenames))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[a.txt, b.txt]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Argparse_MutuallyExclusiveStoreConstGroup_Works()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("filenames", nargs="*")
mutex = parser.add_mutually_exclusive_group()
mutex.add_argument("--pytest", dest="pattern", action="store_const", const=".*_test\\.py", default=".*_test\\.py")
mutex.add_argument("--django", dest="pattern", action="store_const", const="test.*\\.py")
args = parser.parse_args(["--django", "a.py", "b.py"])
write_text("/out.txt", args.pattern + "|" + str(args.filenames))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("test.*\\.py|[a.py, b.py]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Argparse_MutuallyExclusiveGroup_SupportsMultipleOptionAliases()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
mutex = parser.add_mutually_exclusive_group(required=False)
mutex.add_argument("--pytest", dest="pattern", action="store_const", const=".*_test\\.py", default=".*_test\\.py")
mutex.add_argument("--django", "--unittest", dest="pattern", action="store_const", const="test.*\\.py")
args = parser.parse_args(["--unittest"])
write_text("/out.txt", args.pattern)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("test.*\\.py", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Argparse_StoreFalseAction_WorksForDonorShapedFlags()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
mutex = parser.add_mutually_exclusive_group(required=False)
mutex.add_argument("--allow-dict-kwargs", action="store_true")
mutex.add_argument("--no-allow-dict-kwargs", dest="allow_dict_kwargs", action="store_false")
args = parser.parse_args(["--no-allow-dict-kwargs"])
write_text("/out.txt", str(args.allow_dict_kwargs))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonCsvFnmatchAndSys_SupportRepresentativeOrdinaryMatrixCalls()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv
import fnmatch
import json
import sys

writer = csv.writer(delimiter=";")
writer.writerow(["name", "score"])
writer.writerow(["alpha", "2"])
reader = csv.reader(writer.getvalue().splitlines(), delimiter=";")
rows = list(reader)
filtered = fnmatch.filter(["a.txt", "b.md", "c.txt"], "*.txt")
payload = json.loads("{\"ok\": true, \"count\": 2}")
vals = []
vals.append(str(sys.argv))
vals.append(str(rows))
vals.append(str(filtered))
vals.append(str(payload["count"]))
write_text("/out.txt", "|".join(vals))
""",
            host,
            new LythonRunOptions
            {
                Args = ["--demo", "x"]
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[--demo, x]|[[name, score], [alpha, 2]]|[a.txt, c.txt]|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Regex_FlagSemanticsAndModuleSplitMaxsplit_Work()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
vals = []
vals.append(str(re.search("^a", "x\na", re.MULTILINE).start()))
vals.append(str(re.fullmatch("a.b", "a\nb", re.DOTALL).span()))
vals.append(str(re.split(":+", ":a::b:", 1)))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2|(0, 3)|[, a::b:]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Regex_ShortVerboseFlagAlias_WorksForDonorShapedPatterns()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
m = re.search(r"\d+ (\.\d+)+", "release 1.2.3", re.X | re.I)
write_text("/out.txt", m.group(0))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1.2.3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibWithSuffixAndRegexUnicodeFlag_Work()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
import re

target = Path(__file__).with_suffix(".svg")
ok = re.fullmatch("a.b", "a\nb", re.UNICODE | re.DOTALL) is not None
write_text("/out.txt", target.as_posix() + "|" + str(ok))
""",
            host,
            new LythonRunOptions
            {
                SourcePath = "/repo/docs/img/plugin-events.py"
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo/docs/img/plugin-events.svg|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibRelativeTo_AcceptsStringParentArgument()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
value = Path("/repo/docs/page.md").relative_to("/repo").as_posix()
write_text("/out.txt", value)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("docs/page.md", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibPath_AcceptsMultiplePathSegments()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
target = Path("/repo", "docs", "guide", "intro.md")
write_text("/out.txt", target.parent.as_posix() + "|" + target.name)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo/docs/guide|intro.md", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibRenameAndUnlink_AreHostMediated()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/site/aaa", "alpha");
        host.SeedFile("/repo/site/foo.site", "beta");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
renamed = Path("/repo/site/aaa").rename(Path("/repo/site/bbb"))
Path("/repo/site/foo.site").unlink()
vals = []
vals.append(renamed.as_posix())
vals.append(str(Path("/repo/site/aaa").exists()))
vals.append(str(Path("/repo/site/bbb").exists()))
vals.append(str(Path("/repo/site/foo.site").exists()))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/repo/site/bbb|False|True|False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibMkdir_IsHostMediated()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
Path("/repo/subdir").mkdir()
write_text("/out.txt", str(Path("/repo/subdir").is_dir()))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibOpenAndGlob_WorkForDonorShapedTextCases()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/docs/a.po", "# fr translations \n");
        host.SeedFile("/repo/docs/b.txt", "skip");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

items = sorted(Path("/repo/docs").glob("*.po"))
with items[0].open(encoding="utf-8") as f:
    line = f.readline().rstrip()
write_text("/out.txt", items[0].name + "|" + line)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("a.po|# fr translations", host.ReadText("/out.txt"));
    }

    [Fact]
    public void BuiltinOpen_AcceptsUtf8EncodingAndEmptyNewlineContracts()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/in.txt", "alpha\nbeta\n");

        var result = new LythonEngine().Run(
            """
with open("/repo/in.txt", encoding="UTF-8", newline="") as reader:
    first = reader.readline().rstrip()

with open("/repo/out.txt", "w", encoding="UTF-8", newline="") as writer:
    writer.write(first)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha", host.ReadText("/repo/out.txt"));
    }

    [Fact]
    public void BuiltinOpen_AcceptsUtf8SigAndStrictErrorsForDonorShapedTextFiles()
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/in.txt", "\uFEFFalpha\n");

        var result = new LythonEngine().Run(
            """
with open("/repo/in.txt", encoding="utf-8-sig", errors="strict") as reader:
    first = reader.readline().rstrip()

with open("/repo/out.txt", "w", encoding="utf-8-sig", errors="strict") as writer:
    writer.write(first)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("\uFEFFalpha", host.ReadText("/repo/out.txt"));
    }

    [Fact]
    public void PathlibParts_ExposePurePathSegments()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
value = Path("/repo/docs/page.md").parent.parts
write_text("/out.txt", str(value))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("(/, repo, docs)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PathlibPurePathHelpers_WorkForOrdinaryPathShaping()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

base = Path("/repo/docs/page.md")
joined = base.parent.joinpath("nested", "guide.txt")
renamed = joined.with_name("intro.txt")
vals = []
vals.append(str(base.is_absolute()))
vals.append(str(joined.match("*.txt")))
vals.append(renamed.as_posix())
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|/repo/docs/nested/intro.txt", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import re
re.compile("a", "bad")
""",
        "compile",
        "expects flags to be an integer or None")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("--flag", action="explode")
""",
        "compile",
        "only supports 'store', 'store_true', 'store_false'")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("--count", type="int")
""",
        "compile",
        "expects a callable or None")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("--count", nargs=0)
""",
        "compile",
        "expects '?', '*', '+', or a positive integer")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("filenames", nargs="+")
parser.parse_args([])
""",
        "SystemExit",
        "the following arguments are required: filenames")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.parse_args(values=["--lang", "fr"])
""",
        "compile",
        "argparse.ArgumentParser.parse_args([args][, namespace]) expects")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.parse_args("abc")
""",
        "compile",
        "expects an iterable of strings, not a single string")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.parse_args(1)
""",
        "compile",
        "expects an iterable of strings.")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.parse_args(["--lang", 1])
""",
        "compile",
        "expects an iterable of strings.")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/out.bin").write_bytes(b"abc")
""",
        "compile",
        "UTF-8 text-shaped only")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/in.txt").open("rb")
""",
        "compile",
        "binary modes like 'rb' and 'wb' are unsupported")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/in.txt").open(1)
""",
        "compile",
        "expects mode to be a string")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/in.txt").open("x")
""",
        "compile",
        "only supports modes 'r', 'w', and 'a'")]
    [InlineData(
        """
import argparse
parser = argparse.ArgumentParser()
parser.add_argument("--lang", choices=1)
""",
        "compile",
        "Object is not iterable.")]
    public void NearMissStdlibSurfaces_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (exceptionType == "compile")
        {
            Assert.Null(result.Failure);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.NotNull(result.Failure);
            Assert.Equal(exceptionType, result.Failure!.ExceptionType);
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProvableBuiltinOpenBinaryMode_FailsAtCompileTime()
    {
        var result = new LythonEngine().Run(
            """
with open("/repo/in.txt", "rb") as handle:
    handle.read()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("binary modes like 'rb' and 'wb' are unsupported", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        """
from pathlib import Path
Path("/repo/input.txt").read_text(encoding="latin-1")
""",
        "compile",
        "only supports encoding='utf-8'")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/input.txt").read_text(errors="ignore")
""",
        "compile",
        "only supports errors='strict'")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/output.txt").write_text("alpha", encoding="utf-8", errors="ignore")
""",
        "compile",
        "only supports errors='strict'")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/output.txt").write_text("alpha", encoding="utf-8", newline="\r\n")
""",
        "compile",
        "only supports newline=''")]
    [InlineData(
        """
from pathlib import Path
Path("/repo/input.txt").open(encoding="utf-8", newline="\r\n")
""",
        "compile",
        "only supports newline=''")]
    public void PathlibEncodingAndNewlineContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var host = new MockLythonHost("/repo");
        host.SeedFile("/repo/input.txt", "alpha");

        var result = new LythonEngine().Run(source, host);

        Assert.False(result.Success);
        if (exceptionType == "compile")
        {
            Assert.Null(result.Failure);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.NotNull(result.Failure);
            Assert.Equal(exceptionType, result.Failure!.ExceptionType);
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PathlibRelativeToOutsideRoot_FailsPrecisely()
    {
        var result = new LythonEngine().Run(
            """
from pathlib import Path
Path("/repo/a.txt").relative_to("/other")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
        Assert.Contains("is not under", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Argparse_MutuallyExclusiveGroup_RejectsConflictingOptions()
    {
        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
mutex = parser.add_mutually_exclusive_group()
mutex.add_argument("--pytest", dest="pattern", action="store_const", const="a")
mutex.add_argument("--django", dest="pattern", action="store_const", const="b")
parser.parse_args(["--pytest", "--django"])
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure!.ExceptionType);
        Assert.Contains("mutually exclusive arguments must not be used together", result.Failure.Message, StringComparison.Ordinal);
    }
}
