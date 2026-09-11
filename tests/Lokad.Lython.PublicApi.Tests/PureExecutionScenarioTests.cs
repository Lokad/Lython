using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PureExecutionScenarioTests
{
    [Fact]
    public void ComprehensionsUseSharedCellsWithoutLeakingLoopTargets()
    {
        var result = new LythonEngine().Run(
            """
total = 0
values = [(total := total + x) for x in [1, 2, 3]]
functions = [lambda: x for x in [1, 2, 3]]
try:
    x
    leaked = True
except NameError:
    leaked = False
return str(values) + "|" + str(total) + "|" + str([f() for f in functions]) + "|" + str(leaked)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[1, 3, 6]|6|[3, 3, 3]|False", result.ReturnValue);
    }

    [Fact]
    public void SortedEnumerateAndRangeFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "Values", "SortedEnumerateAndRange"));
        var host = new MockLythonHost();

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
    public void LogicAndMembershipFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "Operators", "LogicAndMembership"));
        var host = new MockLythonHost();

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
    public void FloatAndTupleFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "Values", "FloatAndTuple"));
        var host = new MockLythonHost();

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
    public void ConstructorsAndStringsFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "Values", "ConstructorsAndStrings"));
        var host = new MockLythonHost();

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
    public void NumericOperatorsAndComparisonsFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "Operators", "NumericOperatorsAndComparisons"));
        var host = new MockLythonHost();

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
    public void TextBuiltins_NormalizeNewlinesToLf()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "a\r\nb\rc\n");

        var result = new LythonEngine().Run(
            """
text = open("/input.txt").read()
__lython_file = open("/output.txt", "w")
__lython_file.write(text)
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("a\nb\nc\n", host.ReadText("/output.txt"));
    }

    [Fact]
    public void DictViews_AreLiveIterables()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
d = {"a": 1}
ks = d.keys()
vs = d.values()
its = d.items()
d.update({"b": 2})
__lython_file = open("/out.txt", "w")
__lython_file.write(str(len(list(ks))) + "," + str(len(list(vs))) + "," + str(len(list(its))) + "," + str(list(its)[1][0]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("2,2,2,b", host.ReadText("/out.txt"));
    }

    [Fact]
    public void AssignmentExpressions_BindAndReturnValueInPythonLikePositions()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
if (n := 3):
    vals.append(str(n))
text = (word := "alpha")
vals.append(word)
vals.append(text)
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        if (!result.Success)
        {
            throw new Xunit.Sdk.XunitException(result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        }
        Assert.Equal("3|alpha|alpha", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GeneratorExpressions_AreLazyAndWorkInsideBuiltinCalls()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
factor = 2
values = (x * factor for x in [1, 2, 3] if x > 1)
factor = 4
items = list(values)
flag = any(x == 8 for x in items)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + str(flag))
__lython_file.close()
""",
            host);

        Assert.True(
            result.Success,
            result.Failure?.Message ??
            string.Join(" | ", result.Diagnostics.Select(d => d.Span is null ? d.Message : $"{d.Message} @ {d.Span.Line}:{d.Span.Column}")));
        Assert.Equal("[8, 12]|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void MathModule_SupportsRepresentativeScalarAndAggregateCalls()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import math
vals = []
vals.append(str(math.sqrt(9)))
vals.append(str(math.log(8, 2)))
vals.append(str(math.floor(3.9)))
vals.append(str(math.ceil(3.1)))
vals.append(str(math.isclose(math.pi, 3.1415926536)))
vals.append(str(math.prod([2, 3, 4])))
vals.append(str(math.fsum([0.1, 0.2, 0.3])))
vals.append(str(math.isfinite(math.inf)))
vals.append(str(math.isnan(math.nan)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3.0|3.0|3|4|True|24|0.6|False|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DateTimeModule_SupportsIsoArithmeticAndIntrospection()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import datetime

delta = datetime.timedelta(days=1, seconds=3661, microseconds=5)
d = datetime.date(2024, 1, 2)
dt = datetime.datetime(2024, 1, 2, 3, 4, 5, 6)
t = datetime.time(7, 8, 9, 10, tzinfo=datetime.timezone.utc)

vals = []
vals.append(str(delta.days))
vals.append(str(delta.seconds))
vals.append(str(delta.microseconds))
vals.append(str(delta.total_seconds()))
vals.append(str((delta * 2).total_seconds()))
vals.append(str((delta / 2).total_seconds()))
vals.append(str(delta // datetime.timedelta(hours=1)))
vals.append(d.isoformat())
vals.append(str(d.weekday()))
vals.append((d + datetime.timedelta(days=2)).isoformat())
vals.append(str((datetime.date(2024, 1, 5) - d).days))
vals.append(d.strftime("%Y/%m/%d"))
vals.append(dt.replace(year=2025, hour=9).isoformat())
vals.append(dt.strftime("%Y-%m-%d %H:%M:%S"))
vals.append(datetime.datetime.strptime("2024-01-02 03:04:05", "%Y-%m-%d %H:%M:%S").isoformat())
vals.append(datetime.date.fromisoformat("2024-02-03").isoformat())
vals.append(datetime.time.fromisoformat("07:08:09+00:00").isoformat())
vals.append(datetime.datetime.fromisoformat("2024-01-02T03:04:05+00:00").isoformat())
vals.append(dt.date().isoformat())
vals.append(dt.time().isoformat())
vals.append(t.isoformat())
vals.append(str(isinstance(dt, datetime.datetime)))
vals.append(str(type(delta).__name__))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "1|3661|5|90061.000005|180122.00001|45030.500002|25|2024-01-02|1|2024-01-04|3|2024/01/02|2025-01-02T09:04:05.000006|2024-01-02 03:04:05|2024-01-02T03:04:05|2024-02-03|07:08:09+00:00|2024-01-02T03:04:05+00:00|2024-01-02|03:04:05.000006|07:08:09.000010+00:00|True|timedelta",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void SysArgv_UsesRunOptionsArguments()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import sys
__lython_file = open("/out.txt", "w")
__lython_file.write(str(sys.argv))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Args = ["--lang", "fr", "--apply"]
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['--lang', 'fr', '--apply']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ZipAndNext_SupportCommonAgentScriptShapes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
pairs = list(zip(["a", "b"], [1, 2, 3]))
first = next((x for x in [1, 2, 3] if x > 1), None)
missing = next((x for x in [1] if x > 4), None)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(pairs) + "|" + str(first) + "|" + str(missing))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[('a', 1), ('b', 2)]|2|None", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ZipEnumerateAndNext_ComposeInScriptLikeLineScans()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
src_lines = ["src-a\n", "src-b\n"]
dst_lines = ["dst-a\n", "dst-b\n"]
items = []
for idx, (src_line, dst_line) in enumerate(zip(src_lines, dst_lines)):
    items.append(str(idx) + ":" + src_line.rstrip() + ":" + dst_line.rstrip())

lines = ["alpha", "type: talk-about-video-slider", "omega"]
insert_at = next((idx for idx, line in enumerate(lines) if "type: talk-about-video-slider" in line), None)

__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + str(insert_at))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['0:src-a:dst-a', '1:src-b:dst-b']|1", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertyDecorator_BindsGetterOnInstanceAndExposesDescriptorOnClass()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self, value):
        self._value = value

    @property
    def value(self):
        return self._value + 1

box = Box(4)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(box.value) + "|" + str(Box.value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("5|<property object>", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertyBuiltin_SupportsGetterAndSetter()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self, value):
        self._value = value

    def get_value(self):
        return self._value

    def set_value(self, value):
        self._value = value * 2

    value = property(get_value, set_value)

box = Box(3)
box.value = 5
__lython_file = open("/out.txt", "w")
__lython_file.write(str(box.value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("10", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertyWithoutSetter_FailsWithActionableAttributeError()
    {
        var result = new LythonEngine().Run(
            """
class Box:
    @property
    def value(self):
        return 1

box = Box()
box.value = 2
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("AttributeError", result.Failure?.ExceptionType);
        Assert.Contains("has no setter", result.Failure?.Message);
    }

    [Fact]
    public void PropertyWithoutDeleter_FailsWithActionableAttributeError()
    {
        var result = new LythonEngine().Run(
            """
class Box:
    @property
    def value(self):
        return 1

box = Box()
del box.value
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("AttributeError", result.Failure?.ExceptionType);
        Assert.Contains("has no deleter", result.Failure?.Message);
    }

    [Fact]
    public void Argparse_ParsesRequiredChoicesFlagsRepeatsAndTypedValues()
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

__lython_file = open("/out.txt", "w")
__lython_file.write(args.lang + "|" + str(args.include) + "|" + str(args.apply) + "|" + str(args.max_rounds))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Args = ["--lang", "fr", "--include", "a", "--include", "b", "--apply", "--max-rounds", "5"]
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("fr|['a', 'b']|True|5", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Argparse_ScriptLikeOptionalDefaultsRemainPythonShaped()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import argparse

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="demo")
    parser.add_argument("--lang", required=True, choices=("fr", "de"))
    parser.add_argument("--repo-root", default=None)
    parser.add_argument("--include", action="append", default=[])
    parser.add_argument("--exclude", action="append", default=[])
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    return parser.parse_args()

args = parse_args()
__lython_file = open("/out.txt", "w")
__lython_file.write(str(args.repo_root) + "|" + str(args.include) + "|" + str(args.exclude) + "|" + str(args.apply) + "|" + str(args.verbose))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Args = ["--lang", "fr"]
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("None|[]|[]|False|False", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ArgparseFailures_UseSystemExitWithStructuredExitCode()
    {
        var result = new LythonEngine().Run(
            """
import argparse

parser = argparse.ArgumentParser()
parser.add_argument("--lang", required=True, choices=("fr", "de"))
parser.parse_args()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                Args = ["--lang", "es"]
            });

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure?.ExceptionType);
        Assert.Contains("invalid choice", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DictSetDefault_SupportsScriptLikeGroupingAndTupleAppend()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
grouped = {}
grouped.setdefault("a.md", []).append((12, "bad link"))
grouped.setdefault("a.md", []).append((15, "extra link"))
grouped.setdefault("b.md", []).append((None, "front matter"))
__lython_file = open("/out.txt", "w")
__lython_file.write(str(grouped))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{'a.md': [(12, 'bad link'), (15, 'extra link')], 'b.md': [(None, 'front matter')]}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SysExit_IntegerCode_UsesStructuredTermination()
    {
        var result = new LythonEngine().Run(
            """
import sys
sys.exit(3)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(3, result.ExitCode);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure?.ExceptionType);
    }

    [Fact]
    public void SysExit_OmittedCode_UsesZeroExitCode()
    {
        var result = new LythonEngine().Run(
            """
import sys
sys.exit()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure?.ExceptionType);
    }

    [Fact]
    public void SysExit_KeywordCode_UsesStructuredTermination()
    {
        var result = new LythonEngine().Run(
            """
import sys
sys.exit(code=4)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(4, result.ExitCode);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure?.ExceptionType);
    }

    [Fact]
    public void SystemExit_PropagatesAsStructuredTermination()
    {
        var result = new LythonEngine().Run(
            """
raise SystemExit(3)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(3, result.ExitCode);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure?.ExceptionType);
    }

    [Fact]
    public void SystemExit_OutOfRangeIntegerCode_DoesNotThrowClrOverflow()
    {
        LythonExecutionResult? result = null;
        var exception = Record.Exception(() =>
        {
            result = new LythonEngine().Run(
                """
raise SystemExit(10 ** 100)
""",
                new MockLythonHost());
        });

        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure?.ExceptionType);
    }

    [Fact]
    public void SystemExit_IsNotCaughtByExceptException()
    {
        var result = new LythonEngine().Run(
            """
try:
    raise SystemExit(5)
except Exception:
    __lython_file = open("/out.txt", "w")
    __lython_file.write("caught")
    __lython_file.close()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(5, result.ExitCode);
        Assert.Equal("SystemExit", result.Failure?.ExceptionType);
    }

    [Fact]
    public void SystemExit_CanBeCaughtByBareExcept()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
try:
    raise SystemExit(5)
except:
    __lython_file = open("/out.txt", "w")
    __lython_file.write("caught")
    __lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("caught", host.ReadText("/out.txt"));
    }

    [Fact]
    public void NestedFunctions_CaptureLocalsAndEvaluateDefaultsAtDefinitionTime()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def outer():
    factor = 2
    def inner(value=factor + 1):
        return factor * value
    factor = 4
    return inner()

__lython_file = open("/out.txt", "w")
__lython_file.write(str(outer()))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("12", host.ReadText("/out.txt"));
    }

    [Fact]
    public void KeywordOnlyParameters_WorkForFunctionsAndLambdas()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def build(*parts, sep="|", suffix):
    return sep.join(parts) + suffix

f = lambda *parts, tail, sep="-": sep.join(parts) + tail

__lython_file = open("/out.txt", "w")
__lython_file.write(build("a", "b", suffix="!") + "|" + f("x", "y", tail="?", sep=":"))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("a|b!|x:y?", host.ReadText("/out.txt"));
    }

    [Fact]
    public void BytesLiterals_SupportLengthEqualityIndexingAndRendering()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
value = b"ab\x00"
parts = []
parts.append(str(len(value)))
parts.append(str(value == b"ab\x00"))
parts.append(str(value[1]))
parts.append(str(value))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(@"3|True|98|b'ab\x00'", host.ReadText("/out.txt"));
    }

    [Fact]
    public void AttributeAssignment_WorksForScriptModules()
    {
        var host = new MockLythonHost();
        host.SeedFile("helper.py", "value = 1\n");

        var result = new LythonEngine().Run(
            """
import helper
helper.value = 3
__lython_file = open("/out.txt", "w")
__lython_file.write(str(helper.value))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ClassDefinition_SupportsFieldsMethodsAndClassAttributes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    kind = "box"

    def __init__(self, value):
        self.value = value

    def render(self):
        return self.kind + ":" + self.value

box = Box("ok")
Box.kind = "crate"
__lython_file = open("/out.txt", "w")
__lython_file.write(box.render() + "|" + Box.kind)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("crate:ok|crate", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ClassDefinition_SupportsSingleInheritance()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    def value(self):
        return "a"

class Child(Base):
    pass

__lython_file = open("/out.txt", "w")
__lython_file.write(Child().value())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("a", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Match_ClassPattern_UsesUserDefinedMatchArgs()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Point:
    __match_args__ = ("x", "y")

    def __init__(self, x, y):
        self.x = x
        self.y = y

value = Point(1, 2)
match value:
    case Point(1, y):
        __lython_file = open("/out.txt", "w")
        __lython_file.write(str(y))
        __lython_file.close()
    case _:
        __lython_file = open("/out.txt", "w")
        __lython_file.write("miss")
        __lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ClassDefinition_SupportsMultipleInheritanceUsingC3Mro()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class A:
    def order(self):
        return "A"

class B(A):
    def order(self):
        return "B>" + super().order()

class C(A):
    def order(self):
        return "C>" + super().order()

class D(B, C):
    pass

__lython_file = open("/out.txt", "w")
__lython_file.write(D().order() + "|" + str(issubclass(D, C)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("B>C>A|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ClassDefinition_InconsistentMultipleInheritanceFailsPrecisely()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class X:
    pass

class Y:
    pass

class A(X, Y):
    pass

class B(Y, X):
    pass

class C(A, B):
    pass
""",
            host);

        Assert.False(result.Success);
        var failure = result.Failure;
        Assert.Equal("TypeError", failure?.ExceptionType);
        Assert.Contains("consistent method resolution order", failure?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dataclass_GeneratesInitReprEqAndMatchArgs()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass

@dataclass
class Point:
    x: int
    y: int = 2

point = Point(1)
parts = []
parts.append(str(point))
parts.append(str(point == Point(1, 2)))

match point:
    case Point(1, y):
        parts.append(str(y))
    case _:
        parts.append("miss")

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Point(x=1, y=2)|True|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Dataclass_RespectsDisabledGeneratedMembers()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass

@dataclass(repr=False, eq=False, match_args=False)
class Box:
    value: int

box = Box(1)
parts = []
parts.append(str(box))
parts.append(str(box == Box(1)))

match box:
    case Box(value=1):
        parts.append("hit")
    case _:
        parts.append("miss")

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("<Box object>|False|hit", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Dataclass_FieldMetadataKwOnlyDefaultFactoryAndPostInitWorkTogether()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import KW_ONLY, dataclass, field, is_dataclass

@dataclass
class Box:
    a: int
    _: KW_ONLY
    b: list = field(default_factory=list, repr=False, compare=False)
    c: int = field(init=False, default=9)

    def __post_init__(self):
        self.b.append(self.a)

@dataclass(kw_only=True)
class Opt:
    value: int

box = Box(2, b=[7])
opt = Opt(value=4)
parts = []
parts.append(str(is_dataclass(Box)))
parts.append(str(is_dataclass(box)))
parts.append(str(box))
parts.append(str(box.b))
parts.append(str(box.c))
parts.append(str(Box.__dataclass_params__.kw_only))
parts.append(str(Box.__dataclass_fields__["b"].default_factory is list))
parts.append(str(Box.__dataclass_fields__["b"].kw_only))
parts.append(str(Box.__dataclass_fields__["c"].init))
parts.append(str(Opt.__dataclass_params__.kw_only))

match box:
    case Box(2):
        parts.append("match")
    case _:
        parts.append("miss")

match opt:
    case Opt(value=4):
        parts.append("kw")
    case _:
        parts.append("miss")

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|Box(a=2, c=9)|[7, 2]|9|False|True|True|False|True|match|kw", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Dataclass_FrozenAndOrderBehaveLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import FrozenInstanceError, dataclass

@dataclass(frozen=True, order=True)
class Point:
    x: int
    y: int

p = Point(1, 2)
parts = []
parts.append(str(Point(1, 2) < Point(2, 0)))
parts.append(str(Point(1, 2) <= Point(1, 2)))
parts.append(str(Point(2, 0) > Point(1, 9)))
parts.append(str(Point(2, 0) >= Point(2, 0)))

try:
    p.x = 3
except FrozenInstanceError:
    parts.append("assign")

try:
    del p.x
except FrozenInstanceError:
    parts.append("delete")

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|True|assign|delete", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Dataclass_HelperApis_FieldsAsdictAstupleAndReplaceWork()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import asdict, astuple, dataclass, field, fields, replace

@dataclass
class Child:
    value: int

@dataclass
class Box:
    name: str
    child: Child
    tags: list = field(default_factory=list)
    meta: dict = field(default_factory=dict)
    hidden: int = field(init=False, default=9)

box = Box("alpha", Child(3), tags=["x"], meta={"k": Child(4)})
clone = replace(box, name="beta", tags=["y"])
items = fields(Box)
parts = []
parts.append(str(len(items)))
parts.append(items[0].name)
parts.append(str(items[2].default_factory is list))
parts.append(str(asdict(box)))
parts.append(str(astuple(box)))
parts.append(clone.name + ":" + str(clone.tags) + ":" + str(clone.hidden))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("5|name|True|{'name': 'alpha', 'child': {'value': 3}, 'tags': ['x'], 'meta': {'k': {'value': 4}}, 'hidden': 9}|('alpha', (3,), ['x'], {'k': (4,)}, 9)|beta:['y']:9", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Dataclass_ClassVarInitVarMetadataAndReplaceParityWork()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import InitVar, asdict, astuple, dataclass, field, fields, replace
from typing import ClassVar

@dataclass
class Box:
    label: ClassVar[str] = "box"
    x: int
    y: InitVar[int]
    z: int = field(default=0, metadata={"unit": "px"})

    def __post_init__(self, y):
        self.z = self.x + y

box = Box(2, 3)
clone = replace(box, x=5, y=7)
items = fields(Box)
parts = []
parts.append(Box.label)
parts.append(str(len(items)))
parts.append(items[0].name + ":" + items[1].name)
parts.append(str("label" in Box.__dataclass_fields__))
parts.append(str("y" in Box.__dataclass_fields__))
parts.append(str(items[1].metadata["unit"]))
parts.append(str(box.z))
parts.append(str(asdict(box)))
parts.append(str(astuple(box)))
parts.append(str(clone.z))
try:
    box.y
except AttributeError:
    parts.append("missing")

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("box|2|x:z|True|True|px|5|{'x': 2, 'z': 5}|(2, 5)|12|missing", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Dataclass_UnsafeHashAndFrozenHashAllowDictionaryKeys()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass

@dataclass(unsafe_hash=True)
class Unsafe:
    x: int

@dataclass(frozen=True)
class Frozen:
    x: int

unsafe_dict = {Unsafe(1): "u"}
frozen_dict = {Frozen(2): "f"}
parts = []
parts.append(unsafe_dict[Unsafe(1)])
parts.append(frozen_dict[Frozen(2)])
parts.append(str(Unsafe.__dataclass_params__.unsafe_hash))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("u|f|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Dataclass_DescriptorDefaultsRunSetNameAndDescriptorAwareInit()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass, field

events = []

class Defaulting:
    def __init__(self, default):
        self.default = default

    def __set_name__(self, owner, name):
        events.append(owner.__name__ + ":" + name)
        self.slot = "_" + name

    def __get__(self, obj, owner):
        if obj is None:
            return self.default
        return object.__getattribute__(obj, self.slot)

    def __set__(self, obj, value):
        object.__setattr__(obj, self.slot, value)

@dataclass
class Plain:
    x: int = Defaulting(10)

@dataclass
class Wrapped:
    x: int = field(default=Defaulting(20))

plain = Plain()
wrapped = Wrapped()
parts = []
parts.append(str(events))
parts.append(str(plain.x))
parts.append(str(wrapped.x))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['Plain:x', 'Wrapped:x']|10|20", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ClassDefinition_SupportsStaticMethodClassMethodAndTypeChecks()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    @classmethod
    def kind(cls):
        return cls.label

class Child(Base):
    label = "child"

    @staticmethod
    def join(a, b):
        return a + ":" + b

value = Child()
parts = []
parts.append(Child.join("a", "b"))
parts.append(value.kind())
parts.append(str(isinstance(value, Child)))
parts.append(str(isinstance(value, Base)))
parts.append(str(isinstance(value, (Base, int))))
parts.append(str(issubclass(Child, Base)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("a:b|child|True|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void IsInstance_SupportsBuiltinTypeChecks()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from pathlib import Path

parts = []
parts.append(str(isinstance([1, 2], list)))
parts.append(str(isinstance((1, 2), tuple)))
parts.append(str(isinstance({"a": 1}, dict)))
parts.append(str(isinstance({1, 2}, set)))
parts.append(str(isinstance("x", str)))
parts.append(str(isinstance(b"x", bytes)))
parts.append(str(isinstance(Path("/tmp"), Path)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|True|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void TypeSurfaceAttributes_AndClassInteractions_ArePythonLike()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    pass

class Child(Base):
    pass

item = Child()
parts = []
parts.append(item.__class__.__name__)
parts.append(Child.__class__.__name__)
parts.append(type.__class__.__name__)
parts.append(Child.__qualname__)
parts.append(Child.__base__.__name__)
parts.append(Child.__bases__[0].__name__)
parts.append(Child.__mro__[0].__name__)
parts.append(Child.__mro__[1].__name__)
parts.append(Child.__mro__[2].__name__)
parts.append(str(item.__class__ is Child))
parts.append(str(Child.__class__ is type))
parts.append(str(type.__class__ is type))
parts.append(str(isinstance(Child, type)))
parts.append(str(isinstance(type, type)))
parts.append(str(isinstance(object, type)))
parts.append(type(item).__name__)
parts.append(type(Child).__name__)
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Child|type|type|Child|Base|Base|Child|Base|object|True|True|True|True|True|True|Child|type", host.ReadText("/out.txt"));
    }

    [Fact]
    public void BytesBuiltin_SupportsCopyAndIterableConstruction()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
parts = []
parts.append(str(bytes()))
parts.append(str(bytes([65, 66, 67])))
parts.append(str(bytes(b"xy")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("b''|b'ABC'|b'xy'", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Super_UsesExplicitMroBasedLookupForInstancesAndClassMethods()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    prefix = "base"

    def __init__(self, value):
        self.value = value

    def render(self):
        return self.prefix + ":" + self.value

    @classmethod
    def kind(cls):
        return cls.label

class Child(Base):
    prefix = "child"
    label = "child-type"

    def render(self):
        return super(Child, self).render() + "!"

    @classmethod
    def kind2(cls):
        return super(Child, cls).kind()

item = Child("ok")
__lython_file = open("/out.txt", "w")
__lython_file.write(item.render() + "|" + Child.kind2())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("child:ok!|child-type", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Super_ZeroArgumentFormWorksForMethodsAndClassMethods()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    label = "base"

    def render(self):
        return "base"

    @classmethod
    def kind(cls):
        return cls.label

class Child(Base):
    label = "child"

    def render(self):
        return super().render()

    @classmethod
    def kind2(cls):
        return super().kind()

__lython_file = open("/out.txt", "w")
__lython_file.write(Child().render() + "|" + Child.kind2())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("base|child", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Super_ZeroArgumentFormFailsPreciselyOutsideMethodLikeContexts()
    {
        var result = new LythonEngine().Run(
            """
class Demo:
    @staticmethod
    def bad():
        return super()

Demo.bad()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        var failure = result.Failure;
        Assert.Equal("TypeError", failure?.ExceptionType);
        Assert.Contains("only supported inside instance methods", failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UserDefinedDataDescriptor_SupportsGetSetAndClassAccess()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Descriptor:
    def __get__(self, instance, owner):
        if instance is None:
            return "class-view"
        return instance._value + 1

    def __set__(self, instance, value):
        instance._value = value * 2

class Box:
    score = Descriptor()

    def __init__(self, value):
        self._value = value

box = Box(3)
before = box.score
box.score = 5
after = box.score
__lython_file = open("/out.txt", "w")
__lython_file.write(str(before) + "|" + str(after) + "|" + str(Box.score))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4|11|class-view", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedNonDataDescriptor_YieldsToInstanceAttribute()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Descriptor:
    def __get__(self, instance, owner):
        return "descriptor"

class Box:
    value = Descriptor()

box = Box()
before = box.value
box.value = "instance"
after = box.value
__lython_file = open("/out.txt", "w")
__lython_file.write(before + "|" + after)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("descriptor|instance", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertySetterDecorator_ComposesThroughClassBodyDecoration()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self, value):
        self._value = value

    @property
    def value(self):
        return self._value

    @value.setter
    def value(self, value):
        self._value = value + 10

box = Box(1)
box.value = 5
__lython_file = open("/out.txt", "w")
__lython_file.write(str(box.value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("15", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertyDeleterDecorator_ComposesThroughClassBodyDecoration()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self, value):
        self._value = value

    @property
    def value(self):
        return self._value

    @value.deleter
    def value(self):
        self._value = -1

box = Box(4)
del box.value
__lython_file = open("/out.txt", "w")
__lython_file.write(str(box.value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("-1", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertyGetterDecorator_ReplacesGetterOnExistingProperty()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self, value):
        self._value = value

    @property
    def value(self):
        return -1

    @value.getter
    def value(self):
        return self._value * 3

__lython_file = open("/out.txt", "w")
__lython_file.write(str(Box(4).value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("12", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertyGetterDecorator_CanOverrideBasePropertyFromSubclass()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    def __init__(self, value):
        self._value = value

    @property
    def value(self):
        return self._value

class Child(Base):
    @Base.value.getter
    def value(self):
        return self._value * 4

__lython_file = open("/out.txt", "w")
__lython_file.write(str(Child(3).value) + "|" + str(Base(3).value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("12|3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertyDeleterDecorator_CanOverrideBasePropertyFromSubclass()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    def __init__(self, value):
        self._value = value

    @property
    def value(self):
        return self._value

    @value.deleter
    def value(self):
        self._value = -1

class Child(Base):
    @Base.value.deleter
    def value(self):
        self._value = -9

box = Child(3)
del box.value
__lython_file = open("/out.txt", "w")
__lython_file.write(str(box.value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("-9", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedClass_GetAttrProvidesMissingAttributeFallback()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Dynamic:
    def __getattr__(self, name):
        return "missing:" + name

item = Dynamic()
__lython_file = open("/out.txt", "w")
__lython_file.write(item.value + "|" + item.other)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("missing:value|missing:other", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedClass_GetAttributeCanInterceptOrdinaryAccess()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self):
        object.__setattr__(self, "value", "x")

    def __getattribute__(self, name):
        if name == "value":
            return "wrapped"
        return object.__getattribute__(self, name)

box = Box()
__lython_file = open("/out.txt", "w")
__lython_file.write(box.value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("wrapped", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ObjectGetAttribute_BypassesGetAttrFallback()
    {
        var result = new LythonEngine().Run(
            """
class Box:
    def __getattr__(self, name):
        return "missing:" + name

box = Box()
__lython_file = open("/out.txt", "w")
__lython_file.write(object.__getattribute__(box, "value"))
__lython_file.close()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("AttributeError", result.Failure?.ExceptionType);
        Assert.Contains("'Box' object has no attribute 'value'", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UserDefinedClass_GetAttrStillRunsAfterGetAttributeRaisesAttributeError()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __getattribute__(self, name):
        return object.__getattribute__(self, name)

    def __getattr__(self, name):
        return "fallback:" + name

box = Box()
__lython_file = open("/out.txt", "w")
__lython_file.write(box.value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("fallback:value", host.ReadText("/out.txt"));
    }

    [Fact]
    public void GetAttribute_CanDelegateToObjectAndStillSeeDataDescriptorPrecedence()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Data:
    def __get__(self, instance, owner):
        return "descriptor"

    def __set__(self, instance, value):
        object.__setattr__(instance, "_stored", value)

class Box:
    value = Data()

    def __init__(self):
        object.__setattr__(self, "value", "instance")

    def __getattribute__(self, name):
        return object.__getattribute__(self, name)

__lython_file = open("/out.txt", "w")
__lython_file.write(Box().value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("descriptor", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedClass_SetAttrCanInterceptAndUseObjectSetAttr()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __setattr__(self, name, value):
        object.__setattr__(self, name, str(value) + "!")

box = Box()
box.value = 3
__lython_file = open("/out.txt", "w")
__lython_file.write(box.value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3!", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedClass_DelAttrCanInterceptAndUseObjectDelAttr()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self):
        object.__setattr__(self, "value", "x")

    def __delattr__(self, name):
        object.__delattr__(self, name)
        object.__setattr__(self, "deleted", name + "!")

box = Box()
del box.value
__lython_file = open("/out.txt", "w")
__lython_file.write(box.deleted)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("value!", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ObjectSetAttr_UsesDescriptorSetterWhenPresent()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self):
        self._value = 0

    @property
    def value(self):
        return self._value

    @value.setter
    def value(self, value):
        self._value = value * 5

box = Box()
object.__setattr__(box, "value", 4)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(box.value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("20", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedDescriptor_DeleteParticipatesInAttributeDeletion()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Dropper:
    def __get__(self, instance, owner):
        return "bound"

    def __delete__(self, instance):
        object.__setattr__(instance, "_deleted", "done")

class Box:
    value = Dropper()

    def __init__(self):
        self._deleted = "no"

box = Box()
del box.value
__lython_file = open("/out.txt", "w")
__lython_file.write(box._deleted)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("done", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PropertyDeleteMethod_IsCallableFromClassDescriptorSurface()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self, value):
        self._value = value

    @property
    def value(self):
        return self._value

    @value.deleter
    def value(self):
        self._value = -7

box = Box(4)
Box.value.__delete__(box)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(box.value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("-7", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedDescriptor_SetNameRunsDuringClassCreation()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Named:
    def __set_name__(self, owner, name):
        self.owner = owner
        self.name = name

    def __get__(self, instance, owner):
        return self.owner.__name__ + ":" + self.name

class Box:
    value = Named()

__lython_file = open("/out.txt", "w")
__lython_file.write(Box().value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Box:value", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SetName_IsLookedUpOnTypeNotThroughInstanceGetAttr()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Marker:
    def __getattr__(self, name):
        return "dynamic:" + name

class Box:
    token = Marker()

__lython_file = open("/out.txt", "w")
__lython_file.write(str(Box.token))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("<Marker object>", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SetName_NonCallableFailsDuringClassCreation()
    {
        var result = new LythonEngine().Run(
            """
class Marker:
    __set_name__ = 1

class Box:
    token = Marker()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("__set_name__ must be callable", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InitSubclass_RunsForDerivedClasses()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    initialized = "base"

    def __init_subclass__(cls):
        super().__init_subclass__()
        cls.initialized = cls.__name__

class Child(Base):
    pass

__lython_file = open("/out.txt", "w")
__lython_file.write(Base.initialized + "|" + Child.initialized)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("base|Child", host.ReadText("/out.txt"));
    }

    [Fact]
    public void InitSubclass_ReceivesClassHeaderKeywordArguments()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    def __init_subclass__(cls, label, **kwargs):
        super().__init_subclass__(**kwargs)
        cls.label = label

class Child(Base, label="demo"):
    pass

__lython_file = open("/out.txt", "w")
__lython_file.write(Child.label)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("demo", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SetName_RunsBeforeInitSubclass()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Descriptor:
    def __set_name__(self, owner, name):
        self.owner = owner
        self.name = name

class Base:
    def __init_subclass__(cls):
        super().__init_subclass__()
        cls.owner_name = cls.token.owner.__name__
        cls.token_name = cls.token.name

class Child(Base):
    token = Descriptor()

__lython_file = open("/out.txt", "w")
__lython_file.write(Child.owner_name + ":" + Child.token_name)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Child:token", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ClassHeader_MetaclassKeywordFailsPrecisely()
    {
        var result = new LythonEngine().Run(
            """
class Box(metaclass=object):
    pass
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("metaclass", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NonDescriptor_SetNameRunsDuringClassCreation()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Marker:
    def __set_name__(self, owner, name):
        self.owner_name = owner.__name__
        self.field_name = name

class Box:
    token = Marker()

__lython_file = open("/out.txt", "w")
__lython_file.write(Box.token.owner_name + ":" + Box.token.field_name)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Box:token", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedClass_NewCanAllocateAndInitReceivesReturnedInstance()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Box:
    def __new__(cls, value):
        item = object.__new__(cls)
        object.__setattr__(item, "from_new", value * 2)
        return item

    def __init__(self, value):
        object.__setattr__(self, "from_init", value + 1)

box = Box(4)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(box.from_new) + "|" + str(box.from_init) + "|" + str(isinstance(box, object)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("8|5|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedClass_NewCanReturnForeignValueAndSkipsInit()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Token:
    def __new__(cls, value):
        return "token:" + str(value)

    def __init__(self, value):
        __lython_file = open("/init.txt", "w")
        __lython_file.write("ran")
        __lython_file.close()

item = Token(7)
__lython_file = open("/out.txt", "w")
__lython_file.write(item + "|" + str(isinstance(item, str)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.False(host.Exists("/init.txt"));
        Assert.Equal("token:7|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void UserDefinedClass_NewCanBePlainCallableClassMember()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Token:
    __new__ = str

item = Token()
__lython_file = open("/out.txt", "w")
__lython_file.write(str(isinstance(item, str)) + "|" + str(item == str(Token)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ZeroArgumentSuper_WorksWhenMethodContainsClosure()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Base:
    def render(self):
        return "A"

class Child(Base):
    def render(self):
        def nested():
            return self

        return super().render() + "|" + str(nested() is self)

__lython_file = open("/out.txt", "w")
__lython_file.write(Child().render())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("A|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ZeroArgumentSuper_WorksWhenInheritedMethodIsReboundOnSubclass()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class A:
    def render(self):
        return "A"

class B(A):
    def render(self):
        return super().render() + "B"

class C(B):
    pass

class D(C):
    render = B.render

__lython_file = open("/out.txt", "w")
__lython_file.write(D().render())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("AB", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Super_CanBeShadowedByOrdinaryClassName()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class super:
    msg = "truly super"

class Box:
    def render(self):
        return super().msg

__lython_file = open("/out.txt", "w")
__lython_file.write(Box().render())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("truly super", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Super_WithNonTypeFirstArgument_FailsPrecisely()
    {
        var result = new LythonEngine().Run(
            """
class Box:
    def render(self):
        return super(1, self).render()

Box().render()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("first argument to be a class", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Super_WithMismatchedReceiver_FailsPrecisely()
    {
        var result = new LythonEngine().Run(
            """
class A:
    pass

class B:
    def render(self):
        return super(A, self)

B().render()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("instance to be an instance of the given class or its subclass", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionDecoratorExpressions_AreEvaluatedAsCallables()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def twice(fn):
    def wrapped(value):
        return fn(value) * 2
    return wrapped

@twice
def plus_one(value):
    return value + 1

__lython_file = open("/out.txt", "w")
__lython_file.write(str(plus_one(5)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("12", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ClassDecoratorExpressions_AreEvaluatedAsCallables()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def named(cls):
    cls.label = "decorated"
    return cls

@named
class Box:
    pass

__lython_file = open("/out.txt", "w")
__lython_file.write(Box.label)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("decorated", host.ReadText("/out.txt"));
    }

    [Fact]
    public void BackslashNewlineContinuation_WorksInOrdinaryExpressions()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            "value = 1 + \\\n    2 + \\\n    3\n__lython_file = open(\"/out.txt\", \"w\")\n__lython_file.write(str(value))\n__lython_file.close()\n",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("6", host.ReadText("/out.txt"));
    }

    [Fact]
    public void NumericSemantics_HandleNegativeDivisionModuloAndRangeStep()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
values = []
values.append(str(-7 // 3))
values.append(str(-7 % 3))
values.append(str(7 // -3))
values.append(str(7 % -3))
values.append(str(list(range(5, -1, -2))))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("-3|2|-3|-2|[5, 3, 1]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void StringMethods_CoverSearchWhitespaceAndFormatting()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
text = "  alpha beta  "
vals = []
vals.append(str(text.startswith("  al")))
vals.append(str(text.endswith("  ")))
vals.append(str(text.find("beta")))
vals.append(str(text.strip()))
vals.append(str(text.lstrip()))
vals.append(str(text.rstrip()))
vals.append(str("a  b\tc".split()))
vals.append(str("a,,b".split(",")))
vals.append(str("x\ny\r\nz".splitlines()))
vals.append(str("x\ny\r\nz".splitlines(True)))
vals.append("{1}:{0}".format("left", "right"))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal(
            "True|True|8|alpha beta|alpha beta  |  alpha beta|['a', 'b', 'c']|['a', '', 'b']|['x', 'y', 'z']|['x\\n', 'y\\r\\n', 'z']|right:left",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void UnicodeStrings_UsePythonLikeCharacterSemantics()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
text = "a😀b"
vals = []
vals.append(str(len(text)))
vals.append(text[1])
vals.append(text[1:2])
vals.append(str(text.find("😀")))
vals.append(str(text.count("😀")))
vals.append(str(list(text)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(
            result.Success,
            result.Failure?.Message ??
            string.Join(" | ", result.Diagnostics.Select(d => d.Span is null ? d.Message : $"{d.Message} @ {d.Span.Line}:{d.Span.Column}")));
        Assert.Null(result.Failure);
        Assert.Equal("3|😀|😀|1|1|['a', '😀', 'b']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Format_PreservesUnicodeWithoutUtf16ShadowState()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
template = "😀 {1} {0}"
__lython_file = open("/out.txt", "w")
__lython_file.write(template.format("café", "élan"))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("😀 élan café", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Utf8NativeStringHelpers_HandleSearchReplaceAndSplit()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
text = "é😀é"
vals = []
vals.append(str(text.startswith("é😀")))
vals.append(str(text.endswith("😀é")))
vals.append(str("😀" in text))
vals.append(str(text.find("😀")))
vals.append(str(text.count("é")))
vals.append(text.replace("😀", "x"))
vals.append(str("  é\t😀 \n".split()))
vals.append(str("é😀é".split("😀")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("True|True|True|1|2|éxé|['é', '😀']|['é', 'é']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OsPathHelpers_HandleUnicodeSegments()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import os
vals = []
vals.append(os.path.join("/src", "é😀.txt"))
vals.append(os.path.dirname("/src/é😀.txt"))
vals.append(os.path.basename("/src/é😀.txt"))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("/src/é😀.txt|/src|é😀.txt", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Globals_StringValues_AreNormalizedToPyString()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import os
__lython_file = open("/out.txt", "w")
__lython_file.write(value.replace("é", "x") + "|" + str("😀" in value) + "|" + os.path.basename(path))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Globals = new Dictionary<string, object?>
                {
                    ["value"] = "é😀é",
                    ["path"] = "/tmp/é😀.txt"
                }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("x😀x|True|é😀.txt", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Utf8NativeTextHelpers_HandleTrimAndSplitlinesCases()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
vals.append(" \t😀 é \n".strip())
vals.append(" \t😀 é \n".lstrip())
vals.append(" \t😀 é \n".rstrip())
vals.append(str("a\r\nb\rc\n".splitlines()))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("😀 é|😀 é \n| \t😀 é|['a', 'b', 'c']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Utf8NativeRendering_HandlesNestedCollections()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
value = {"name": "é😀", "items": ["α", "β"], "pair": ("x", 2)}
__lython_file = open("/out.txt", "w")
__lython_file.write(str(value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("{'name': 'é😀', 'items': ['α', 'β'], 'pair': ('x', 2)}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Globals_NestedStrings_AreNormalizedRecursively()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
__lython_file = open("/out.txt", "w")
__lython_file.write(values[0].replace("é", "x") + "|" + data["path"] + "|" + str("é" in items))
__lython_file.close()
""",
            host,
            new LythonRunOptions
            {
                Globals = new Dictionary<string, object?>
                {
                    ["values"] = new List<object?> { "é😀é" },
                    ["data"] = new Dictionary<string, object?> { ["path"] = "/tmp/é😀.txt" },
                    ["items"] = new HashSet<object?> { "a", "é" }
                }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("x😀x|/tmp/é😀.txt|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PublicReturnValues_NormalizeInternalPyStrings()
    {
        var result = new LythonEngine().Run(
            """
value = "a😀b"
return value
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.IsType<string>(result.ReturnValue);
        Assert.Equal("a😀b", (string?)result.ReturnValue);
    }

    [Fact]
    public void ListAndDictionaryMethods_SupportMutationAndDefaults()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
items = [1]
items.append(2)
items.extend((3, 4))
last = items.pop()
d = {"a": 1}
missing = d.get("z", 99)
d.update({"b": 2})
popped = d.pop("a")
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + str(last) + "|" + str(missing) + "|" + str(popped) + "|" + str(list(d.items())))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("[1, 2, 3]|4|99|1|[('b', 2)]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Dictionaries_SupportHashableNonStringKeysAcrossViewsEqualityAndMembership()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
d = {1: "one", (2, 3): "pair", True: "bool"}
same = d[1]
pair = d[(2, 3)]
keys = list(d.keys())
items = list(d.items())
copy = dict(d)
equal = copy == d
contains_int = 1 in d
contains_tuple = (2, 3) in d
__lython_file = open("/out.txt", "w")
__lython_file.write(str(same) + "|" + str(pair) + "|" + str(keys) + "|" + str(items) + "|" + str(equal) + "|" + str(contains_int) + "|" + str(contains_tuple))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("bool|pair|[1, (2, 3)]|[(1, 'bool'), ((2, 3), 'pair')]|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DictionaryComprehensionsAndUpdate_WorkWithTupleAndNumericKeys()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
base = {(n, n + 1): n for n in [1, 2]}
extra = {0: "zero"}
base.update(extra)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(base[(1, 2)]) + "|" + str(base[(2, 3)]) + "|" + str(base[0]) + "|" + str(list(base.keys())))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("1|2|zero|[(1, 2), (2, 3), 0]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void PublicReturnValues_NormalizePyDictionariesWithNonStringKeys()
    {
        var result = new LythonEngine().Run(
            """
return {1: "one", (2, 3): "pair"}
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        var dict = Assert.IsType<Dictionary<object, object?>>(result.ReturnValue);
        Assert.Equal(2, dict.Count);
        Assert.Equal("one", dict[new BigInteger(1)]);
        Assert.Contains(
            dict,
            pair => pair.Value?.Equals("pair") == true &&
                    pair.Key is object?[] tuple &&
                    tuple.Length == 2 &&
                    tuple[0] is BigInteger first && first == new BigInteger(2) &&
                    tuple[1] is BigInteger second && second == new BigInteger(3));
    }

    [Fact]
    public void AdditionalCollectionAndStringErgonomics_FollowPythonLikeSemantics()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
items = [1, 2]
items_copy = items.copy()
items.clear()
d = {"a": 1}
same = d.setdefault("a", 9)
new_value = d.setdefault("b", [])
new_value.append(3)
d_copy = d.copy()
d.clear()
vals = []
vals.append(str(items_copy))
vals.append(str(items))
vals.append(str(same))
vals.append(str(d_copy["b"]))
vals.append(str(d))
vals.append(str("banana".count("na")))
vals.append("spam".removeprefix("sp"))
vals.append("spam".removesuffix("am"))
vals.append(str("key=value".partition("=")))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("[1, 2]|[]|1|[3]|{}|2|am|sp|('key', '=', 'value')", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Builtins_SortedAnyAllMinMaxAndEnumerateFollowPythonLikeSemantics()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
vals.append(str(sorted([3, 1, 2])))
vals.append(str(sorted(("b", "a", "c"))))
vals.append(str(any([0, "", 3])))
vals.append(str(any([])))
vals.append(str(all([1, "x", True])))
vals.append(str(all([1, 0, True])))
vals.append(str(min([3, 1, 2])))
vals.append(str(max(["b", "a", "c"])))
vals.append(str(list(enumerate(["a", "b"], -2))))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("[1, 2, 3]|['a', 'b', 'c']|True|False|True|False|1|c|[(-2, 'a'), (-1, 'b')]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Builtins_ReprAndSumCoverCommonScratchScriptPatterns()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
line = " a\t\n"
vals = []
vals.append(repr(line))
vals.append(repr(["a", 2, None, True]))
vals.append(repr(("x",)))
vals.append(repr({"b": [1, "two"]}))
vals.append(repr({"z", "a"}))
vals.append(f"{line!r}")
vals.append(str(sum([1, 2, 3])))
vals.append(str(sum((1, 2), 10)))
vals.append(str(sum(x for x in range(4))))
vals.append(str(sum([1.5, 2])))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("' a\\t\\n'|['a', 2, None, True]|('x',)|{'b': [1, 'two']}|{'a', 'z'}|' a\\t\\n'|6|13|6|3.5", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Builtins_CommonExceptionTypesCanBeNamedRaisedAndCaught()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
try:
    len(1)
except TypeError as err:
    vals.append(err.type)

try:
    raise ImportError("missing")
except (ImportError, NameError) as err:
    vals.append(err.type + ":" + err.message)

try:
    raise AttributeError("attr")
except AttributeError as err:
    vals.append(err.type + ":" + err.message)

try:
    raise FileNotFoundError("gone")
except FileNotFoundError as err:
    vals.append(err.type + ":" + err.message)

try:
    raise OSError("os")
except OSError as err:
    vals.append(err.type + ":" + err.message)

try:
    raise StopIteration("done")
except StopIteration as err:
    vals.append(err.type + ":" + err.message)

try:
    raise ZeroDivisionError("zero")
except ZeroDivisionError as err:
    vals.append(err.type + ":" + err.message)

try:
    raise NotImplementedError("missing implementation")
except NotImplementedError as err:
    vals.append(err.type + ":" + err.message)

try:
    raise RuntimeError()
except RuntimeError as err:
    vals.append(err.type + ":" + err.message)

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("TypeError|ImportError:missing|AttributeError:attr|FileNotFoundError:gone|OSError:os|StopIteration:done|ZeroDivisionError:zero|NotImplementedError:missing implementation|RuntimeError:", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ConstructorsAndDictionaryViews_HandleEmptyAndCopyCases()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
d = dict([("b", 2), ("a", 1)])
copy = dict(d)
vals = []
vals.append(str(list()))
vals.append(str(tuple()))
vals.append(str(dict()))
vals.append(str(copy.get("missing")))
vals.append(str(len(list(copy.keys()))))
vals.append(str(list(copy.values())))
vals.append(str(list(copy.items())))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("[]|()|{}|None|2|[2, 1]|[('b', 2), ('a', 1)]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void AugmentedAssignment_SupportsAccumulatorStyleScripts()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
total = 1
total += 2
total *= 5
total -= 4
total //= 2
total %= 3
text = "a"
text += "b"
items = [1]
alias = items
items += [2, 3]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(total) + "|" + text + "|" + str(items) + "|" + str(alias))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("2|ab|[1, 2, 3]|[1, 2, 3]", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
text = "ha"
text *= 3
__lython_file = open("/out.txt", "w")
__lython_file.write(text)
__lython_file.close()
""",
        "hahaha")]
    [InlineData(
        """
count = 10
count += 5
count -= 3
__lython_file = open("/out.txt", "w")
__lython_file.write(str(count))
__lython_file.close()
""",
        "12")]
    [InlineData(
        """
count = 17
count //= 3
count %= 4
__lython_file = open("/out.txt", "w")
__lython_file.write(str(count))
__lython_file.close()
""",
        "1")]
    [InlineData(
        """
items = [1, 2]
alias = items
items += [3]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + str(alias))
__lython_file.close()
""",
        "[1, 2, 3]|[1, 2, 3]")]
    public void AugmentedAssignment_CoversAdditionalSupportedCases(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("missing += 1\n", "NameError", "is not defined")]
    [InlineData("items = [1]\nitems -= [1]\n", "TypeError", "Operands are not compatible with '-'")]
    public void AugmentedAssignment_FailureCasesArePinned(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Comprehensions_SupportSimpleForAndOptionalIfWithoutLeakingLoopVariable()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
x = "outer"
values = [n * 2 for n in range(6) if n % 2 == 1]
pairs = {"k" + str(n): n * n for n in range(5) if n < 3}
__lython_file = open("/out.txt", "w")
__lython_file.write(str(values) + "|" + str(pairs) + "|" + x)
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("[2, 6, 10]|{'k0': 0, 'k1': 1, 'k2': 4}|outer", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
values = [n + 1 for n in [1, 2, 3]]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(values))
__lython_file.close()
""",
        "[2, 3, 4]")]
    [InlineData(
        """
mapping = {ch: ch.upper() for ch in "ab"}
__lython_file = open("/out.txt", "w")
__lython_file.write(str(mapping))
__lython_file.close()
""",
        "{'a': 'A', 'b': 'B'}")]
    [InlineData(
        """
letters = [ch for ch in "egg"]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(letters))
__lython_file.close()
""",
        "['e', 'g', 'g']")]
    [InlineData(
        """
mapping = {"k" + str(n % 2): n for n in [1, 2, 3, 4]}
__lython_file = open("/out.txt", "w")
__lython_file.write(str(mapping))
__lython_file.close()
""",
        "{'k1': 3, 'k0': 4}")]
    [InlineData(
        """
outer = "kept"
values = [n for n in [0, 1, 2] if n]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(values) + "|" + outer)
__lython_file.close()
""",
        "[1, 2]|kept")]
    [InlineData(
        """
values = [n for n in ["", "x", 0, 3] if n]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(values))
__lython_file.close()
""",
        "['x', 3]")]
    public void Comprehensions_CoverAdditionalPortableShapes(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Fact]
    public void Slicing_SupportsStringsListsTuplesAndSteps()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
text = "abcdef"
items = [0, 1, 2, 3, 4]
parts = ("a", "b", "c", "d")
values = []
values.append(text[1:5])
values.append(text[::-2])
values.append(str(items[1:4]))
values.append(str(items[::-1]))
values.append(str(parts[:3]))
values.append(str(parts[3:0:-2]))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("bcde|fdb|[1, 2, 3]|[4, 3, 2, 1, 0]|('a', 'b', 'c')|('d', 'b')", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
text = "alpha"
__lython_file = open("/out.txt", "w")
__lython_file.write(text[:])
__lython_file.close()
""",
        "alpha")]
    [InlineData(
        """
text = "alpha"
__lython_file = open("/out.txt", "w")
__lython_file.write(text[:0])
__lython_file.close()
""",
        "")]
    [InlineData(
        """
text = "alpha"
__lython_file = open("/out.txt", "w")
__lython_file.write(text[100:-100])
__lython_file.close()
""",
        "")]
    [InlineData(
        """
text = "alpha"
__lython_file = open("/out.txt", "w")
__lython_file.write(text[-100:100])
__lython_file.close()
""",
        "alpha")]
    [InlineData(
        """
items = [0, 1, 2, 3, 4, 5]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items[::2]))
__lython_file.close()
""",
        "[0, 2, 4]")]
    [InlineData(
        """
items = [0, 1, 2, 3, 4, 5]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items[1::2]))
__lython_file.close()
""",
        "[1, 3, 5]")]
    [InlineData(
        """
items = [0, 1, 2, 3]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items[::-1]))
__lython_file.close()
""",
        "[3, 2, 1, 0]")]
    [InlineData(
        """
items = [0, 1, 2, 3]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items[::-100]))
__lython_file.close()
""",
        "[3]")]
    [InlineData(
        """
parts = ("a", "b", "c", "d")
__lython_file = open("/out.txt", "w")
__lython_file.write(str(parts[3:3:-2]))
__lython_file.close()
""",
        "()")]
    [InlineData(
        """
items = [10, 20, 30]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items[True:]))
__lython_file.close()
""",
        "[20, 30]")]
    [InlineData(
        """
items = [10, 20, 30]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items[::True]))
__lython_file.close()
""",
        "[10, 20, 30]")]
    public void Slicing_CoversBoundaryNormalizationAndStepShapes(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("text = \"alpha\"\nwrite_text(\"/out.txt\", text[::0])\n", "LA3120", "slice step cannot be zero")]
    [InlineData("text = \"alpha\"\nwrite_text(\"/out.txt\", text[1:\"x\"])\n", "LA3119", "Slice indices must be integers or None")]
    public void Slicing_StaticFailureCasesAreRejectedAtCompileTime(string source, string diagnosticCode, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == diagnosticCode && diagnostic.Message.Contains(messageFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void ConditionalExpressions_DefaultParameters_KeywordArguments_AndUnpacking_WorkTogether()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def render(name, suffix = "!", loud = False):
    label = name.upper() if loud else name.lower()
    return label + suffix

first, second = ["Alpha", "Beta"]
text = render(first)
text = text + "|" + render(second, loud = True, suffix = "?")
__lython_file = open("/out.txt", "w")
__lython_file.write(text + "|" + first + "|" + second)
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("alpha!|BETA?|Alpha|Beta", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
value = "left" if True else "right"
__lython_file = open("/out.txt", "w")
__lython_file.write(value)
__lython_file.close()
""",
        "left")]
    [InlineData(
        """
value = "left" if 0 else "right"
__lython_file = open("/out.txt", "w")
__lython_file.write(value)
__lython_file.close()
""",
        "right")]
    [InlineData(
        """
value = "one" if False else "two" if True else "three"
__lython_file = open("/out.txt", "w")
__lython_file.write(value)
__lython_file.close()
""",
        "two")]
    [InlineData(
        """
values = [("odd" if n % 2 else "even") for n in [1, 2, 3]]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(values))
__lython_file.close()
""",
        "['odd', 'even', 'odd']")]
    public void ConditionalExpressions_CoverCommonCompositions(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
def render(name, suffix = "!"):
    return name + suffix

__lython_file = open("/out.txt", "w")
__lython_file.write(render("a"))
__lython_file.close()
""",
        "a!")]
    [InlineData(
        """
def render(name, suffix = "!", loud = False):
    return name.upper() if loud else name.lower()

__lython_file = open("/out.txt", "w")
__lython_file.write(render("AbC", loud = True))
__lython_file.close()
""",
        "ABC")]
    [InlineData(
        """
def render(name, suffix = "!", loud = False):
    base = name.upper() if loud else name.lower()
    return base + suffix

__lython_file = open("/out.txt", "w")
__lython_file.write(render("AbC", "?", loud = False))
__lython_file.close()
""",
        "abc?")]
    [InlineData(
        """
def render(name, suffix = "!", loud = False):
    base = name.upper() if loud else name.lower()
    return base + suffix

__lython_file = open("/out.txt", "w")
__lython_file.write(render(name = "AbC", suffix = "?", loud = True))
__lython_file.close()
""",
        "ABC?")]
    public void DefaultParametersAndKeywordArguments_CoverPortableCallShapes(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
def render(name, suffix = "!", loud = False):
    return name + suffix

render()
""",
        "missing required argument 'name'")]
    [InlineData(
        """
def render(name, suffix = "!"):
    return name + suffix

render("x", tone = "?")
""",
        "unexpected keyword 'tone'")]
    [InlineData(
        """
def render(name, suffix = "!"):
    return name + suffix

render("x", name = "y")
""",
        "duplicate binding for 'name'")]
    public void UserFunctionKeywordBindingFailures_ArePinned(string source, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal("TypeError", result.Failure.ExceptionType);
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(
        """
a, b = (1, 2)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(a) + "|" + str(b))
__lython_file.close()
""",
        "1|2")]
    [InlineData(
        """
a, b = ["x", "y"]
__lython_file = open("/out.txt", "w")
__lython_file.write(a + "|" + b)
__lython_file.close()
""",
        "x|y")]
    [InlineData(
        """
a, b, c = "egg"
__lython_file = open("/out.txt", "w")
__lython_file.write(a + "|" + b + "|" + c)
__lython_file.close()
""",
        "e|g|g")]
    public void UnpackingAssignment_SupportsPortableIterables(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Fact]
    public void LoopAndComprehensionDestructuring_SupportsPairShapedIterables()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
pairs = [("a", 1), ("b", 2)]
parts = []
for key, value in pairs:
    parts.append(key + str(value))

mapping = {key: value for key, value in pairs}
__lython_file = open("/out.txt", "w")
__lython_file.write(str(parts) + "|" + str(mapping))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("['a1', 'b2']|{'a': 1, 'b': 2}", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("a, b = [1]\n", "LA3030", "wrong number of values")]
    [InlineData("a, b = 1\n", "LA3031", "Object is not iterable")]
    public void UnpackingAssignment_LiteralFailureCasesAreRejectedAtCompileTime(string source, string diagnosticCode, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == diagnosticCode && diagnostic.Message.Contains(messageFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void WithStatement_SupportsOpenReadAndWrite()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "hello");

        var result = new LythonEngine().Run(
            """
with open("/input.txt", "r") as reader:
    text = reader.read()

with open("/output.txt", "w") as writer:
    writer.write(text.upper())
    writer.write("!")
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("HELLO!", host.ReadText("/output.txt"));
    }

    [Theory]
    [InlineData(
        """
with open("/output.txt", "w") as writer:
    count = writer.write("alpha")

__lython_file = open("/out.txt", "w")
__lython_file.write(str(count) + "|" + open("/output.txt").read())
__lython_file.close()
""",
        "5|alpha")]
    [InlineData(
        """
__lython_file = open("/append.txt", "w")
__lython_file.write("base")
__lython_file.close()
with open("/append.txt", "a") as handle:
    handle.write("-extra")

__lython_file = open("/out.txt", "w")
__lython_file.write(open("/append.txt").read())
__lython_file.close()
""",
        "base-extra")]
    [InlineData(
        """
__lython_file = open("/seed.txt", "w")
__lython_file.write("hello")
__lython_file.close()
with open("/seed.txt") as handle:
    text = handle.read()

__lython_file = open("/out.txt", "w")
__lython_file.write(text)
__lython_file.close()
""",
        "hello")]
    [InlineData(
        """
with open("/sample.txt", "w") as handle:
    handle.write("x")

try:
    handle.write("y")
except ValueError as err:
    __lython_file = open("/out.txt", "w")
    __lython_file.write(err.message)
    __lython_file.close()
""",
        "I/O operation on closed file")]
    [InlineData(
        """
try:
    with open("/sample.txt", "w") as handle:
        handle.write("alpha")
        raise ValueError("boom")
except ValueError as err:
    __lython_file = open("/out.txt", "w")
    __lython_file.write(open("/sample.txt").read() + "|" + err.message)
    __lython_file.close()
""",
        "alpha|boom")]
    public void WithStatement_CoversHandleLifecycleModesAndFlushBehavior(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
handle = open("/output.txt", "w")
handle.write("alpha")
closed = handle.close()
__lython_file = open("/out.txt", "w")
__lython_file.write(str(closed) + "|" + open("/output.txt").read())
__lython_file.close()
""",
        "None|alpha")]
    [InlineData(
        """
__lython_file = open("/append.txt", "w")
__lython_file.write("base")
__lython_file.close()
handle = open("/append.txt", "a")
handle.write("-extra")
handle.close()
handle.close()
__lython_file = open("/out.txt", "w")
__lython_file.write(open("/append.txt").read())
__lython_file.close()
""",
        "base-extra")]
    [InlineData(
        """
__lython_file = open("/input.txt", "w")
__lython_file.write("alpha")
__lython_file.close()
handle = open("/input.txt", "r")
handle.close()
try:
    handle.read()
except ValueError as err:
    __lython_file = open("/out.txt", "w")
    __lython_file.write(err.message)
    __lython_file.close()
""",
        "I/O operation on closed file")]
    [InlineData(
        """
handle = open("/output.txt", "w")
handle.close()
try:
    handle.write("alpha")
except ValueError as err:
    __lython_file = open("/out.txt", "w")
    __lython_file.write(err.message)
    __lython_file.close()
""",
        "I/O operation on closed file")]
    public void FileClose_CoversManualLifecycleModesAndClosedHandleErrors(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Fact]
    public void FormattedStrings_SupportInterpolationAndEscapedBraces()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
name = "alpha"
count = 3
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{name}-{count}-{{ok}}")
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("alpha-3-{ok}", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
name = "alpha"
count = 3
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{name}-{count}")
__lython_file.close()
""",
        "alpha-3")]
    [InlineData(
        """
name = "alpha"
count = 3
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{name}-{count}-{name.upper()}")
__lython_file.close()
""",
        "alpha-3-ALPHA")]
    [InlineData(
        """
value = 2
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{value + 5}")
__lython_file.close()
""",
        "7")]
    [InlineData(
        """
flag = True
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{'yes' if flag else 'no'}")
__lython_file.close()
""",
        "yes")]
    [InlineData(
        """
values = [10, 20, 30]
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{values[1:3]}")
__lython_file.close()
""",
        "[20, 30]")]
    [InlineData(
        """
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{ {'answer': 42}['answer'] }")
__lython_file.close()
""",
        "42")]
    [InlineData(
        """
__lython_file = open("/out.txt", "w")
__lython_file.write(f"{{left}}-{1 + 1}-{{right}}")
__lython_file.close()
""",
        "{left}-2-{right}")]
    public void FormattedStrings_CoverMultipleExpressionsAndEmbeddedSubsetSyntax(string source, string expected)
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(source, host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal(expected, host.ReadText("/out.txt"));
    }

    [Fact]
    public void FormattedStrings_FormatLineNumberDiagnosticsLikePython()
    {
        var host = new MockLythonHost();
        host.SeedFile("/project/script.nvn", "zero\none\ntwo\nthree\n");

        var result = new LythonEngine().Run(
            """
from pathlib import Path

path = Path("/project/script.nvn")
lines = path.read_text().splitlines()

for i in range(1, 3):
    print(f"{i + 1:5d}: {lines[i]}")
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("    2: one\n    3: two\n", result.StandardOutput);
    }

    [Fact]
    public void FormattedStrings_CoverCommonFormatSpecifiersAndConversions()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
parts = []
parts.append(f"{15:04d}")
parts.append(f"{-15:05d}")
parts.append(f"{15:#x}")
parts.append(f"{12345:,d}")
parts.append(f"{2.5:.2f}")
parts.append(f"{0.125:.1%}")
parts.append(f"{'xy':>4s}")
parts.append(f"{'x':05}")
parts.append(f"{'abcdef':.3s}")
parts.append(f"{3:<4}!")
parts.append(f"{15!s:>4}")
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("0015|-0015|0xf|12,345|2.50|12.5%|  xy|x0000|abc|3   !|  15", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FormattedStrings_SupportDebugExpressionsAndNestedFormatFields()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
value = "a"
width = 5
parts = [f"{value=}", f"{value=:>{width}}", f"{ width = }"]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("value='a'|value=    a| width = 5", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("f\"{\"\n")]
    [InlineData("f\"}\"\n")]
    [InlineData("f\"{}\"\n")]
    public void FormattedStrings_InvalidShapes_ReportCompileDiagnostic(string source)
    {
        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        Assert.Contains(
            compiled.Diagnostics,
            diagnostic => diagnostic.Code == "LA1007" &&
                          diagnostic.Message.Contains("Invalid string literal.", StringComparison.Ordinal));
    }

    [Fact]
    public void ListAndTuple_ConstructorsIterateStrings()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
vals.append(str(list("eggs")))
vals.append(str(tuple("ab")))
items = [1]
items.extend("xy")
vals.append(str(items))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("['e', 'g', 'g', 's']|('a', 'b')|[1, 'x', 'y']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void MultiLineListsIterationAppendIndexingAndComprehensions_WorkLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
files = [
    "/input/A/Parts.csv",
    "/input/B/Parts.csv",
    "/input/C/Parts.csv",
]

markers = []
for path in files:
    markers.append(path[7] + ":" + str(files[0] == "/input/A/Parts.csv"))

selected = [
    path
    for path in files
    if path != "/input/B/Parts.csv"
]

__lython_file = open("/out.txt", "w")
__lython_file.write(str(files[0]) + "|" + str(files[:2]) + "|" + str(markers) + "|" + str(selected))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("/input/A/Parts.csv|['/input/A/Parts.csv', '/input/B/Parts.csv']|['A:True', 'B:True', 'C:True']|['/input/A/Parts.csv', '/input/C/Parts.csv']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void MultiLineCollectionVariantsAndPatterns_RunLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
values = (
    [
        1,
        2,
        3,
    ],
)

numbers = values[
    0
]

picked = numbers[
    1
]

window = numbers[
    1:
    3
]

pairs = [
    (
        name,
        value,
    )
    for name, value in [
        (
            "a",
            1,
        ),
        (
            "b",
            2,
        ),
    ]
    if (
        value
        > 1
    )
]

row = {
    "pairs": pairs,
    "window": window,
}

seen = {
    "a",
    "b",
    "a",
}

parts = []
match [
    "head",
    "tail",
]:
    case [
        first,
        second,
    ]:
        parts.append(first)
        parts.append(second)
    case _:
        parts.append("miss")

parts.append(str(picked))
parts.append(str(row["pairs"]))
parts.append(str(row["window"]))
parts.append(str(len(seen)))
parts.append(str("b" in seen))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("head|tail|2|[('b', 2)]|[2, 3]|2|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DictUpdate_OverwritesValuesWithoutChangingExistingKeyOrder()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
d = {"a": 1, "b": 2}
d.update({"b": 20, "c": 3})
__lython_file = open("/out.txt", "w")
__lython_file.write(str(list(d.items())) + "|" + str(d.get("b")) + "|" + str(d.get("missing", 99)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("[('a', 1), ('b', 20), ('c', 3)]|20|99", host.ReadText("/out.txt"));
    }

    [Fact]
    public void EqualityMembershipAndComparison_FollowSupportedPythonLikeSemantics()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
vals.append(str([1, 2] == [1, 2]))
vals.append(str((1, "a") == (1, "a")))
vals.append(str({"a": 1} == {"a": 1}))
vals.append(str(2 == 2.0))
vals.append(str("bc" in "abcd"))
vals.append(str("" in "abcd"))
vals.append(str("a" in {"a": 1, "b": 2}))
vals.append(str(2 in [1, 2, 3]))
vals.append(str([1, 2] != [2, 1]))
vals.append(str("b" > "a"))
vals.append(str(3 >= 3.0))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True|True|True|True|True|True|True|True|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ChainedComparisons_EvaluateLikePythonAndReuseMiddleValue()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
seen = []
def mid():
    seen.append("x")
    return 2

vals = []
vals.append(str(0 < mid() < 3))
vals.append(str(3 < mid() < 4))
vals.append(str("a" < "b" < "c"))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals) + "|" + str(len(seen)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True|False|True|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void AssertStatement_SupportsSuccessAndMessageFailure()
    {
        var successHost = new MockLythonHost();
        var success = new LythonEngine().Run(
            """
assert 1 < 2
__lython_file = open("/out.txt", "w")
__lython_file.write("ok")
__lython_file.close()
""",
            successHost);

        Assert.True(success.Success);
        Assert.Null(success.Failure);
        Assert.Equal("ok", successHost.ReadText("/out.txt"));

        var failure = new LythonEngine().Run(
            """
assert 1 > 2, "bad guard"
""",
            new MockLythonHost());

        Assert.False(failure.Success);
        Assert.NotNull(failure.Failure);
        Assert.Equal("AssertionError", failure.Failure?.ExceptionType);
        Assert.Contains("bad guard", failure.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExponentiationBitwiseAndAnnotatedAssignment_WorkLikePythonSubset()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
value: int = 2 ** 3
mask = (5 | 2) ^ 1
shifted = (1 << 4) >> 2
flipped = ~1
__lython_file = open("/out.txt", "w")
__lython_file.write(str(value) + "|" + str(mask) + "|" + str(shifted) + "|" + str(flipped))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("8|6|4|-2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void WithStatement_SupportsMultipleContextManagers()
    {
        var host = new MockLythonHost();
        host.SeedFile("/left.txt", "L");
        host.SeedFile("/right.txt", "R");

        var result = new LythonEngine().Run(
            """
with open("/left.txt", "r") as left, open("/right.txt", "r") as right:
    __lython_file = open("/out.txt", "w")
    __lython_file.write(left.read() + right.read())
    __lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("LR", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SemicolonDelimitedSimpleStatements_AreParsedLikeSeparateLines()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            "a = 1; b = 2; __lython_file = open(\"/out.txt\", \"w\"); __lython_file.write(str(a + b)); __lython_file.close()\n",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OneLineSuites_HandleCommonCompoundStatements()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
if True: vals.append("if")
else: vals.append("bad")
if False: vals.append("bad")
else: vals.append("else")
for item in [1, 2]: vals.append("f" + str(item)); vals.append("tail")
count = 0
while count < 2: vals.append("w" + str(count)); count += 1
try: raise ValueError("bad")
except ValueError: vals.append("caught")
try: vals.append("try")
except ValueError: vals.append("bad")
else: vals.append("try-else")
finally: vals.append("finally")
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("if|else|f1|tail|f2|tail|w0|w1|caught|try|try-else|finally", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OneLineSuites_HandleTryExceptContinueScratchPattern()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
for text in ["1", "x", "2"]:
    try: value = int(text)
    except ValueError: continue
    vals.append(str(value))
if vals: __lython_file = open("/out.txt", "w"); __lython_file.write(",".join(vals)); __lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("1,2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OneLineSuites_HandleDefinitionsWithAndMatchCaseBodies()
    {
        var host = new MockLythonHost();
        host.SeedFile("/source.txt", "L");

        var result = new LythonEngine().Run(
            """
def inc(value): return value + 1
class Box: label = "box"
with open("/source.txt", "r") as handle: text = handle.read()
match inc(1):
    case 1: text = text + "bad"
    case 2: text = text + Box.label
__lython_file = open("/out.txt", "w")
__lython_file.write(text)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("Lbox", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ChainedAssignment_EvaluatesRightHandSideOnce()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
box = []
def produce():
    box.append("x")
    return len(box)

a = b = produce()
__lython_file = open("/out.txt", "w")
__lython_file.write(str(a) + "|" + str(b) + "|" + str(len(box)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("1|1|1", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ChainedAssignment_SupportsSimpleAndSubscriptTargets()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
items = [0, 0]
items[0] = value = 42
left = right = [1, 2]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + str(value) + "|" + str(left) + "|" + str(right))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("[42, 0]|42|[1, 2]|[1, 2]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void MatchStatement_SupportsLiteralSequenceMappingAndGuardPatterns()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
parts = []

value = [1, 2, 3]
match value:
    case [first, *rest] if first == 1:
        parts.append(str(rest))
    case _:
        parts.append("miss")

row = {"kind": "file", "path": "/tmp/a.txt", "mode": "w"}
match row:
    case {"kind": "file", "path": path, **rest}:
        parts.append(path)
        parts.append(str(rest))

score = 1
match score:
    case 0 | 1 as small:
        parts.append(str(small))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("[2, 3]|/tmp/a.txt|{'mode': 'w'}|1", host.ReadText("/out.txt"));
    }

    [Fact]
    public void MatchStatement_SupportsClassPatterns_OnRuntimeOwnedTypes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import pathlib

parts = []
p = pathlib.Path("/docs/guide.md")

match p:
    case pathlib.Path(name=name, suffix=suffix):
        parts.append(name)
        parts.append(suffix)

match [10, 20]:
    case list(left, right):
        parts.append(str(left + right))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("guide.md|.md|30", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SubscriptAssignment_SupportsListsAndDictionaries()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
items = [1, 2, 3]
items[1] = 20
row = {"name": "old"}
row["name"] = "new"
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + str(row))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("[1, 20, 3]|{'name': 'new'}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DeleteStatement_HandlesNamesAndSubscripts()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
value = 1
del value
items = [1, 2, 3]
del items[1]
row = {"name": "x", "age": 2}
del row["age"]
__lython_file = open("/out.txt", "w")
__lython_file.write(str(items) + "|" + str(row))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("[1, 3]|{'name': 'x'}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void TryElseBareExceptAndExceptionTuple_WorkLikePythonSubset()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
try:
    vals.append("try")
except:
    vals.append("except")
else:
    vals.append("else")

try:
    raise ValueError("bad")
except (RuntimeError, ValueError) as err:
    vals.append(err.message)

try:
    raise RuntimeError("boom")
except:
    vals.append("bare")

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("try|else|bad|bare", host.ReadText("/out.txt"));
    }

    [Fact]
    public void LambdaAndSortedKeyReverse_WorkLikePythonSubset()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
rows = [("b", 2), ("a", 3), ("c", 1)]
f = lambda row: row[1]
ordered = sorted(rows, key=f, reverse=True)
__lython_file = open("/out.txt", "w")
__lython_file.write(str(ordered) + "|" + str((lambda x: x + 1)(4)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("[('a', 3), ('b', 2), ('c', 1)]|5", host.ReadText("/out.txt"));
    }

    [Fact]
    public void VariadicFunctionsAndCallSplatting_WorkLikePythonSubset()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
def describe(prefix, *items, **meta):
    return prefix + "|" + str(items) + "|" + str(meta)

parts = ["a", "b"]
options = {"flag": True, "count": 2}
__lython_file = open("/out.txt", "w")
__lython_file.write(describe("p", *parts, **options) + "|" + str(sorted(*[[3, 1, 2]], reverse = True)))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("p|('a', 'b')|{'flag': True, 'count': 2}|[3, 2, 1]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void SetsBroaderComprehensionsAndStarredUnpacking_WorkLikePythonSubset()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
flat = [value for row in [[1, 2], [3]] for value in row]
mapping = {str(x) + str(y): x + y for x in [1, 2] for y in [3]}
squares = {value * value for value in [1, 2, 2, 3] if value > 1}
labels = {name for name, value in [("a", 1), ("b", 2)] if value > 1}
nested = {left + right for left in [1, 2] for right in [10, 20] if right == 20}
head, *middle, tail = [10, 20, 30, 40]
items = {3, 1, 2, 1}
items.add(4)
other = set([2, 4, 5])
__lython_file = open("/out.txt", "w")
__lython_file.write(str(flat) + "|" + str(mapping) + "|" + str(sorted(squares)) + "|" + str(sorted(labels)) + "|" + str(sorted(nested)) + "|" + str(head) + "|" + str(middle) + "|" + str(tail) + "|" + str(items | other) + "|" + str(items & other) + "|" + str(4 in items))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("[1, 2, 3]|{'13': 4, '23': 5}|[4, 9]|['b']|[21, 22]|10|[20, 30]|40|{1, 2, 3, 4, 5}|{2, 4}|True", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("1 << -1\n", "ValueError", "negative shift count")]
    [InlineData("1.5 | 2\n", "TypeError", "Operands are not compatible with '|'")]
    [InlineData("~1.5\n", "TypeError", "Operand is not an integer")]
    [InlineData("items = (1, 2)\nitems[0] = 3\n", "TypeError", "Tuple does not support item assignment")]
    [InlineData("items = (1, 2)\ndel items[0]\n", "TypeError", "Tuple does not support item deletion")]
    public void BitwiseFailures_ReportExpectedErrors(string source, string exceptionType, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
            return;
        }

        Assert.Equal(exceptionType, result.Failure.ExceptionType);
        Assert.Contains(message, result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("set([[1]])\n", "TypeError", "hashable")]
    [InlineData("value = {[]}\n", "TypeError", "hashable")]
    [InlineData("value = {[item] for item in [1]}\n", "TypeError", "hashable")]
    [InlineData("f = lambda x: x\nf(**1)\n", "TypeError", "expects a dictionary")]
    [InlineData("a, *rest = [1]\n__lython_file = open(\"/out.txt\", \"w\")\n__lython_file.write(str(rest))\n__lython_file.close()\n", null, "[]")]
    public void SetAndSplattingEdgeCases_ArePinned(string source, string? exceptionType, string expectedFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        if (exceptionType is null)
        {
            Assert.True(result.Success, result.Failure?.Message);
            return;
        }

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(expectedFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sorted([1, \"a\"])\n", "TypeError", "Values are not comparable")]
    [InlineData("min([1, \"a\"])\n", "TypeError", "Values are not comparable")]
    [InlineData("max([1, \"a\"])\n", "TypeError", "Values are not comparable")]
    [InlineData("1 in 2\n", "TypeError", "membership testing")]
    [InlineData("1 < \"a\"\n", "TypeError", "Values are not comparable")]
    public void UnsupportedComparisonShapes_ReportExpectedFailure(string source, string exceptionType, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
            return;
        }

        Assert.Equal(exceptionType, result.Failure.ExceptionType);
        Assert.Contains(message, result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ListPopOnEmptyList_FailsWithIndexError()
    {
        var result = new LythonEngine().Run(
            """
items = []
items.pop()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("IndexError", result.Failure?.ExceptionType);
        Assert.Contains("pop from empty list", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DictPopOnMissingKey_FailsWithKeyError()
    {
        var result = new LythonEngine().Run(
            """
d = {}
d.pop("missing")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("KeyError", result.Failure?.ExceptionType);
        Assert.Contains("missing", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinWithNonStringElement_FailsWithTypeError()
    {
        var result = new LythonEngine().Run(
            """
"-".join(["a", 1])
""",
            new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("iterable of strings", StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal("TypeError", result.Failure.ExceptionType);
            Assert.Contains("iterable of strings", result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Format_SupportsEscapedBraces()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
__lython_file = open("/out.txt", "w")
__lython_file.write("a{{b}}:{0}".format("x"))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("a{b}:x", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("\"{1}\".format(\"x\")\n", "Invalid positional format field.")]
    [InlineData("\"{\".format()\n", "Expected '}' in format string.")]
    [InlineData("\"}\".format()\n", "Unexpected '}' in format string.")]
    [InlineData("\"{0:>3}\".format(\"x\")\n", "Format specifiers are not supported.")]
    public void Format_InvalidPattern_FailsWithValueError(string source, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Contains(message, result.Failure?.Message, StringComparison.Ordinal);
    }
}
