using Lokad.Lython.Tests.Harness;
using Xunit.Sdk;

namespace Lokad.Lython.Tests;

public sealed class ArgparseModuleFunctionTests
{
    [Fact]
    public void ArgparseModule_OptionTerminatorAndNegativePositionals_AreDisambiguated()
    {
        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser(add_help=False)
parser.add_argument("first")
parser.add_argument("second")
args = parser.parse_args(["--", "-x", "-2.5"])
print(args.first, args.second)

numeric = argparse.ArgumentParser(add_help=False)
numeric.add_argument("value", type=int)
print(numeric.parse_args(["-2"]).value)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("-x -2.5\n-2\n", result.StandardOutput);
    }

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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
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
        Assert.Equal("fr|['a', 'b']|True|5", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write(str(args.allow_dict_kwargs))
__lython_file.close()
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
__lython_file = open("/out.txt", "w")
__lython_file.write(args.pattern)
__lython_file.close()
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
__lython_file = open("/out.txt", "w")
__lython_file.write(str(args.files))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("['a.txt', 'b.txt']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseModule_ParseKnownNamespaceAndNargsFinePrint_ArePythonShaped()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser(prog="tool", description="demo")
parser.add_argument("-v", "--verbose", action="count", default=0)
parser.add_argument("-o", "--output", default=argparse.SUPPRESS)
parser.add_argument("--limit", nargs="?", const="auto", default="none")
parser.add_argument("--pair", nargs=2, action="append", metavar="PAIR")
parser.add_argument("source", nargs="?")
parser.set_defaults(mode="scan")
seed = argparse.Namespace(existing="keep")
args, rest = parser.parse_known_args(["-vv", "--limit", "--pair", "a", "b", "input.txt", "--extra"], namespace=seed)
missing = "present"
try:
    args.output
except AttributeError:
    missing = "absent"
vals = []
vals.append(str(args.verbose))
vals.append(args.limit)
vals.append(str(args.pair))
vals.append(args.source)
vals.append(args.mode)
vals.append(args.existing)
vals.append(str(rest))
vals.append(str(parser.get_default("mode")))
vals.append(missing)
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("2|auto|[['a', 'b']]|input.txt|scan|keep|['--extra']|scan|absent", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseModule_OptionAliasesInlineValuesAndFormatterSurface_Work()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser(prog="tool", formatter_class=argparse.ArgumentDefaultsHelpFormatter, epilog="done")
parser.add_argument("-o", "--output", default="out.txt", help="destination")
parser.add_argument("--lang", choices=("fr", "de"), default="fr")
parser.add_argument("--quiet", action="store_true", help=argparse.SUPPRESS)
args = parser.parse_args(["-oreport.txt", "--lang=de"])
help_text = parser.format_help()
vals = []
vals.append(args.output)
vals.append(args.lang)
vals.append(str(args.quiet))
vals.append(str("destination" in help_text))
vals.append(str("--quiet" in help_text))
vals.append(str(argparse.OPTIONAL + argparse.ZERO_OR_MORE + argparse.ONE_OR_MORE))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("report.txt|de|False|True|False|?*+", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseModule_FileType_UsesHostMediatedTextOpen()
    {
        var host = new MockLythonHost();
        host.WriteText("/in.txt", "hello");

        var result = new LythonEngine().Run(
            """
import argparse

reader = argparse.FileType("r")
handle = reader("/in.txt")
__lython_file = open("/out.txt", "w")
__lython_file.write(handle.read())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("hello", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseModule_ExitOnErrorFalse_RaisesArgumentError()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser(exit_on_error=False)
parser.add_argument("--lang", choices=("fr", "de"))
try:
    parser.parse_args(["--lang", "es"])
except argparse.ArgumentError as ex:
    __lython_file = open("/out.txt", "w")
    __lython_file.write(ex.type + ":" + ex.message)
    __lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Contains("ArgumentError:argument --lang: invalid choice", host.ReadText("/out.txt"), StringComparison.Ordinal);
    }

    [Fact]
    public void ArgparseModule_UnsupportedAdvancedFeatures_AreExplicit()
    {
        var result = new LythonEngine().Run(
            """
import argparse

argparse.ArgumentParser(fromfile_prefix_chars="@")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("NotImplementedError", result.Failure!.ExceptionType);
        Assert.Contains("fromfile_prefix_chars", result.Failure.Message, StringComparison.Ordinal);
    }
}
