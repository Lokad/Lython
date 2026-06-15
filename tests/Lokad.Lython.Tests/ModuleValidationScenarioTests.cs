using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ModuleValidationScenarioTests
{
    [Fact]
    public void ImportAlias_AndFromImport_SupportAllowlistedModules()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re as regex
from json import dumps
from fnmatch import fnmatch as matches
vals = []
vals.append(str(regex.search("a+", "caaab").span()))
vals.append(dumps({"ok": True}))
vals.append(str(matches("a.txt", "*.txt")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("(1, 4)|{\"ok\":true}|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CommaDelimitedImports_SupportMultipleModulesAndAliases()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json as js, re
__lython_file = open("/out.txt", "w")
__lython_file.write(js.dumps({"ok": True}) + "|" + str(re.search("a+", "caaab").span()))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("{\"ok\":true}|(1, 4)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GroupedFromImport_SupportsParenthesizedMemberLists()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from json import (
    dumps,
    loads,
)
__lython_file = open("/out.txt", "w")
__lython_file.write(dumps(loads("{\"ok\": true}")))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("{\"ok\":true}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DottedFromImport_SupportsBuiltinSubmodules()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from os.path import basename, join
__lython_file = open("/out.txt", "w")
__lython_file.write(join("/repo", "docs", basename("/repo/source/guide.md")))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("/repo/docs/guide.md", host.ReadText("/out.txt"));
    }

    [Fact]
    public void RegexAndJson_ModuleFunctionsAcceptKeywordArguments()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
import re
vals = []
vals.append(str(re.search(pattern = "a+", string = "caaab").span()))
vals.append(str(list(re.findall(pattern = "a+", string = "caaab a"))))
vals.append(str(list(re.findall(pattern = "(a)?b", string = "b ab b"))))
vals.append(str(list(re.findall(pattern = "(a)|(x)", string = "axa"))))
vals.append(re.sub(pattern = "a+", repl = "x", string = "caaab a"))
vals.append(json.dumps(obj = json.loads(s = "{\"ok\": true}")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        if (!result.Success)
        {
            var failure = result.Failure;
            var diagnostics = string.Join("||", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}:{diagnostic.Message}"));
            Assert.Fail($"{failure?.ExceptionType}|{failure?.Message}|{diagnostics}");
        }
        Assert.Null(result.Failure);
        Assert.Equal("(1, 4)|[aaa, a]|[, a, ]|[(a, ), (, x), (a, )]|cxb x|{\"ok\":true}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void LocalModules_CanBeImportedAndAreCached()
    {
        var host = new MockLythonHost();
        host.SeedFile(
            "/helper.py",
            """
count = 0
count += 1

def bump(value):
    return value + count
""");

        var result = new LythonEngine().Run(
            """
import helper
import helper as again
from helper import bump
__lython_file = open("/out.txt", "w")
__lython_file.write(str(helper.bump(2)) + "|" + str(again.bump(2)) + "|" + str(bump(2)))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("3|3|3", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("from re import missing\n", "ImportError", "Cannot import name 'missing' from 're'")]
    [InlineData("from csv import nope as writer\n", "ImportError", "Cannot import name 'nope' from 'csv'")]
    [InlineData("import missing\n", "ImportError", "No module named 'missing'")]
    [InlineData("from missing import value\n", "ImportError", "No module named 'missing'")]
    public void FromImport_UnknownMember_FailsWithImportError(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("import csv\ncsv.reader(1)\n", "compile", "iterable of strings")]
    [InlineData("import csv\ncsv.reader([1])\n", "compile", "iterable of strings")]
    [InlineData("import csv\ncsv.reader([\"a\"], delimiter = 1)\n", "compile", "csv delimiter must be a string")]
    [InlineData("import csv\ncsv.reader([\"a\"], delimiter = \"\")\n", "compile", "csv delimiter must be one character")]
    [InlineData("import csv\ncsv.reader([\"a\"], delimiter = \"\\n\")\n", "compile", "csv delimiter cannot be a newline")]
    [InlineData("import csv\ncsv.writer(delimiter = 1)\n", "compile", "csv delimiter must be a string")]
    [InlineData("import json\njson.dumps(1, 2)\n", "compile", "json.dumps(obj, *, ...) expects one object plus supported keyword options.")]
    [InlineData("import json\njson.loads()\n", "compile", "json.loads(s, *, ...) expects one string argument plus supported keyword options.")]
    [InlineData("import json\njson.dumps(value = 1)\n", "compile", "json.dumps(obj, *, ...) expects one object plus supported keyword options.")]
    [InlineData("import re\nre.search(\"a\", 1)\n", "compile", "expects string to be a string")]
    [InlineData("import re\nre.match(\"a\", 1)\n", "compile", "expects string to be a string")]
    [InlineData("import re\nre.fullmatch(\"a\", 1)\n", "compile", "expects string to be a string")]
    [InlineData("import re\nre.escape(1)\n", "compile", "re.escape(string) expects a string argument")]
    [InlineData("import re\nre.findall(1, \"alpha\")\n", "compile", "expects pattern to be a string or compiled regex pattern")]
    [InlineData("import re\nre.sub(\"a\", 1, \"alpha\")\n", "compile", "expects repl to be a string or callable")]
    [InlineData("import re\nre.search(text = \"alpha\", pattern = \"a\")\n", "compile", "re.search(pattern, string[, flags][, pos][, endpos]) expects two to five arguments.")]
    [InlineData("import re\nre.sub(pattern = \"a\", string = \"alpha\")\n", "compile", "re.sub(pattern, repl, string[, count][, flags][, pos][, endpos]) expects three to seven arguments.")]
    [InlineData("import fnmatch\nfnmatch.fnmatch(\"a.txt\", 1)\n", "compile", "expects two string arguments")]
    [InlineData("import fnmatch\nfnmatch.filter([\"a.txt\"], 1)\n", "compile", "expects an iterable and a string pattern")]
    [InlineData("import fnmatch\nfnmatch.filter(1, \"*.txt\")\n", "compile", "expects an iterable of strings")]
    [InlineData("import fnmatch\nfnmatch.filter([\"a.txt\", 1], \"*.txt\")\n", "compile", "expects an iterable of strings")]
    [InlineData("from pathlib import Path\nPath(\"/repo/out.txt\").write_text(1)\n", "compile", "expects a string plus optional keyword-compatible arguments")]
    public void ModuleContractFailure_ReportsExpectedException(string source, string exceptionType, string messageFragment)
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
    public void RegexFindAll_ReturnsAllMatches()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
vals = list(re.findall("a+", "caaab a"))
__lython_file = open("/out.txt", "w")
__lython_file.write(str(vals) + "|" + str(len(vals)))
__lython_file.close()
""",
            host);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"{result.Failure?.ExceptionType}|{result.Failure?.Message}|{string.Join("||", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}:{diagnostic.Message}"))}");
        }
        Assert.Null(result.Failure);
        Assert.Equal("[aaa, a]|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void RegexFindAll_UsesPythonCaptureShapes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
left = str(list(re.findall("(a)?b", "b ab b")))
mid = str(list(re.findall("(a)|(x)", "axa")))
right = str(list(re.compile("(a)|(x)").findall("axa")))
__lython_file = open("/out.txt", "w")
__lython_file.write(left + "|" + mid + "|" + right)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("[, a, ]|[(a, ), (, x), (a, )]|[(a, ), (, x), (a, )]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void RegexFullMatch_ReturnsNoneWhenTextHasExtraCharacters()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
__lython_file = open("/out.txt", "w")
__lython_file.write(str(re.fullmatch("abc", "abc\n") is None))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("import csv\nwriter = csv.writer()\nwriter.writerow()\n", "csv.writerow(row) expects one argument.")]
    [InlineData("import csv\nwriter = csv.writer()\nwriter.writerows()\n", "csv.writerows(rows) expects one argument.")]
    [InlineData("import csv\nwriter = csv.writer()\nwriter.getvalue(1)\n", "csv.getvalue() expects no arguments.")]
    [InlineData("import re\nm = re.search(\"a\", \"a\")\nm.start(1, 2)\n", "match.start(group=0) expects zero or one group identifier.")]
    [InlineData("import re\nm = re.search(\"a\", \"a\")\nm.end(1, 2)\n", "match.end(group=0) expects zero or one group identifier.")]
    [InlineData("import re\nm = re.search(\"a\", \"a\")\nm.span(1, 2)\n", "match.span(group=0) expects zero or one group identifier.")]
    public void ModuleMemberContractFailure_ReportsTypeError(string source, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal("TypeError", result.Failure.ExceptionType);
            Assert.Contains(message, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void JsonDumps_UnsupportedRuntimeType_FailsWithTypeError()
    {
        var result = new LythonEngine().Run(
            """
import json
import re
json.dumps(re.search("a", "a"))
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("Unsupported json.dumps value type", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegexSearchAndMatch_SupportZeroLengthMatches()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import re
vals = []
vals.append(str(re.search("x*", "axx").span()))
vals.append(str(re.match("a*", "xxx").span()))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("(0, 0)|(0, 0)", host.ReadText("/out.txt"));
    }
}
