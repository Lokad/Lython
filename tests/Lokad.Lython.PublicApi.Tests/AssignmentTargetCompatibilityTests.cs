using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AssignmentTargetCompatibilityTests
{
    [Fact]
    public void AugmentedAssignment_TargetsAndOperators_RunLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
items = [1, 2, 3, 4]
alias = items
items[0] += 4

row = {"qty": 1}
row["qty"] += 2

i = 0
matrix = [[1]]
matrix[i][0] += 4

class Box:
    pass

box = Box()
box.value = 1
box.value += 2

cell = Box()
cell.value = 1
cell.value += 6

items[1:3] += [9]

power = 2
power **= 5

mask = 3
mask |= 4
mask &= 6
mask ^= 5

shift = 1
shift <<= 4
shift >>= 2

left = {1, 2}
same_left = left
left |= {2, 3}

middle = {1, 2, 3}
same_middle = middle
middle &= {2, 3, 4}

toggle = {1, 2}
same_toggle = toggle
toggle ^= {2, 3}

values = [
    str(alias),
    str(row["qty"]),
    str(matrix[0][0]),
    str(box.value),
    str(cell.value),
    str(power),
    str(mask),
    str(shift),
    str(same_left is left) + ":" + str(len(left)) + ":" + str(3 in same_left),
    str(same_middle is middle) + ":" + str(len(middle)) + ":" + str(1 in same_middle),
    str(same_toggle is toggle) + ":" + str(len(toggle)) + ":" + str(3 in same_toggle) + ":" + str(2 in same_toggle),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal(
            "[5, 2, 3, 9, 4]|3|5|3|7|32|3|4|True:3:True|True:2:False|True:2:True:False",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void AugmentedAssignment_OpenPyxlCellValues_RunLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from openpyxl import Workbook

wb = Workbook()
ws = wb.active
ws["A1"].value = 1
ws["A1"].value += 2
ws.cell(row=1, column=2).value = 3
ws.cell(row=1, column=2).value += 4

__lython_file = open("/out.txt", "w")
__lython_file.write(str(ws["A1"].value) + "|" + str(ws.cell(row=1, column=2).value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("3|7", host.ReadText("/out.txt"));
    }

    [Fact]
    public void AugmentedAssignment_EvaluatesTargetPartsOnce()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
calls = []
items = [10]

def target():
    calls.append("target")
    return items

def index():
    calls.append("index")
    return 0

target()[index()] += 5

class Box:
    pass

box = Box()
box.value = 1

def owner():
    calls.append("owner")
    return box

owner().value += 4

__lython_file = open("/out.txt", "w")
__lython_file.write(str(items[0]) + "|" + str(box.value) + "|" + str(calls))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("15|5|['target', 'index', 'owner']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Walrus_AssignsAndReturnsValueInOrdinaryConditions()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
value = 3
parts = []

if (n := value):
    parts.append(str(n))

lines = ["alpha", "beta", ""]
while (line := lines.pop(0)):
    parts.append(line)

parts.append(str((m := len(parts))))
ok = (m == 3) and (n == 3)
if (flag := ok):
    parts.append(str(flag))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("3|alpha|beta|3|True", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("items = (1, 2)\nitems[0] += 1", "TypeError", "'tuple' object does not support item assignment")]
    [InlineData("class Box:\n    pass\nbox = Box()\nbox.missing += 1", "AttributeError", "'Box' object has no attribute 'missing'")]
    public void AugmentedAssignment_InvalidRuntimeTargets_FailClearly(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("text = \"ab\"\ntext[0] = \"x\"", "TypeError", "'str' object does not support item assignment")]
    [InlineData("text = \"ab\"\ndel text[0]", "TypeError", "'str' object doesn't support item deletion")]
    [InlineData("items = (1, 2)\ntry:\n    items[0:1] = [9]\nexcept TypeError as err:\n    raise AssertionError(err.message)", "AssertionError", "'tuple' object does not support item assignment")]
    [InlineData("items = (1, 2)\ndel items[0:1]", "TypeError", "'tuple' object does not support item deletion")]
    [InlineData("text = \"ab\"\ntry:\n    text[0:1] = \"x\"\nexcept TypeError as err:\n    raise AssertionError(err.message)", "AssertionError", "'str' object does not support item assignment")]
    [InlineData("text = \"ab\"\ndel text[0:1]", "TypeError", "'str' object does not support item deletion")]
    public void ImmutableSequenceMutation_FailsClearly(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("r = range(3)\ndel r[0]", "TypeError", "'range' object doesn't support item deletion")]
    [InlineData("r = range(3)\nr[0] = 9", "TypeError", "'range' object does not support item assignment")]
    [InlineData("x = 1\ntry:\n    del x[0]\nexcept TypeError as err:\n    raise AssertionError(err.message)", "AssertionError", "'int' object does not support item deletion")]
    [InlineData("x = 1\nx[0] = 9", "TypeError", "'int' object does not support item assignment")]
    [InlineData("x = {1}\ntry:\n    del x[0]\nexcept TypeError as err:\n    raise AssertionError(err.message)", "AssertionError", "'set' object doesn't support item deletion")]
    [InlineData("x = {1}\nx[0] = 9", "TypeError", "'set' object does not support item assignment")]
    [InlineData("try:\n    del None[0]\nexcept TypeError as err:\n    raise AssertionError(err.message)", "AssertionError", "'NoneType' object does not support item deletion")]
    [InlineData("x = 1.5\nx[0] = 9", "TypeError", "'float' object does not support item assignment")]
    [InlineData("from collections import namedtuple\nP = namedtuple(\"P\", [\"x\", \"y\"])\np = P(1, 2)\ndel p[0]", "TypeError", "'P' object doesn't support item deletion")]
    [InlineData("from collections import namedtuple\nP = namedtuple(\"P\", [\"x\", \"y\"])\np = P(1, 2)\np[0] = 9", "TypeError", "'P' object does not support item assignment")]
    public void UnsupportedMutationTargets_NameReceivers(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("items = [0]\nif (items[0] := 1):\n    pass", "assignment expression target")]
    [InlineData("class Box:\n    pass\nbox = Box()\nif (box.value := 1):\n    pass", "assignment expression target")]
    [InlineData("row = [1, 2]\nif ((a, b) := row):\n    pass", "assignment expression target")]
    [InlineData("row = [1]\nif ([a] := row):\n    pass", "assignment expression target")]
    [InlineData("if ((a := 1) := 2):\n    pass", "assignment expression target")]
    [InlineData("class Box:\n    pass\nbox = Box()\nbox.value: int = 1", "Unsupported assignment target")]
    [InlineData("items = [0]\nitems[0]: int = 1", "Unsupported assignment target")]
    [InlineData("(a, (b, c)) = [1, [2, 3]]", "Unsupported assignment target")]
    public void UnsupportedAssignmentTargetForms_ReportCompileDiagnostics(string source, string messageFragment)
    {
        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Message.Contains(messageFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void ParenthesizedSingleTargets_RunLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
x = 0
(x) = 1
((x)) = 2

items = [1, 2]
(items[0]) = 9
(items)[0] += 10
del (items[1])

class Box:
    pass

box = Box()
box.value = 1
(box.value) += 2
del (box.value)

count = 0
(count) += 1

first = 0
second = 0
first = (second) = 3

def run():
    local = 0
    (local) = 4
    (local) += 1
    del (local)
    return "deleted"

values = [
    str(x),
    str(items),
    str(count),
    str(first) + ":" + str(second),
    run(),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("2|[19]|1|3:3|deleted", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ParenthesizedAnnotatedTargets_RunLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
x = 0
(x): int = 1
((x)): int = 2

bare = 0
(bare): int

def run():
    local = 0
    (local): int = 4
    return local

values = [
    str(x),
    str(bare),
    str(run()),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("2|0|4", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DisplayUnpackingTargets_RunLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
a = 0
b = 0
(a, b) = (1, 2)

c = 0
d = 0
[c, d] = [3, 4]

e = 0
r = []
(e, *r) = (5, 6, 7)

single = 0
(single,) = (8,)

listed = 0
[listed] = [9]

trailing = 0
trailing, = (10,)

first = 0
second = 0
third = 0
fourth = 0
first = (second,) = (11,)
third, fourth = (12, 13)

def run():
    p = 0
    q = 0
    (p, q) = (14, 15)
    return p + q

values = [
    str(a + b),
    str(c + d),
    str(e) + ":" + str(r),
    str(single),
    str(listed),
    str(trailing),
    str(first) + ":" + str(second),
    str(third + fourth),
    str(run()),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("3|7|5:[6, 7]|8|9|10|(11,):11|25|29", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DeleteDisplayTargets_RunLikePython()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
a = 1
b = 2
del (a, b)

c = 3
d = 4
del c, d

e = 5
f = 6
del [e, f]

items = [1, 2, 3]
del (items[0], items[1])

class Box:
    pass

box = Box()
box.v = 1
box.w = 2
other = 3
del (box.v, other)
del box.w

solo = 1
del (solo,)

t1 = 1
del t1,

del ()
del []

def run():
    p = 1
    q = 2
    del (p, q)
    return 7

g = 0
def set_global():
    global g
    tmp = 1
    g = 5
    del (tmp, g)
    return 6

gone = []
try:
    a
except NameError:
    gone.append("a")
try:
    b
except NameError:
    gone.append("b")
try:
    c
except NameError:
    gone.append("c")
try:
    d
except NameError:
    gone.append("d")
try:
    e
except NameError:
    gone.append("e")
try:
    f
except NameError:
    gone.append("f")
try:
    solo
except NameError:
    gone.append("solo")
try:
    t1
except NameError:
    gone.append("t1")
try:
    other
except NameError:
    gone.append("other")

marker = set_global()
try:
    g
except NameError:
    gone.append("g")

values = [
    "|".join(gone),
    str(items),
    str(hasattr(box, "v")) + ":" + str(hasattr(box, "w")),
    str(run()),
    str(marker),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, DescribeFailure(result));
        Assert.Null(result.Failure);
        Assert.Equal("a|b|c|d|e|f|solo|t1|other|g|[2]|False:False|7|6", host.ReadText("/out.txt"));
    }

    private static string DescribeFailure(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
}

