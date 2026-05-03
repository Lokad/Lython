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
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|[name, count, items]|pcs|{'name': demo, 'count': 2, 'items': [4, 9]}|(demo, 2, [4, 9])|Box(name=next, count=2, items=[4, 9])|[4, 9]", host.ReadText("/out.txt"));
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
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[x, y]|id|True|True|True", host.ReadText("/out.txt"));
    }
}
