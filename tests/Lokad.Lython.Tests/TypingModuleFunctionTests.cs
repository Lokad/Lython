using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TypingModuleFunctionTests
{
    [Fact]
    public void TypingModule_InertAliasesHelpersAndStarImports_WorkInGeneratedScripts()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from typing import *

T = TypeVar("T")
UserId = NewType("UserId", int)
Point = NamedTuple("Point", [("x", int), ("y", int)])
Payload = TypedDict("Payload", {"name": str, "count": int})

class Box(Generic[T]):
    pass

class Shape(Protocol):
    pass

class Row(TypedDict):
    name: str

point = Point(1, y=2)
payload = Payload(name="abc", count=3)
parts = []
parts.append(str(List[int]))
parts.append(str(Dict[str, int]))
parts.append(str(get_origin(List[int])))
parts.append(str(get_args(Dict[str, int])))
parts.append(str(cast(int, "7")))
parts.append(str(UserId(3)))
parts.append(str(TYPE_CHECKING))
parts.append(str(T))
parts.append(str(point))
parts.append(str(point.x))
parts.append(str(point[1]))
parts.append(str(payload["name"]))
parts.append(str(Box))
parts.append(str(Shape))
parts.append(str(Row))
write_text("/out.txt", "|".join(parts))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("typing.List[int]|typing.Dict[str, int]|typing.List|(str, int)|7|3|False|T|Point(x=1, y=2)|1|2|abc|<class 'Box'>|<class 'Shape'>|<class 'Row'>", host.ReadText("/out.txt"));
    }

    [Fact]
    public void TypingModule_DataclassClassVarAndParameterizedAliases_RemainCompatible()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass, fields
from typing import ClassVar, List

@dataclass
class Box:
    marker: ClassVar[str] = "BOX"
    values: List[int]

box = Box([1, 2])
parts = []
parts.append(str([field.name for field in fields(Box)]))
parts.append(str(Box.__annotations__["values"]))
parts.append(str(box))
write_text("/out.txt", "|".join(parts))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[values]|List[int]|Box(values=[1, 2])", host.ReadText("/out.txt"));
    }

    [Fact]
    public void TypingModule_StaticContractsAcceptCommonAnnotationHeavySnippets()
    {
        var compiled = new LythonEngine().Compile(
            """
from dataclasses import dataclass
from typing import *

T = TypeVar("T")

@dataclass
class Box(Generic[T]):
    marker: ClassVar[str] = "BOX"
    values: List[int]

value = cast(str, "demo")
origin = get_origin(Optional[int])
args = get_args(Dict[str, int])
""");

        Assert.Empty(compiled.Diagnostics);
    }
}
