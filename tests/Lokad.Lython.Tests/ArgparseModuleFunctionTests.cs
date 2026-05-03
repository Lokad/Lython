using Lokad.Lython.Tests.Harness;
using Xunit.Sdk;

namespace Lokad.Lython.Tests;

public sealed class ArgparseModuleFunctionTests
{
    [Fact]
    public void ArgparseModule_RequiredChoicesAppendStoreTrueAndTypedValues_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser(description="demo")
parser.add_argument("--lang", required=True, choices=("fr", "de"))
parser.add_argument("--include", action="append", default=[])
parser.add_argument("--apply", action="store_true")
parser.add_argument("--max-rounds", type=int, default=12)
args = parser.parse_args()
vals = []
vals.append(args.lang)
vals.append(str(args.include))
vals.append(str(args.apply))
vals.append(str(args.max_rounds))
write_text("/out.txt", "|".join(vals))
""",
            host,
            new LythonRunOptions
            {
                Args = ["--lang", "fr", "--include", "a", "--include", "b", "--apply", "--max-rounds", "5"]
            });

        if (!result.Success)
        {
            throw new XunitException(string.Join(
                " | ",
                result.Diagnostics.Select(d => d.Message).Append($"{result.Failure?.ExceptionType}:{result.Failure?.Message}")));
        }
        Assert.Equal("fr|[a, b]|True|5", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseModule_StoreTrueStoreFalseAndAppend_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser()
mutex = parser.add_mutually_exclusive_group(required=False)
mutex.add_argument("--allow-dict-kwargs", action="store_true")
mutex.add_argument("--no-allow-dict-kwargs", dest="allow_dict_kwargs", action="store_false")
args = parser.parse_args(args=["--no-allow-dict-kwargs"])
write_text("/out.txt", str(args.allow_dict_kwargs))
""",
            host);

        if (!result.Success)
        {
            throw new XunitException(string.Join(
                " | ",
                result.Diagnostics.Select(d => d.Message).Append($"{result.Failure?.ExceptionType}:{result.Failure?.Message}")));
        }
        Assert.Equal("False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseModule_MutuallyExclusiveAndStoreConst_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser()
group = parser.add_mutually_exclusive_group(required=False)
group.add_argument("--pytest", dest="pattern", action="store_const", const=".*_test\\.py", default=".*_test\\.py")
group.add_argument("--django", "--unittest", dest="pattern", action="store_const", const="test.*\\.py")
args = parser.parse_args(["--unittest"])
write_text("/out.txt", args.pattern)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("test.*\\.py", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseModule_PositionalNargsShapes_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser()
parser.add_argument("files", nargs="+")
args = parser.parse_args(["a.txt", "b.txt"])
write_text("/out.txt", str(args.files))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[a.txt, b.txt]", host.ReadText("/out.txt"));
    }
}
