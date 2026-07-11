using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StaticErrorDiagnosticsTests
{
    [Fact]
    public void OpenLiteralContracts_ReportCompileErrors()
    {
        var compiled = new LythonEngine().Compile(
            """
open("/repo/in.txt", "rb")
open("/repo/in.txt", encoding="latin-1")
open("/repo/in.txt", newline="bad")
open("/repo/in.txt", errors="surrogateescape")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3001");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3003");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3004");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3005");
    }

    [Fact]
    public void OsWalkAndSubprocessLiteralContracts_ReportCompileErrors()
    {
        var compiled = new LythonEngine().Compile(
            """
import os
import subprocess

list(os.walk("/repo", topdown="yes", onerror="boom", followlinks=1))
subprocess.run("rg")
subprocess.run([])
subprocess.run(["rg", 1], timeout="fast", check="yes", capture_output="sure")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3010");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3011");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3012");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3020");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3027");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3021");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3024");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3025");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3026");
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenStaticErrorsExist()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
open("/repo/out.txt", "rb")
__lython_file = open("/repo/created.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3001");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void LiteralUnpackingAndLoopShapeErrors_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
a, b = [1]
x, *rest, y = [1]
for item in 1:
    pass
for left, right in [1, 2]:
    pass
values = [x for x in None]
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3030");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3031");
    }

    [Fact]
    public void LiteralBuiltinMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
list(1)
tuple(None)
set(False)
len(1)
dict(1)
dict([("a", 1, 2)])
any(1)
all(None)
min(False)
max(1)
sum(1)
sorted(1)
sorted([1], key=1, reverse=1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3031");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3032");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3033");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3034");
    }

    [Fact]
    public void PropertyDataclassesAndCommonPathLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import os
from dataclasses import asdict, astuple, field

property(1, None, "x")
field(default=1, default_factory=list)
field(default_factory=1)
asdict(box, dict_factory=1)
astuple(box, tuple_factory=1)
os.path.commonpath("/repo/docs")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3036");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3037");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3038");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3039");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3042");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3043");
    }

    [Fact]
    public void RegexUnsupportedLocaleAndNamedEntityPatterns_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import re

PATTERN = r"\N{LATIN SMALL LETTER A}"
FLAGS = re.I | re.LOCALE

re.compile(PATTERN)
re.search(PATTERN, "a")
re.compile("a", re.LOCALE)
re.search("a", "a", FLAGS)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3044");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3045");
    }

    [Fact]
    public void ByteTextBoundaryMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path
import sys

__lython_file = open("/repo/out.txt", "w")
__lython_file.write(b"abc")
__lython_file.close()
__lython_file = open("/repo/out.txt", "a")
__lython_file.write(b"abc")
__lython_file.close()
Path("/repo/out.txt").write_text(b"abc")
sys.stdout.write(b"abc")
Path("/repo/out.bin").write_bytes(b"abc")
Path("/repo/in.bin").read_bytes()
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3046");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3047");
    }

    [Fact]
    public void PathReadTextDefaultDictAndArgparseTypeLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path
from collections import defaultdict
import argparse

Path("/repo/input.txt").read_text(encoding="latin-1")
defaultdict(1)
parser = argparse.ArgumentParser()
parser.add_argument("--count", type="int")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3049");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3049");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3050");
    }

    [Fact]
    public void ArgparseActionNargsAndPathWriteTextLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path
import argparse

parser = argparse.ArgumentParser()
parser.add_argument("--flag", action="explode")
parser.add_argument("--count", nargs=0)
Path("/repo/output.txt").write_text("alpha", encoding="latin-1", newline="bad")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3051");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3052");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3053");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3054");
    }

    [Fact]
    public void ArgparseStringContractsAndPathOpenLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path
import argparse

parser = argparse.ArgumentParser()
parser.add_argument(1, dest=2, action=3, help=4, choices=1)
parser.add_argument("   ")
Path("/repo/input.txt").open(1)
Path("/repo/input.txt").open("x")
Path("/repo/input.txt").open("rb", -1, "latin-1")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3055");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3056");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3057");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3058");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3059");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3031");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3060");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3061");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3062");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3063");
    }

    [Fact]
    public void PathAliasTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

path = Path("/repo/input.txt")
same_path = path

same_path.open("rb")
same_path.read_text(encoding="latin-1")
same_path.write_text(b"abc")
same_path.write_bytes(b"abc")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3061");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3049");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3046");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3047");
    }

    [Fact]
    public void PathReadTextStringTypeFlow_ReportStringStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
again = text.strip()

text.find(1)
text.split(1)
text.strip(1)
again.replace("a", 1)
again.startswith("a", start="bad")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3091");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3093");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3094");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3076");
    }

    [Fact]
    public void StringListTypeFlow_ReportPathWriteTextStaticErrorAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
lines = text.splitlines()
Path("/repo/output.txt").write_text(lines)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
    }

    [Fact]
    public void AbstractNonCallableValues_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
lines = text.splitlines()
path = Path("/repo/input.txt")

text()
lines()
path()
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(3, compiled.Diagnostics.Count(d => d.Code == "LA3107"));
    }

    [Fact]
    public void TextFileHandleTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

with Path("/repo/out.txt").open("w") as writer:
    writer.read()
    writer.write(1)
    writer.writelines([1, 2])

with open("/repo/in.txt", mode="r") as reader:
    reader.write("x")
    text = reader.readline()
    text.find(1)
    lines = reader.readlines()
    Path("/repo/out.txt").write_text(lines)

