using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

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
    [InlineData("items = (1, 2)\nitems[0] += 1", "TypeError", "Tuple does not support item assignment")]
    [InlineData("class Box:\n    pass\nbox = Box()\nbox.missing += 1", "AttributeError", "Object has no attribute 'missing'")]
    public void AugmentedAssignment_InvalidRuntimeTargets_FailClearly(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("items = [0]\nif (items[0] := 1):\n    pass", "assignment expression target")]
    [InlineData("class Box:\n    pass\nbox = Box()\nif (box.value := 1):\n    pass", "assignment expression target")]
    [InlineData("row = [1, 2]\nif ((a, b) := row):\n    pass", "assignment expression target")]
    [InlineData("row = [1]\nif ([a] := row):\n    pass", "assignment expression target")]
    [InlineData("if ((a := 1) := 2):\n    pass", "assignment expression target")]
    [InlineData("class Box:\n    pass\nbox = Box()\nbox.value: int = 1", "Unsupported assignment target")]
    [InlineData("items = [0]\nitems[0]: int = 1", "Unsupported assignment target")]
    [InlineData("(target) = 1", "Unsupported assignment target")]
    [InlineData("(a, b) = [1, 2]", "Unsupported assignment target")]
    [InlineData("[a, b] = [1, 2]", "Unsupported assignment target")]
    [InlineData("(a, (b, c)) = [1, [2, 3]]", "Unsupported assignment target")]
    public void UnsupportedAssignmentTargetForms_ReportCompileDiagnostics(string source, string messageFragment)
    {
        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Message.Contains(messageFragment, StringComparison.Ordinal));
    }

    private static string DescribeFailure(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message));
}
