using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DataclassesModuleFunctionTests
{
    [Theory]
    [InlineData("field", false)]
    [InlineData("field", true)]
    [InlineData("asdict", false)]
    [InlineData("asdict", true)]
    [InlineData("astuple", false)]
    [InlineData("astuple", true)]
    public async Task OrdinaryFunctionsDoNotAcquireDataclassContracts(string name, bool shadowImport)
    {
        var source = (shadowImport ? "from dataclasses import " + name + "\n" : "") +
            "def " + name + "(k, v):\n    return k + v\n" +
            "print(" + name + "('a', 'b'))\n";
        await AssertBothModes(source, "ab\n");
    }

    [Theory]
    [InlineData("field")]
    [InlineData("asdict")]
    [InlineData("astuple")]
    public async Task ReassignedModuleDoesNotRetainDataclassContracts(string name)
    {
        var source = "import dataclasses\nclass Helpers:\n    def " + name +
            "(self, k, v):\n        return k + v\ndataclasses = Helpers()\n" +
            "print(dataclasses." + name + "('a', 'b'))\n";
        await AssertBothModes(source, "ab\n");
    }

    [Theory]
    [InlineData("field")]
    [InlineData("asdict")]
    [InlineData("astuple")]
    public async Task ReassignedHelperDoesNotRetainDataclassContracts(string name)
    {
        var source = "from dataclasses import " + name + "\n" +
            "def combine(k, v):\n    return k + v\n" + name + " = combine\n" +
            "print(" + name + "('a', 'b'))\n";
        await AssertBothModes(source, "ab\n");
    }

    [Fact]
    public async Task OrdinaryFieldCallProvidesAnOrdinaryDataclassDefault()
    {
        await AssertBothModes("""
            from dataclasses import dataclass
            def field(k, v):
                return k + v
            @dataclass
            class Box:
                value: str = field('a', 'b')
            print(Box(), Box('z'))
            """, "Box(value='ab') Box(value='z')\n");
    }

    [Theory]
    [InlineData("import dataclasses as dc", "dc.field", "dc.asdict", "dc.astuple")]
    [InlineData("from dataclasses import field as make_field, asdict as to_dict, astuple as to_tuple", "make_field", "to_dict", "to_tuple")]
    [InlineData("from dataclasses import field, asdict, astuple\nmake_field = field\nto_dict = asdict\nto_tuple = astuple", "make_field", "to_dict", "to_tuple")]
    public async Task AliasesPreserveFieldOptionsAndHelperContracts(string imports, string field, string asdict, string astuple)
    {
        var source = "from dataclasses import dataclass\n" + imports + "\n" +
            "@dataclass\nclass Box:\n    value: int = " + field + "()\n" +
            "    keyword: int = " + field + "(default=7, kw_only=True)\n" +
            "    cached: int = " + field + "(default=9, init=False)\n" +
            "    items: list = " + field + "(default_factory=list)\n" +
            "box = Box(1, keyword=8)\nprint(" + asdict + "(box, dict_factory=dict))\n" +
            "print(" + astuple + "(box, tuple_factory=tuple))\n";
        await AssertBothModes(source, "{'value': 1, 'keyword': 8, 'cached': 9, 'items': []}\n(1, 8, 9, [])\n");
    }

    [Theory]
    [InlineData("import dataclasses as dc", "dc.field(default=1, default_factory=list)", "LA3037")]
    [InlineData("from dataclasses import field as make_field", "make_field(default_factory=1)", "LA3038")]
    [InlineData("from dataclasses import asdict as to_dict", "to_dict(None, dict_factory=1)", "LA3039")]
    [InlineData("import dataclasses as dc", "dc.astuple(None, tuple_factory=1)", "LA3042")]
    public void InvalidAliasedDataclassCallsKeepTheirDiagnostics(string imports, string call, string code)
    {
        var script = new LythonEngine().Compile(imports + "\n" + call);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Theory]
    [InlineData("import dataclasses as dc", "dc.field")]
    [InlineData("from dataclasses import field as make_field", "make_field")]
    public void AliasedFieldsKeepStaticDefaultAndConstructorFacts(string imports, string field)
    {
        var source = "from dataclasses import dataclass\n" + imports + "\n" +
            "@dataclass\nclass Box:\n    text: str = " + field + "(default='alpha', kw_only=True)\n" +
            "    label: str = " + field + "(default='beta', init=False)\n" +
            "    items: list = " + field + "(default_factory=list)\n" +
            "box = Box()\nbox.text.find(1)\nbox.label.find(1)\nbox.items.extend(1)\n";
        var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.Equal(2, script.Diagnostics.Count(diagnostic => diagnostic.Code == "LA3075"));
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA3140");
    }

    [Theory]
    [InlineData("import dataclasses as dc", "dc.field", "Box()")]
    [InlineData("import dataclasses as dc", "dc.field", "Box(1, 2)")]
    [InlineData("import dataclasses as dc", "dc.field", "Box(1, cached=2)")]
    [InlineData("from dataclasses import field as make_field", "make_field", "Box()")]
    [InlineData("from dataclasses import field as make_field", "make_field", "Box(1, 2)")]
    [InlineData("from dataclasses import field as make_field", "make_field", "Box(1, cached=2)")]
    public void AliasedFieldsKeepInvalidConstructorDiagnostics(string imports, string field, string call)
    {
        var source = "from dataclasses import dataclass\n" + imports + "\n" +
            "@dataclass\nclass Box:\n    value: int = " + field + "()\n" +
            "    keyword: int = " + field + "(default=7, kw_only=True)\n" +
            "    cached: int = " + field + "(default=9, init=False)\n" + call;
        var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA3149");
    }

    private static async Task AssertBothModes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

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
    public void DataclassesModule_FieldsAreStableAndReplaceErrorsRemainCatchable()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from dataclasses import dataclass, field, fields, replace

@dataclass
class Box:
    value: int
    cached: int = field(init=False, default=0)

box = Box(1)
stable = fields(Box)[0] is fields(box)[0]
public = fields(Box)[0] is Box.__dataclass_fields__["value"]
try:
    replace(box, cached=2)
except ValueError:
    caught = True
else:
    caught = False

__lython_file = open("/out.txt", "w")
__lython_file.write(str(stable) + "|" + str(public) + "|" + str(caught))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True", host.ReadText("/out.txt"));
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
        var failure = result.Failure;
        Assert.Equal("TypeError", failure?.ExceptionType);
        Assert.Contains("without a default cannot follow", failure?.Message, StringComparison.Ordinal);
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
        var failure = result.Failure;
        Assert.Equal("NotImplementedError", failure?.ExceptionType);
        Assert.Contains("slots=True", failure?.Message, StringComparison.Ordinal);
    }
}