handle = open("/repo/out.txt", "w")
handle()
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3109");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3111");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3112");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3110");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3107");
    }

    [Fact]
    public void AbstractStateMergesBranchStringTypeFlow_ReportStaticErrorAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

if flag:
    text = Path("/repo/a.txt").read_text()
else:
    text = str(1)

text.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void AbstractStateWidensDisagreeingBranches_DoesNotReportSpeculativeTypeError()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

if flag:
    value = Path("/repo/a.txt").read_text()
else:
    value = 1

value.find(1)
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void WithBodyTypeFlow_PropagatesNonHandleAssignmentsAfterBlock()
    {
        var compiled = new LythonEngine().Compile(
            """
with open("/repo/in.txt", "r") as reader:
    text = reader.readline()

text.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void SimpleFunctionSummaryTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

def load_text():
    text = Path("/repo/input.txt").read_text()
    return text

def load_lines():
    with open("/repo/input.txt", "r") as reader:
        return reader.readlines()

text = load_text()
lines = load_lines()

text.find(1)
Path("/repo/output.txt").write_text(lines)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
    }

    [Fact]
    public void ParameterizedFunctionSummaryTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

def file_name(path):
    return path.name

def sibling(path, suffix=".bak"):
    renamed = path.with_suffix(suffix)
    return renamed.parent

def identity(value):
    return value

name = file_name(Path("/repo/input.txt"))
parent = sibling(path=Path("/repo/input.txt"))
text = identity(Path("/repo/input.txt").read_text())
lines = identity(text.splitlines())

name.find(1)
parent.not_a_path_member
text.startswith(1)
Path("/repo/output.txt").write_text(lines)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3113");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
    }

    [Fact]
    public void ParameterizedFunctionSummaryTypeFlow_WidensUnknownArguments()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

def identity(value):
    return value

if flag:
    value = Path("/repo/input.txt").read_text()
else:
    value = 1

result = identity(value)
result.find(1)
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void ConditionalFunctionSummaryTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

def pick_text(flag):
    if flag:
        return Path("/repo/a.txt").read_text()
    else:
        return str(1)

def pick_name(flag, path):
    if flag:
        return path.name
    else:
        value = path.suffix
    return value

text = pick_text(flag)
name = pick_name(flag, Path("/repo/input.txt"))

text.find(1)
name.startswith(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
    }

    [Fact]
    public void ConditionalFunctionSummaryTypeFlow_WidensDisagreeingReturns()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

def pick(flag):
    if flag:
        return Path("/repo/a.txt").read_text()
    else:
        return 1

value = pick(flag)
value.find(1)
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void KnownSealedRuntimeMemberSurfaces_ReportMissingMembersAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

path = Path("/repo/input.txt")
path.not_a_path_member
path.not_a_path_call()
path.write_bytes(b"abc")

with open("/repo/input.txt", "r") as reader:
    reader.seek(0)
    reader.not_a_file_member
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(3, compiled.Diagnostics.Count(d => d.Code == "LA3113"));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3047");
    }

    [Fact]
    public void PathMemberValueTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

path = Path("/repo/input.txt")
name = path.name
suffix = path.with_suffix(".bak").suffix
parent = path.parent
resolved_parent = path.resolve().parent

name.find(1)
suffix.startswith(1)
parent.read_text(encoding="latin-1")
resolved_parent.not_a_path_member
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3049");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3113");
    }

    [Fact]
    public void StaticCallableContracts_ReportSealedMemberArityErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

path = Path("/repo/input.txt")
path.as_posix(1)
path.resolve(extra=1)
path.exists(1)
path.read_text("utf-8", "strict", "", "extra")
path.write_text()
path.with_suffix()
path.joinpath()

with open("/repo/input.txt", "r") as reader:
    reader.readline(1, 2)
    reader.write()
    reader.writelines()
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(7, compiled.Diagnostics.Count(d => d.Code == "LA3114"));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3108");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3111");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3112");
    }

    [Fact]
    public void StaticCallableContracts_ReportKeywordShapeErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

path = Path("/repo/input.txt")
path.read_text(unknown=1)
path.write_text(encoding="utf-8")
path.write_text("alpha", text="beta")
path.joinpath(other="child")

mapping = {"name": "alpha"}
mapping.get(default="fallback")
mapping.get("name", key="other")
mapping.get(missing="name")

"banana".replace(old="na")
"banana".replace("na", old="x", new="y")
"banana".startswith(prefix="ba", unknown=1)
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(4, compiled.Diagnostics.Count(d => d.Code == "LA3114"));
        Assert.Equal(3, compiled.Diagnostics.Count(d => d.Code == "LA3126"));
        Assert.Equal(3, compiled.Diagnostics.Count(d => d.Code == "LA3147"));
    }

    [Fact]
    public void ContainerMemberSurfaces_ReportMissingMembersAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
items = ["alpha"]
mapping = {"name": "alpha"}
seen = {"alpha"}
pair = ("alpha", 1)

items.not_a_list_member
items.copy().not_a_list_member
mapping.not_a_dict_member
mapping.copy().not_a_dict_member
seen.not_a_set_member
seen.copy().not_a_set_member
pair.not_a_tuple_member
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(7, compiled.Diagnostics.Count(d => d.Code == "LA3113"));
    }

    [Fact]
    public void ContainerCallableContracts_ReportArityErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
items = ["alpha"]
items.append()
items.extend()
items.pop(1, 2)
items.copy(1)
items.clear(1)

mapping = {"name": "alpha"}
mapping.get()
mapping.keys(1)
mapping.values(1)
mapping.items(1)
mapping.update()
mapping.pop()
mapping.copy(1)
mapping.clear(1)
mapping.setdefault()

seen = {"alpha"}
seen.add()
seen.discard()
seen.remove()
seen.copy(1)
seen.clear(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3121");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3122");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3123");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3124");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3125");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3126");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3127");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3128");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3129");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3130");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3131");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3132");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3133");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3134");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3135");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3136");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3137");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3138");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3139");
    }

    [Fact]
    public void ContainerMemberReturnFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

items = ["alpha", "beta"]
copy = items.copy()
popped = items.pop()

copy[0].find(1)
popped.startswith(1)

numbers = [1]
number = numbers.copy().pop()
Path("/repo/out.txt").write_text(number)

mapping = {"name": "alpha"}
mapping_copy = mapping.copy()
mapping_copy.not_a_dict_member

seen = {"alpha"}
seen_copy = seen.copy()
seen_copy.not_a_set_member
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
        Assert.Equal(2, compiled.Diagnostics.Count(d => d.Code == "LA3113"));
    }

    [Fact]
    public void DictionaryMemberReturnFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

mapping = {"name": "alpha", "count": 1}
name = mapping.get("name")
name.find(1)

count = mapping.get("count")
Path("/repo/out.txt").write_text(count)

missing = mapping.get("missing")
missing()

fallback = mapping.get("missing", "fallback")
fallback.find(1)

popped = mapping.pop("name")
popped.startswith(1)

inserted = {"count": 1}.setdefault("name", "alpha")
inserted.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3075") >= 3);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3107");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
    }

    [Fact]
    public void DictionaryMemberReturnFlow_AcceptsKeywordBoundKeys()
    {
        var compiled = new LythonEngine().Compile(
            """
mapping = {"name": "alpha"}
name = mapping.get(key="name")
name.find(1)

fallback = mapping.get(key="missing", default="fallback")
fallback.find(1)

popped = mapping.pop(key="name")
popped.startswith(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(2, compiled.Diagnostics.Count(d => d.Code == "LA3075"));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
    }

    [Fact]
    public void DictionaryMemberReturnFlow_WidensUnknownKeys()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

mapping = {"name": "alpha", "count": 1}
value = mapping.get(key, "fallback")
Path("/repo/out.txt").write_text(value)
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void ContainerMemberArgumentTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
items = []
items.extend(1)

writer_items = []
with open("/repo/out.txt", "w") as writer:
    writer_items.extend(writer)

mapping = {}
mapping.update([])
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3140");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3109");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3104");
    }

    [Fact]
    public void ContainerMutationsInvalidateReceiverFacts()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

