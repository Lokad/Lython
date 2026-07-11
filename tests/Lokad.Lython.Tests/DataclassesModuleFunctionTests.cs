using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DataclassesModuleFunctionTests
{
    [Fact]
    public void DataclassesModule_HelperFunctions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import KW_ONLY, InitVar, asdict, astuple, dataclass, field, fields, is_dataclass, replace
from typing import ClassVar

@dataclass(order=True, kw_only=True)
class Box:
    name: str
    marker: ClassVar[str] = "BOX"
    setup: InitVar[int] = 0
    _: KW_ONLY
    count: int = field(default=1, metadata={"unit": "pcs"})
    items: list = field(default_factory=list)

    def __post_init__(self, setup):
        self.items.append(setup)

box = Box(name="demo", setup=4, count=2)
replacement = replace(box, name="next", setup=9)
field_names = [f.name for f in fields(box)]
field_meta = fields(box)[1].metadata["unit"]
vals = []
vals.append(str(is_dataclass(Box)))
vals.append(str(is_dataclass(box)))
vals.append(str(field_names))
vals.append(str(field_meta))
vals.append(str(asdict(box)))
vals.append(str(astuple(box)))
vals.append(str(replacement))
vals.append(str(replacement.items))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|['name', 'count', 'items']|pcs|{'name': 'demo', 'count': 2, 'items': [4, 9]}|('demo', 2, [4, 9])|Box(name='next', count=2, items=[4, 9])|[4, 9]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DataclassesModule_GeneratedRepresentationUsesFieldRepr()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass

class Label:
    def __repr__(self):
        return "<label>"

@dataclass
class Box:
    name: str
    label: Label

__lython_file = open("/out.txt", "w")
__lython_file.write(str(Box("x", Label())))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Box(name='x', label=<label>)", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DataclassesModule_AsDictPreservesAndRecursivelyCopiesMappingKeys()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from dataclasses import asdict, dataclass

@dataclass
class Box:
    data: dict

value = asdict(Box({1: "x", ("a", 2): ["y"]}))
__lython_file = open("/out.txt", "w")
__lython_file.write(str(value))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{'data': {1: 'x', ('a', 2): ['y']}}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DataclassesModule_FieldAndParamsMetadata_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass, field

@dataclass(frozen=True, unsafe_hash=True, match_args=True)
class Item:
    x: int = field(compare=False, hash=False, repr=False, metadata={"kind": "id"})
    y: int = 2

field_map = Item.__dataclass_fields__
params = Item.__dataclass_params__
vals = []
vals.append(str(sorted(field_map.keys())))
vals.append(str(field_map["x"].metadata["kind"]))
vals.append(str(params.frozen))
vals.append(str(params.unsafe_hash))
vals.append(str(params.match_args))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['x', 'y']|id|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DataclassesModule_RuntimeDataclassCallable_WrapsClasses()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import dataclasses
from dataclasses import Field, fields, is_dataclass

class Box:
    x: int
    y: int = 2

Box = dataclasses.dataclass(Box)
ordered = dataclasses.dataclass(order=True)

@ordered
class Ordered:
    x: int

box = Box(1)
first = fields(Box)[0]
parts = []
parts.append(str(is_dataclass(Box)))
parts.append(str(box))
parts.append(first.name)
parts.append(str(first.type))
parts.append(str(Field))
parts.append(str(Ordered(1) < Ordered(2)))
parts.append(str(Box.__annotations__["x"]))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|Box(x=1, y=2)|x|int|<class 'dataclasses.Field'>|True|int", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DataclassesModule_MakeDataclassAndInheritedFields_WorkTogether()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass, field, fields, make_dataclass

@dataclass
class Base:
    a: int
    b: int = 2

Child = make_dataclass("Child", [("c", int, field(default=3)), ("d", list, field(default_factory=list))], bases=(Base,))

child = Child(1)
names = [f.name for f in fields(Child)]
parts = []
parts.append(str(child))
parts.append(str(names))
parts.append(str(child.d))
parts.append(str(Child.__dataclass_fields__["a"].default))
parts.append(str(Child.__dataclass_fields__["c"].type is int))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(parts))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Child(a=1, b=2, c=3, d=[])|['a', 'b', 'c', 'd']|[]|MISSING|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DataclassesModule_InheritedDefaultOrdering_IsRejected()
    {
        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass

@dataclass
class Base:
    x: int = 1

@dataclass
class Bad(Base):
    y: int
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("without a default cannot follow", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DataclassesModule_SlotsOptions_AreExplicitlyUnsupported()
    {
        var result = new LythonEngine().Run(
            """
import dataclasses

class Box:
    x: int

Box = dataclasses.dataclass(Box, slots=True)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure!.ExceptionType);
        Assert.Contains("slots=True", result.Failure.Message, StringComparison.Ordinal);
    }
}
