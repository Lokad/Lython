using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CopyModuleFunctionTests
{
    [Fact]
    public void CopyModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import copy

shared = [1]
source = [shared]
shallow = copy.copy(source)
deep = copy.deepcopy(source)
shared.append(2)

cyclic = []
cyclic.append(cyclic)
cycle_copy = copy.deepcopy(cyclic)

class Box:
    def __init__(self, value):
        self.value = value
    def __copy__(self):
        return Box(self.value + 1)
    def __deepcopy__(self, memo):
        return Box(self.value + 10)

box = Box(5)
box_copy = copy.copy(box)
box_deep = copy.deepcopy(box)

vals = []
vals.append(str(shallow[0]))
vals.append(str(deep[0]))
vals.append(str(cycle_copy[0] is cycle_copy))
vals.append(str(box_copy.value))
vals.append(str(box_deep.value))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[1, 2]|[1]|True|6|15", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CopyModule_ExpandedSurface_HasDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import copy
from collections import namedtuple
from dataclasses import dataclass
from decimal import Decimal
from pathlib import Path
from typing import NamedTuple

@dataclass
class Row:
    name: str
    qty: int

Point = namedtuple("Point", "x y")
TypedPoint = NamedTuple("TypedPoint", [("x", int), ("y", int)])

source = []
source.append(source)
memo = {}
first = copy.deepcopy(source, memo)
second = copy.deepcopy(source, memo)

mapping = {}
mapping["self"] = mapping
mapping_copy = copy.deepcopy(mapping)
seen = {1, 2}
seen_copy = copy.deepcopy(seen)

class Holder:
    def __init__(self, value):
        self.value = value
    def __deepcopy__(self, memo):
        return Holder(copy.deepcopy(self.value, memo))

shared = [1]
holder = Holder(shared)
holder_copy = copy.deepcopy(holder, memo)
shared_copy = copy.deepcopy(shared, memo)

row = Row("sku", 1)
row2 = copy.replace(row, qty=3)
point = Point(1, 2)
point2 = copy.replace(point, x=5)
typed = TypedPoint(7, y=8)
typed2 = copy.replace(typed, y=9)

class Custom:
    def __init__(self, value):
        self.value = value
    def __replace__(self, value):
        return Custom(value)

custom2 = copy.replace(Custom(4), value=6)

def func():
    return 1

path = Path("/repo/a.txt")
decimal = Decimal("1.25")
pair = (1, 2)

vals = []
vals.append(str(first[0] is first))
vals.append(str(first is second))
vals.append(str(len(memo) > 0))
vals.append(str(mapping_copy["self"] is mapping_copy))
vals.append(str(seen_copy == seen))
vals.append(str(seen_copy is seen))
vals.append(str(holder_copy.value is shared_copy))
vals.append(str(row2.name) + ":" + str(row2.qty))
vals.append(str(point2.x) + ":" + str(point2.y))
vals.append(str(typed2.x) + ":" + str(typed2.y))
vals.append(str(custom2.value))
vals.append(str(copy.Error is copy.error))
vals.append(str(copy.dispatch_table))
vals.append(str(copy.copy(func) is func))
vals.append(str(copy.deepcopy(Row) is Row))
vals.append(str(copy.copy(pair) is pair))
vals.append(str(copy.deepcopy(path) is path))
vals.append(str(copy.deepcopy(decimal) is decimal))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "True|True|True|True|True|False|True|sku:3|5:2|7:9|6|True|{}|True|True|True|True|True",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void CopyModule_StaticContractsCoverExpandedSurface()
    {
        var valid = new LythonEngine().Run(
            """
import copy

items = [1]
memo = {}
copy.copy(items)
copy.deepcopy(items, memo=memo)
copy.replace(items, obj=1)
""",
            new MockLythonHost());

        Assert.False(valid.Success);
        Assert.NotNull(valid.Failure);
        Assert.Equal("TypeError", valid.Failure!.ExceptionType);
        Assert.Contains("copy.replace", valid.Failure.Message, StringComparison.Ordinal);

        var invalid = new LythonEngine().Run(
            """
import copy
from dataclasses import dataclass, field

@dataclass
class StaticRow:
    x: int
    y: int = field(init=False, default=0)

class StaticBad:
    __reduce__ = 1

copy.copy()
copy.deepcopy([], memo=1)
copy.copy(StaticBad())
copy.replace(StaticRow(1), z=2)
copy.replace(StaticRow(1), y=2)
copy.replace(obj=1)
copy.replace(1, 2)
""",
            new MockLythonHost());

        Assert.False(invalid.Success);
        Assert.Null(invalid.Failure);
        Assert.True(
            invalid.Diagnostics.Count(d => d.Code is "LA3151" or "LA3158") >= 4,
            string.Join(" | ", invalid.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Theory]
    [InlineData(
        """
import copy
class Bad:
    __copy__ = 1
copy.copy(Bad())
""",
        "TypeError",
        "__copy__")]
    [InlineData(
        """
import copy
class Bad:
    pass
bad = Bad()
bad.__reduce__ = 1
copy.copy(bad)
""",
        "NotImplementedError",
        "__reduce__")]
    [InlineData(
        """
import copy
from collections import namedtuple
Point = namedtuple("Point", "x y")
copy.replace(Point(1, 2), z=3)
""",
        "ValueError",
        "unexpected field")]
    [InlineData(
        """
import copy
copy.replace(1, x=2)
""",
        "TypeError",
        "dataclass")]
    public void CopyModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