items = [1]
items.clear()
items.append("alpha")
Path("/repo/out.txt").write_text(items[0])

mapping = {"name": 1}
mapping.update({"name": "alpha"})
Path("/repo/out.txt").write_text(mapping["name"])
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void ContainerMutationsPreserveReturnFlowForAssignedResults()
    {
        var compiled = new LythonEngine().Compile(
            """
items = ["alpha"]
value = items.pop()
value.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void LoopTargetTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

for line in Path("/repo/input.txt").read_text().splitlines():
    line.find(1)

with open("/repo/input.txt", "r") as reader:
    for row in reader:
        row.startswith(1)

for key in {"name": 1}:
    key.index(1)

for name, value in [("alpha", 1)]:
    name.count(1)
    Path("/repo/output.txt").write_text(value)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3075") >= 3);
    }

    [Fact]
    public void LoopTargetTypeFlow_ReportTextFileWrongModeAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
with open("/repo/output.txt", "w") as writer:
    for line in writer:
        line.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3109");
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void SubscriptTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

items = ["alpha", "beta"]
first = items[0]
first.find(1)

number = [1][0]
Path("/repo/out.txt").write_text(number)

byte_value = b"abc"[0]
Path("/repo/out.txt").write_text(byte_value)

path = Path("/repo/input.txt")
path[0]

text = "abc"
text["x"]
items[99]
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 2);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3115");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3116");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3117");
    }

    [Fact]
    public void SliceTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = "alpha"
piece = text[1:3]
piece.find(1)

parts = ["alpha", "beta"][0:1]
first = parts[0]
first.startswith(1)

Path("/repo/input.txt")[0:1]
text[1:"x"]
text[::0]
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3118");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3119");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3120");
    }

    [Fact]
    public void SubscriptTypeFlow_WidensHeterogeneousIndexResults()
    {
        var compiled = new LythonEngine().Compile(
            """
items = ["alpha", 1]
value = items[index]
value.find(1)
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void ListComprehensionTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
lines = [line.strip() for line in text.splitlines()]
Path("/repo/output.txt").write_text(lines)

bad = [line.find(1) for line in text.splitlines()]

with open("/repo/input.txt", "r") as reader:
    rows = [row for row in reader]
    Path("/repo/output.txt").write_text(rows)

with open("/repo/output.txt", "w") as writer:
    leaked = [line.find(1) for line in writer]
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 2);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3109");
    }

    [Fact]
    public void SetComprehensionTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
items = {line.strip() for line in text.splitlines()}
Path("/repo/output.txt").write_text(items)

bad = {line.find(1) for line in text.splitlines()}

with open("/repo/output.txt", "w") as writer:
    leaked = {line.find(1) for line in writer}
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3109");
    }

    [Fact]
    public void ConditionalExpressionTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text() if flag else str(1)
text.find(1)

items = ["alpha"] if flag else ["beta"]
item = items[0]
item.startswith(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
    }

    [Fact]
    public void ConditionalExpressionTypeFlow_WidensDisagreeingArms()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

value = Path("/repo/input.txt").read_text() if flag else 1
value.find(1)
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void LambdaExpressionAnalysis_DoesNotLeakOuterBindingsIntoParameters()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

value = Path("/repo/input.txt").read_text()
callback = lambda value: value.find(1)
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void UnpackingAssignmentTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

first, second = ["alpha", Path("/repo/input.txt").read_text()]
first.find(1)
second.startswith(1)

left, middle, right = ("alpha", 1, b"abc")
left.count(1)
Path("/repo/out.txt").write_text(middle)
Path("/repo/out.txt").write_text(right)

head, *rest, tail = ["alpha", "beta", "gamma"]
head.index(1)
tail.endswith(1)
Path("/repo/out.txt").write_text(rest)

char, other = "xy"
char.rfind(1)

byte, other_byte = b"xy"
Path("/repo/out.txt").write_text(byte)

only, *empty = ["alpha"]
empty[0]
""");

        Assert.False(compiled.IsValid);
        var diagnostics = string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message));
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3075") >= 3, diagnostics);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 3, diagnostics);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3046");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3117");
    }

    [Fact]
    public void AbstractNonIterableValues_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

for item in Path("/repo/input.txt"):
    item.find(1)

items = [item for item in Path("/repo/input.txt")]

first, second = Path("/repo/input.txt")

for name, value in [Path("/repo/input.txt")]:
    name.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3031") >= 4);
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void BinaryExpressionTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text() + str(1)
text.find(1)

lines = ["header"] + text.splitlines()
Path("/repo/out.txt").write_text(lines)

flag = text == "x"
Path("/repo/out.txt").write_text(flag)

fallback = Path("/repo/input.txt").read_text() or str(1)
fallback.startswith(1)

numbers = [1] + [2]
Path("/repo/out.txt").write_text(numbers[0])

data = bytes("abc")
Path("/repo/out.txt").write_text(data[0])

b"a" + b"b"
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3141");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 4);
    }

    [Fact]
    public void BinaryExpressionTypeFlow_WidensShortCircuitDisagreement()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

value = Path("/repo/input.txt").read_text() or 1
value.find(1)
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void SequenceConstructorTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
chars = list(text)
chars[0].find(1)

lines = list(text.splitlines())
Path("/repo/out.txt").write_text(lines)

pair = tuple(["alpha", text])
pair[0].startswith(1)
pair[1].count(1)

with open("/repo/input.txt", "r") as reader:
    rows = list(reader)
    Path("/repo/out.txt").write_text(rows)

with open("/repo/out.txt", "w") as writer:
    bad = list(writer)

bad_path_items = list(Path("/repo/input.txt"))
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3075") >= 2);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 2);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3109");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3031");
    }

    [Fact]
    public void LenTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
count = len(text)
Path("/repo/out.txt").write_text(count)

empty_count = len(list())
Path("/repo/out.txt").write_text(empty_count)

len(Path("/repo/input.txt"))

with open("/repo/input.txt", "r") as reader:
    len(reader)
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 2);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3032") >= 2);
    }

    [Fact]
    public void ScalarConstructorTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

count = int(Path("/repo/input.txt").read_text())
Path("/repo/out.txt").write_text(count)

ratio = float("1.5")
Path("/repo/out.txt").write_text(ratio)

flag = bool(count)
Path("/repo/out.txt").write_text(flag)

data = bytes("abc")
Path("/repo/out.txt").write_text(data)
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 4);
    }

    [Fact]
    public void SortedTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
lines = sorted(text.splitlines())
Path("/repo/out.txt").write_text(lines)
lines[0].find(1)

numbers = sorted([1, 2])
Path("/repo/out.txt").write_text(numbers[0])

bad = sorted(Path("/repo/input.txt"))

with open("/repo/out.txt", "w") as writer:
    sorted(writer)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 2);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3031");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3109");
    }

    [Fact]
    public void ComparisonExpressionTypeFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = Path("/repo/input.txt").read_text()
contains = "a" in text
Path("/repo/out.txt").write_text(contains)

ordered = 1 < len(text) < 10
Path("/repo/out.txt").write_text(ordered)

same = text is None
Path("/repo/out.txt").write_text(same)
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3072") >= 3);
    }

    [Fact]
    public void OperatorOperandContracts_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

Path("/repo") + "child"
"text" - "x"
"text" * "x"
"text" / "x"
"text" // 1
"text" % 1
"text" ** 2
b"a" + b"b"
1.5 | 2
{} ^ {}
"text" << 1
+"text"
~1.5
1 < "x"
1 in 2
1 in "abc"
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3141") >= 11);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3142") >= 2);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3143");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3144");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3145");
    }

    [Fact]
    public void OperatorOperandContracts_RemainConservativeForValidAndUnknownShapes()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

text = "a" + "b"
items = [1] + [2]
pair = (1,) + (2,)
union = {1} | {2}
joined = Path("/repo") / "child"
count = True + 1
repeat = "a" * 2
ok = "a" in text

maybe = 1 if flag else "x"
maybe + 1
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void ArgparseParseArgsLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import argparse

parser = argparse.ArgumentParser()
parser.parse_args("abc")
parser.parse_args(1)
parser.parse_args(["--lang", 1])
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3064");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3065");
    }

    [Fact]
    public void CsvReaderAndFnmatchFilterLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import csv
import fnmatch

csv.reader(1)
csv.reader([1])
csv.reader(["a"], delimiter=1)
csv.reader(["a"], delimiter="")
csv.reader(["a"], delimiter="\n")
fnmatch.filter(["a.txt", 1], "*.txt")
fnmatch.filter(1, "*.txt")
fnmatch.filter(["a.txt"], 1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3066");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3067");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3068");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3069");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3070");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3071");
    }

    [Fact]
    public void PathWriteTextAndStringPrefixSuffixLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

Path("/repo/out.txt").write_text(1)
"hello".startswith(1)
"hello".endswith((1,))
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3072");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3073");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3074");
    }

    [Fact]
    public void StringSingleArgumentLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
"hello".find(1)
"hello".index(1)
"hello".rfind(1)
"hello".rindex(1)
"hello".count(1)
"hello".removeprefix(1)
"hello".removesuffix(1)
"hello".partition(1)
"hello".rpartition(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void StringBoundsLiteralMisuse_ReportAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
"hello".startswith("he", "1")
"hello".endswith("lo", 0, "5")
"hello".find("e", "1")
"hello".count("l", 1, "4")
"hello".index("e", "1")
"hello".rfind("e", "1")
"hello".rindex("e", "1")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3076");
    }

    [Fact]
    public void StableLiteralAliases_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path
import fnmatch

enc1 = "latin-1"
enc2 = enc1
Path("/repo/in.txt").read_text(encoding=enc2)

data1 = b"abc"
data2 = data1
__lython_file = open("/repo/out.txt", "w")
__lython_file.write(data2)
__lython_file.close()

names1 = ["a.txt", 1]
names2 = names1
fnmatch.filter(names2, "*.txt")

text1 = "hello"
text2 = text1
needle1 = 1
needle2 = needle1
text2.find(needle2)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3049");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3111");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3070");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void Utf8SigPathTextEncodings_RemainAcceptedAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path

Path("/repo/in.txt").read_text(encoding="utf-8-sig")
Path("/repo/in.txt").read_text(encoding="utf-8-sig", errors="strict")
Path("/repo/out.txt").write_text("alpha", encoding="utf-8-sig", errors="strict", newline="")
Path("/repo/out.txt").open("w", encoding="utf-8-sig", errors="strict", newline="")
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public void BuiltinModuleAndStringContractAliases_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import argparse
import functools
import glob
import os
from pathlib import Path

desc1 = 1
desc2 = desc1
argparse.ArgumentParser(desc2)

func1 = 1
func2 = func1
functools.partial(func2, 1)

recursive1 = 1
recursive2 = recursive1
glob.glob("*.txt", recursive=recursive2)

pattern1 = 1
pattern2 = pattern1
glob.iglob(pattern2)

path1 = 1
path2 = path1
Path(path2).read_text()

name1 = 1
name2 = name1
os.path.join("/repo", name2)

prompt1 = 1
prompt2 = prompt1
input(prompt2)

sep1 = 1
sep2 = sep1
"a,b".split(sep2)

count1 = "x"
count2 = count1
"a,b".rsplit(",", count2)

chars1 = 1
chars2 = chars1
"  a  ".strip(chars2)

old1 = 1
old2 = old1
"hello".replace(old2, "x")

replace_count1 = "x"
replace_count2 = replace_count1
"hello".replace("l", "x", replace_count2)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3088");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3085");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3090");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3089");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("pathlib.Path", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("os.path.join", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3086");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3091");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3092");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3093");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3094");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3095");
    }

    [Fact]
    public void AdditionalModuleIterableAndStringContracts_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import argparse
import functools
import glob
import random

required1 = 1
required2 = required1
parser = argparse.ArgumentParser()
parser.add_mutually_exclusive_group(required=required2)
parser.add_argument("--lang", required=required2)

wrapper1 = 1
wrapper2 = wrapper1
functools.update_wrapper(wrapper2, object())

weights1 = 1
weights2 = weights1
random.choices([1, 2], weights=weights2)

both1 = [1]
both2 = both1
random.choices([1, 2], weights=both1, cum_weights=both2)

escape1 = 1
escape2 = escape1
glob.escape(escape2)

mapping1 = []
mapping2 = mapping1
"{name}".format_map(mapping2)

d = {}
update1 = []
update2 = update1
d.update(update2)

items1 = ["a", 1]
items2 = items1
"-".join(items2)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3097");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3105");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3106");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3098");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3100");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3096");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3103");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3104");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3102");
    }

    [Fact]
    public void FunctoolsExpandedContracts_ReportKnownMembersAndShapeErrors()
    {
        var valid = new LythonEngine().Compile(
            """
import functools

def f(x):
    return x

functools.WRAPPER_ASSIGNMENTS
functools.WRAPPER_UPDATES
functools.Placeholder
functools.update_wrapper(f, f, assigned=("__name__",), updated=())
functools.wraps(f, assigned=("__name__",), updated=())(f)
functools.lru_cache(f)
functools.lru_cache(maxsize=2, typed=True)(f)
functools.cache(f)
functools.cached_property(f)
functools.partialmethod(f, 1)
functools.singledispatch(f)
functools.singledispatchmethod(f)
functools.recursive_repr(fillvalue="...")(f)
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));

        var invalid = new LythonEngine().Compile(
            """
import functools

functools.missing
functools.cache()
functools.cached_property(1)
functools.singledispatch(1)
functools.singledispatchmethod(1)
functools.lru_cache(maxsize="many")
functools.lru_cache(typed="yes")
functools.recursive_repr(fillvalue=1)
""");

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3113");
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3151");
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3085");
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3158");
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenLiteralLoopShapeErrorsExist()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
for left, right in [1, 2]:
    __lython_file = open("/repo/created.txt", "w")
    __lython_file.write("side effect")
    __lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3031");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenHostLacksStandardInput()
    {
        var host = new MockLythonHost("/repo");
        var compiled = new LythonEngine().Compile(
            """
value = input()
__lython_file = open("/repo/created.txt", "w")
__lython_file.write(value)
__lython_file.close()
""");

        Assert.True(compiled.IsValid);

        var result = compiled.Run(host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3040");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenHostLacksSubprocessSupport()
    {
        var host = new MockLythonHost("/repo");
        var compiled = new LythonEngine().Compile(
            """
import subprocess
proc = subprocess.run(["rg", "x"], capture_output=True)
__lython_file = open("/repo/created.txt", "w")
__lython_file.write(proc.stdout)
__lython_file.close()
""");

        Assert.True(compiled.IsValid);

        var result = compiled.Run(host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3041");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenHostLacksSubprocessImportCapability()
    {
        var host = new MockLythonHost("/repo");
        var compiled = new LythonEngine().Compile(
            """
import subprocess
__lython_file = open("/repo/created.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""");

        Assert.True(compiled.IsValid);

        var result = compiled.Run(host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3041");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenRegexStaticErrorsExist()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
import re
PATTERN = r"\N{LATIN SMALL LETTER A}"
FLAGS = re.LOCALE
re.search(PATTERN, "a", FLAGS)
__lython_file = open("/repo/created.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3044");
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3045");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenArgparseParseArgsStaticErrorsExist()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
import argparse
parser = argparse.ArgumentParser()
parser.parse_args(["--lang", 1])
__lython_file = open("/repo/created.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3065");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenByteTextBoundaryStaticErrorsExist()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path
__lython_file = open("/repo/out.txt", "w")
__lython_file.write(b"abc")
__lython_file.close()
Path("/repo/out.bin").write_bytes(b"abc")
__lython_file = open("/repo/created.txt", "a")
__lython_file.write("side effect")
__lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3111");
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3047");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void Run_DoesNotStartExecutionWhenAliasedStaticErrorsExist()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

enc1 = "latin-1"
enc2 = enc1
Path("/repo/in.txt").read_text(encoding=enc2)
__lython_file = open("/repo/created.txt", "w")
__lython_file.write("side effect")
__lython_file.close()
""",
            host);

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3049");
        Assert.False(host.Stat("/repo/created.txt").Exists);
    }

    [Fact]
    public void HostRequirementChecks_RemainConservativeForDeferredCalls()
    {
        var host = new MockLythonHost("/repo");

        var result = new LythonEngine().Run(
            """
def ask():
    return input()

if False:
    ask()

__lython_file = open("/repo/out.txt", "w")
__lython_file.write("ok")
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ok", host.ReadText("/repo/out.txt"));
    }

    [Fact]
    public void SimpleDataclassFieldFlow_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import dataclass

@dataclass
class Box:
    text: str

box = Box("alpha")
box.text.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void OrdinaryUserClasses_RemainDynamicForStaticAnalysis()
    {
        var compiled = new LythonEngine().Compile(
            """
class Box:
    text: str

box = Box()
box.text.find(1)
""");

        Assert.True(compiled.IsValid);
    }

    [Fact]
    public void FunctionLocalReadBeforeAssignment_ReportStaticErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
def bad():
    value.find("x")
    value = "alpha"

bad()
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3146");
    }

    [Fact]
    public void FunctionLocalMaybeAssignedBranches_RemainAccepted()
    {
        var compiled = new LythonEngine().Compile(
            """
def maybe(flag):
    if flag:
        value = "alpha"
    return value
""");

        Assert.True(compiled.IsValid);
    }

    [Fact]
    public void DataclassFieldDefaultsAndFactories_FlowIntoStaticDiagnostics()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import dataclass, field

@dataclass
class Box:
    text: str = field(default="alpha")
    items: list = field(default_factory=list)

box = Box()
box.text.find(1)
box.items.extend(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3140");
    }

    [Fact]
    public void DataclassKeywordOnlyAndInitFalseFields_FlowIntoStaticDiagnostics()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import dataclass, field

@dataclass(kw_only=True)
class Box:
    text: str
    label: str = field(default="beta", init=False)

box = Box(text="alpha")
box.text.find(1)
box.label.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3075") >= 2);
    }

    [Fact]
    public void DataclassMethodReturnFlow_BindsSelfFieldAtCallSite()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import dataclass

@dataclass
class Box:
    text: str

    def as_text(self):
        return self.text

box = Box("alpha")
box.as_text().find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void KnownModuleSurfaces_ReportMissingMembersThroughAliases()
    {
        var compiled = new LythonEngine().Compile(
            """
import json as j
import os
import re

j.not_real
os.path.not_real("x")
pattern = re.compile("x")
pattern.not_real
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3113") >= 3);
    }

    [Fact]
    public void KnownCallableContracts_ReportAliasAndFromImportShapeErrors()
    {
        var compiled = new LythonEngine().Compile(
            """
import json as j
from csv import writer
from pathlib import Path

j.dumps(obj=1, extra=2)
writer(delimiter=",", extra=True)
Path(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(2, compiled.Diagnostics.Count(d => d.Code == "LA3151"));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("pathlib.Path", StringComparison.Ordinal));
    }

    [Fact]
    public void OperatorExpandedContracts_ReportKnownMembersAndShapeErrors()
    {
        var valid = new LythonEngine().Compile(
            """
import operator

operator.truth(1)
operator.not_([])
operator.is_(None, None)
operator.is_not([], [])
operator.abs(-1)
operator.neg(1)
operator.pos(1)
operator.invert(1)
operator.index(True)
operator.floordiv(7, 2)
operator.mod(7, 2)
operator.pow(2, 3)
operator.lshift(1, 2)
operator.rshift(4, 1)
operator.and_(6, 3)
operator.or_(4, 1)
operator.xor(6, 3)
operator.concat([1], [2])
operator.length_hint([1, 2], default=0)
operator.countOf([1, 1], 1)
operator.indexOf([1, 2], 2)
operator.call(operator.add, 1, 2)
operator.itemgetter(0, 1)
operator.attrgetter("name", "child.value")
operator.methodcaller("strip")
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Code + ":" + d.Message)));

        var invalid = new LythonEngine().Compile(
            """
import operator

operator.not_real
operator.truth()
operator.add(1)
operator.setitem([], 0)
operator.itemgetter()
operator.itemgetter(item=0)
operator.attrgetter()
operator.length_hint([], default=0, extra=1)
operator.call()
""");

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3113");
        Assert.True(
            invalid.Diagnostics.Count(d => d.Code == "LA3151") >= 7,
            string.Join(" | ", invalid.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void KnownRuntimeObjectContracts_FlowThroughReturnValues()
    {
        var compiled = new LythonEngine().Compile(
            """
import csv
import subprocess

w = csv.writer()
w.getvalue(1)
w.getvalue().find(1)

result = subprocess.run(["echo"])
result.returncode.find("x")
result.stdout.decode()
result.not_real
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3155");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3113");
    }

    [Fact]
    public void OpenPyxlRuntimeObjectContracts_ReportSealedMemberTypos()
    {
        var compiled = new LythonEngine().Compile(
            """
import openpyxl
from openpyxl.chart import BarChart
from openpyxl.comments import Comment
from openpyxl.drawing.image import Image
from openpyxl.styles import Alignment, Border, Font, NamedStyle, PatternFill, Protection, Side
from openpyxl.styles.colors import Color
from openpyxl.worksheet.table import Table, TableStyleInfo
from openpyxl.worksheet.datavalidation import DataValidation

wb = openpyxl.Workbook()
wb.not_a_workbook_member
wb.security.not_a_workbook_protection_member
wb.security.set_workbook_password()
wb.named_styles[0].not_a_named_style_member
wb.named_styles[0].font.not_a_font_member

ws = wb.active
ws.not_a_worksheet_member
wb["Sheet"].not_a_sheet_member

cell = ws.cell(row=1, column=1)
cell.not_a_cell_member
ws["A1"].not_a_cell_member
cell.offset(row=1).not_a_cell_member
cell.font.not_a_font_member

Font(bold=True).not_a_font_member
PatternFill(fill_type="solid").not_a_fill_member
Border(left=Side(style="thin")).left.not_a_side_member
Alignment(horizontal="center").not_a_alignment_member
Protection(locked=True).not_a_protection_member
NamedStyle("named").not_a_named_style_member
Color(rgb="FF0000").not_a_color_member

comment = Comment("note", "me")
comment.not_a_comment_member

table = Table(displayName="Table1", ref="A1:B2")
table.not_a_table_member
TableStyleInfo(name="TableStyleMedium2").not_a_table_style_member
DataValidation(type="whole").not_a_validation_member
BarChart().not_a_chart_member
Image("/image.png").not_a_image_member

cell.offset(1, 2, 3)
""");

        Assert.False(compiled.IsValid);
        Assert.True(
            compiled.Diagnostics.Count(d => d.Code == "LA3113") >= 15,
            string.Join(" | ", compiled.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3161");

        var worksheetSatellites = new LythonEngine().Compile(
            """
import openpyxl

ws = openpyxl.Workbook().active
ws.auto_filter.not_a_filter_member
ws.sheet_view.not_a_sheet_view_member
ws.page_margins.not_a_margins_member
ws.page_setup.not_a_setup_member
ws.protection.not_a_sheet_protection_member
ws.tables.not_a_table_list_member
ws.data_validations.not_a_validation_list_member
ws.conditional_formatting.not_a_conditional_formatting_member
ws.merged_cells.not_a_ranges_member
ws._drawing.not_a_drawing_member
""");

        Assert.False(worksheetSatellites.IsValid);
        Assert.True(
            worksheetSatellites.Diagnostics.Count(d => d.Code == "LA3113") >= 10,
            string.Join(" | ", worksheetSatellites.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Fact]
    public void UserDefinedCallShapes_ReportProvableMismatches()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import dataclass

def helper(path, *, mode="r"):
    return path

helper()
helper("x", path="y")
helper("x", unknown=1)

@dataclass
class Row:
    name: str
    count: int

    def render(self, suffix):
        return self.name + suffix

Row(name="x")
Row("x", "y", "z")
row = Row("x", 1)
row.render()
row.render("!", suffix="?")
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3148") >= 3);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3149") >= 2);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3150") >= 2);
    }

    [Fact]
    public void ConstantBranches_DoNotAnalyzeUnreachableStaticErrors()
    {
        var compiled = new LythonEngine().Compile(
            """
value = None
if value is None:
    value = "alpha"
else:
    value.not_real

value.find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Code == "LA3113");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3075");
    }

    [Fact]
    public void ArgparseNamespaceShapeFlow_ReportTyposAndMemberMisuseAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import argparse

parser = argparse.ArgumentParser()
parser.add_argument("--apply", action="store_true")
parser.add_argument("--include", action="append", default=[])
parser.add_argument("--max-rounds", type=int, default=12)
args = parser.parse_args(args=[])

args.aply
args.apply + "x"
args.include.extend(1)
args.max_rounds + "x"
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3113");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3141") >= 2);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3140");
    }

    [Fact]
    public void RegexMaybeMatchRefinements_ReportGuardedMemberErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import re

m = re.search("a", "abc")
assert m is not None
m.start(1, 2)
m.not_real
m.group().find(1)

def first(text):
    match = re.search("a", text)
    if match is None:
        return ""
    return match.group()

first("abc").find(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3152");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3113");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3075") >= 2);
    }

    [Fact]
    public void RegexExpandedContracts_ReportKnownMembersAndShapeErrorsAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
import re

pat = re.compile(r"(?P<word>a)(b)?", re.NOFLAG | re.ASCII)
m = pat.search("xa", pos=1, endpos=2)
if m is not None:
    pat.pattern.find("a")
    pat.flags + 1
    pat.groups + 1
    pat.groupindex
    m.re.pattern.find("a")
    m.string.find("x")
    m.pos + m.endpos
    m.groups(default="")
    m.groupdict(default="")
    m.expand(r"\g<word>")
    m.start("word")
    m.end(group="word")
    m.span("missing")
    m.expand(1)

pat.search("x", pos="bad")
re.search("a", "a", pos="bad")
re.sub("a", "x", "a", pos="bad")
re.split("a", "a", endpos="bad")
re.compile("a", re.DEBUG)
re.purge(1)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3159" && d.Message.Contains("missing", StringComparison.Ordinal));
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3158") >= 5);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3045" && d.Message.Contains("DEBUG", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3151" && d.Message.Contains("re.purge", StringComparison.Ordinal));
    }

    [Fact]
    public void DataclassHelperShapeFlow_ReportHelperMisuseAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import asdict, astuple, dataclass, field, fields, replace

@dataclass
class Row:
    name: str
    count: int
    hidden: str = field(default="x", init=False)

row = Row("alpha", 3)
fields(row)[0].name.find(1)
asdict(row)["name"].find(1)
astuple(row)[1] + "x"
replace(row, missing=1)
replace(row, hidden="y")
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3075") >= 2);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3141");
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3156") >= 2);
    }

    [Fact]
    public void DataclassExpandedHelperSurface_IsAcceptedAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import Field, dataclass, field, fields, make_dataclass

@dataclass
class Row:
    name: str = field(default="alpha")

field_type = fields(Row)[0].type
field_class = Field
Dynamic = make_dataclass("Dynamic", [("x", int)])
""");

        Assert.True(compiled.IsValid, string.Join(Environment.NewLine, compiled.Diagnostics.Select(d => d.ToString())));
    }

    [Fact]
    public void SealedPrimitiveDataclassAndDictSurfaces_ReportProvableTyposAtCompileTime()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import asdict, dataclass

@dataclass
class Row:
    name: str
    count: int

row = Row("alpha", 3)
row.nmae
row.count.find("x")
"abc".decode()
value = None
value.strip()
asdict(row)["nmae"]
{"name": "alpha"}["nmae"]
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3113") >= 4);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3157") >= 2);
    }

    [Fact]
    public void KnownCallAbstractTypeContracts_ReportAliasBackedRuntimeTypeErrors()
    {
        var compiled = new LythonEngine().Compile(
            """
import json
import os
import re
import subprocess

path = 1
os.path.join("/repo", path)
re.search(1, "abc")
json.loads({})
subprocess.run(["rg", 1])
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3158") >= 4);
    }

    [Fact]
    public void RegexSummaries_ReportImpossibleGroupsAndLiteralNoMatch()
    {
        var compiled = new LythonEngine().Compile(
            """
import re

m = re.search("(?P<word>a)", "abc")
assert m is not None
m.group("missing")
m.group(2)
m.group({})

n = re.search("z", "abc")
n.group()
""");

        Assert.False(compiled.IsValid);
        Assert.True(compiled.Diagnostics.Count(d => d.Code == "LA3159") >= 3);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3113");
    }

    [Fact]
    public void ExactStarArgumentExpansion_FeedsShapeAndSemanticContracts()
    {
        var compiled = new LythonEngine().Compile(
            """
from pathlib import Path
import re

def helper(name):
    return name

bad_path_args = [1]
Path(*bad_path_args)
kwargs = {"bad": 1}
helper(**kwargs)
args = [1, "abc"]
re.search(*args)
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158" && d.Message.Contains("pathlib.Path", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3148");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3158");
    }

    [Fact]
    public void ExpandedSysContracts_RecognizeMetadataAndReportBadCalls()
    {
        var valid = new LythonEngine().Compile(
            """
import sys

parts = [
    sys.version,
    sys.platform,
    sys.byteorder,
    sys.prefix,
    sys.base_prefix,
    sys.executable,
    sys.path[0],
    sys.implementation.name,
]
sys.getdefaultencoding()
sys.exc_info()
sys.getsizeof(parts)
sys.settrace(None)
sys.setprofile(None)
sys.setrecursionlimit(1000)
sys.addaudithook(None)
sys.audit("event", 1)
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Code + ":" + d.Message)));

        var invalid = new LythonEngine().Compile(
            """
import sys

sys.missing
sys.getdefaultencoding("utf-8")
sys.exc_info(1)
sys.getsizeof()
sys.settrace()
sys.audit()
""");

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3113");
        Assert.True(invalid.Diagnostics.Count(d => d.Code == "LA3151") >= 5);
    }
}
