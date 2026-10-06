using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed partial class PostponedAnnotationTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "function_kinds", "from __future__ import annotations\ndef f(a: Missing, /, b: list[int]=[], *args: tuple[X,...], c: \"Forward\"=9, **kw: dict[str,Y]) -> Result:pass\nprint(f.__annotations__)\n", "{'b': 'list[int]', 'a': 'Missing', 'args': 'tuple[X, ...]', 'c': \"'Forward'\", 'kw': 'dict[str, Y]', 'return': 'Result'}\n" };
        yield return new object[] { "decorator_metadata", "from __future__ import annotations\ndef deco(f):print(f.__annotations__);return f\n@deco\ndef f(x: Missing)->Other:pass\n", "{'x': 'Missing', 'return': 'Other'}\n" };
        yield return new object[] { "module_and_class", "from __future__ import annotations\nprint(__annotations__)\nx: Missing=7\nclass C:\n print(__annotations__)\n y: Other=8\nprint(__annotations__,C.__annotations__,x,C.y)\n", "{}\n{}\n{'x': 'Missing'} {'y': 'Other'} 7 8\n" };
        yield return new object[] { "nonname_targets", "from __future__ import annotations\nclass C:pass\ndef obj():print(\"target\");return C()\ndef key():print(\"key\");return 0\nobj().x:Bad()\nobj()[key()]:Bad()\n(x):Bad()=7\nprint(__annotations__,x)\n", "target\ntarget\nkey\n{} 7\n" };
        yield return new object[] { "local_annotations", "from __future__ import annotations\ndef f():\n x: Missing\n (y): Bad()=7\n return y\nprint(f(),f.__annotations__)\n", "7 {}\n" };
        yield return new object[] { "conditional_setup", "from __future__ import annotations\nprint(__annotations__)\nif False:x:Missing\nclass C:\n print(__annotations__)\n if False:y:Missing\nprint(C.__annotations__)\n", "{}\n{}\n{}\n" };
        yield return new object[] { "private_names", "from __future__ import annotations\nclass C:\n __x: __Missing=7\n def f(self, __p: __Missing.x)->\"__Missing\":pass\nprint(C.__annotations__,C.f.__annotations__)\n", "{'_C__x': '__Missing'} {'_C__p': '__Missing.x', 'return': \"'__Missing'\"}\n" };
        yield return new object[] { "private_expression_names", "from __future__ import annotations\nclass C:\n __x: __Missing.__attr(__key=__value)\n __y: (lambda __p=__default: __p.__attr)\nprint(C.__annotations__)\n", "{'_C__x': '__Missing.__attr(__key=__value)', '_C__y': 'lambda __p=__default: __p.__attr'}\n" };
        yield return new object[] { "private_comprehension", "from __future__ import annotations\nclass C:\n __x: [__item for (__item), *__rest in __data if __item]\nprint(C.__annotations__)\n", "{'_C__x': '[__item for __item, *__rest in __data if __item]'}\n" };
        yield return new object[] { "normalized_names", "from __future__ import annotations\nclass C:\n __x: K.__K\nprint(C.__annotations__)\n", "{'_C__x': 'K.__K'}\n" };
        yield return new object[] { "generic_variadics", "from __future__ import annotations\ndef f[*Ts, **P](x:tuple[*Ts], y:P.args)->P.kwargs:pass\nprint(f.__annotations__)\n", "{'x': 'tuple[*Ts,]', 'y': 'P.args', 'return': 'P.kwargs'}\n" };
        yield return new object[] { "dataclass_strings", "from __future__ import annotations\nfrom dataclasses import dataclass\nfrom typing import ClassVar\n@dataclass\nclass C:\n x:int\n label:ClassVar[str]=\"value\"\nprint(C(7).x,C.__annotations__)\n", "7 {'x': 'int', 'label': 'ClassVar[str]'}\n" };
        yield return new object[] { "early_class_decorator", "from __future__ import annotations\ndef deco(c):print(c.__annotations__);return c\n@deco\nclass C:\n x:Missing\n def f(self)->__class__:return __class__\nprint(C().f() is C)\n", "{'x': 'Missing'}\nTrue\n" };
        yield return new object[] { "alias_and_docstring", "\"doc\"\nfrom __future__ import annotations as future\nx:Missing\nprint(__annotations__)\n", "{'x': 'Missing'}\n" };
        yield return new object[] { "parenthesized_docstring", "(\"doc\")\nfrom __future__ import annotations\nx:Missing\nprint(__annotations__)\n", "{'x': 'Missing'}\n" };
        yield return new object[] { "nested_docstring", "((\"doc\"))\nfrom __future__ import annotations\nx:Missing\nprint(__annotations__)\n", "{'x': 'Missing'}\n" };
        yield return new object[] { "empty_function_identity", "from __future__ import annotations\ndef f():pass\ng=lambda:7\nf.__annotations__[\"x\"]=\"custom\"\nprint(f.__annotations__,g.__annotations__,f.__annotations__ is g.__annotations__)\n", "{'x': 'custom'} {} False\n" };
        yield return new object[] { "suspended_default", "from __future__ import annotations\ndef outer():\n def f(x:Missing=(yield \"default\"))->Other:return x\n yield f.__annotations__,f()\ng=outer()\nprint(next(g),g.send(7))\n", "default ({'x': 'Missing', 'return': 'Other'}, 7)\n" };
        yield return new object[] { "class_global_annotations", "from __future__ import annotations\nx:Module\nclass C:\n global __annotations__\n y:Class\nprint(__annotations__,C.__annotations__)\n", "{'x': 'Module'} {'y': 'Class'}\n" };
        yield return new object[] { "generic_function", "from __future__ import annotations\ndef f[T](x: T, *args: tuple[T,...])->list[T]:return x\nprint(f.__annotations__,f.__type_params__[0].__name__)\n", "{'x': 'T', 'args': 'tuple[T, ...]', 'return': 'list[T]'} T\n" };
        yield return new object[] { "generic_class", "from __future__ import annotations\nclass C[T]:\n x:T\n def f(self,x:T)->T:pass\nprint(C.__annotations__,C.f.__annotations__)\n", "{'x': 'T'} {'x': 'T', 'return': 'T'}\n" };
        yield return new object[] { "generic_nested", "from __future__ import annotations\ndef outer[T]():\n def inner(x:T)->T:pass\n return inner\nprint(outer().__annotations__)\n", "{'x': 'T', 'return': 'T'}\n" };
        yield return new object[] { "class_mapping_deleted", "from __future__ import annotations\nx: Missing\nclass C:\n y:Other\n del __annotations__\n z:Last\nprint(__annotations__)\n", "{'x': 'Missing', 'z': 'Last'}\n" };
        yield return new object[] { "mapping_replaced", "from __future__ import annotations\n__annotations__={}\nx:Missing\nclass C:\n __annotations__={}\n y:Other\nprint(__annotations__,C.__annotations__)\n", "{'x': 'Missing'} {'y': 'Other'}\n" };
        yield return new object[] { "mapping_custom", "from __future__ import annotations\nclass M:\n def __setitem__(self,k,v):print(k,v)\n__annotations__=M()\nx:Missing\nclass C:\n __annotations__=M()\n y:Other\n", "x Missing\ny Other\n" };
        yield return new object[] { "empty_functions", "from __future__ import annotations\ndef f():pass\ng=lambda:7\nprint(f.__annotations__,g.__annotations__,f.__annotations__ is f.__annotations__)\n", "{} {} True\n" };
        yield return new object[] { "literal_values", "from __future__ import annotations\na:0xff\nb:1_000\nc:1e3\nd:1e309\ne:2.0j\nf:1e309j\ng:True\nh:None\ni:...\nprint(__annotations__)\n", "{'a': '255', 'b': '1000', 'c': '1000.0', 'd': '1e309', 'e': '2j', 'f': '1e309j', 'g': 'True', 'h': 'None', 'i': '...'}\n" };
        yield return new object[] { "string_values", "from __future__ import annotations\na: \"a'b\"\nb: 'a\"b'\nc: \"a'b\\\"c\"\nd: \"line\\nnext\"\ne: \"é☃\"\nf: \"\\x00\\x7f\\u200b\\ue000\"\ng: b\"a'b\\x80\"\nprint(__annotations__)\n", "{'a': '\"a\\'b\"', 'b': '\\'a\"b\\'', 'c': '\\'a\\\\\\'b\"c\\'', 'd': \"'line\\\\nnext'\", 'e': \"'é☃'\", 'f': \"'\\\\x00\\\\x7f\\\\u200b\\\\ue000'\", 'g': 'b\"a\\'b\\\\x80\"'}\n" };
        yield return new object[] { "binary_precedence", "from __future__ import annotations\nx: a + b * c - (d - e) / f // g % h\nprint(__annotations__[\"x\"])\n", "a + b * c - (d - e) / f // g % h\n" };
        yield return new object[] { "power_unary", "from __future__ import annotations\nx: (-a) ** b ** -c + ~(d + e)\nprint(__annotations__[\"x\"])\n", "(-a) ** b ** (-c) + ~(d + e)\n" };
        yield return new object[] { "boolean_precedence", "from __future__ import annotations\nx: a or b and not c or (d or e) and (f and g)\nprint(__annotations__[\"x\"])\n", "a or b and not c or (d or e) and (f and g)\n" };
        yield return new object[] { "comparison", "from __future__ import annotations\nx: a < b <= c is not d not in e\nprint(__annotations__[\"x\"])\n", "a < b <= c is not d not in e\n" };
        yield return new object[] { "comparison_nested", "from __future__ import annotations\nx: (a < b) < (c < d)\nprint(__annotations__[\"x\"])\n", "(a < b) < (c < d)\n" };
        yield return new object[] { "bitwise", "from __future__ import annotations\nx: a | b ^ c & d << e + f\nprint(__annotations__[\"x\"])\n", "a | b ^ c & d << e + f\n" };
        yield return new object[] { "conditional", "from __future__ import annotations\nx: (a if b else c) if d else e if f else g\nprint(__annotations__[\"x\"])\n", "(a if b else c) if d else e if f else g\n" };
        yield return new object[] { "call_order", "from __future__ import annotations\nx: obj.__call__(a, *b, key=c, *d, **e)\nprint(__annotations__[\"x\"])\n", "obj.__call__(a, *b, *d, key=c, **e)\n" };
        yield return new object[] { "containers", "from __future__ import annotations\nx: [a, *b, (c,d), {e:f, **g}, {h,*i}, ()]\nprint(__annotations__[\"x\"])\n", "[a, *b, (c, d), {e: f, **g}, {h, *i}, ()]\n" };
        yield return new object[] { "tuple", "from __future__ import annotations\nx: (a,)\nprint(__annotations__[\"x\"])\n", "(a,)\n" };
        yield return new object[] { "mixed_subscript", "from __future__ import annotations\nx: obj[1:3, ::-1, ..., *items]\nprint(__annotations__[\"x\"])\n", "obj[1:3, ::-1, ..., *items]\n" };
        yield return new object[] { "slice_grouping", "from __future__ import annotations\nx: obj[(a if b else c):(d if e else f):(g if h else i)]\nprint(__annotations__[\"x\"])\n", "obj[a if b else c:d if e else f:g if h else i]\n" };
        yield return new object[] { "slice_empty", "from __future__ import annotations\nx: obj[::]\nprint(__annotations__[\"x\"])\n", "obj[:]\n" };
        yield return new object[] { "integer_member", "from __future__ import annotations\nx: (1).real\nprint(__annotations__[\"x\"])\n", "1 .real\n" };
        yield return new object[] { "lambda_kinds", "from __future__ import annotations\nx: lambda a, /, b=3, *args, c=4, **kw: a + b\nprint(__annotations__[\"x\"])\n", "lambda a, /, b=3, *args, c=4, **kw: a + b\n" };
        yield return new object[] { "lambda_varargs", "from __future__ import annotations\nx: lambda *args, **kw: args\nprint(__annotations__[\"x\"])\n", "lambda*args, **kw: args\n" };
        yield return new object[] { "lambda_empty", "from __future__ import annotations\nx: lambda: a\nprint(__annotations__[\"x\"])\n", "lambda: a\n" };
        yield return new object[] { "lambda_defaults", "from __future__ import annotations\nx: lambda a=(x,y), b=f(1,2), *, c=\"q\": (a,b,c)\nprint(__annotations__[\"x\"])\n", "lambda a=(x, y), b=f(1, 2), *, c='q': (a, b, c)\n" };
        yield return new object[] { "list_comp", "from __future__ import annotations\nx: [x + y for x in data if a if b for y in other if c and d]\nprint(__annotations__[\"x\"])\n", "[x + y for x in data if a if b for y in other if c and d]\n" };
        yield return new object[] { "dict_comp", "from __future__ import annotations\nx: {x:y for x, y in data if p or q}\nprint(__annotations__[\"x\"])\n", "{x: y for x, y in data if p or q}\n" };
        yield return new object[] { "set_comp", "from __future__ import annotations\nx: {x for x in data}\nprint(__annotations__[\"x\"])\n", "{x for x in data}\n" };
        yield return new object[] { "generator_call", "from __future__ import annotations\nx: tuple(x for x in data if ok)\nprint(__annotations__[\"x\"])\n", "tuple(x for x in data if ok)\n" };
        yield return new object[] { "nested_comp_target", "from __future__ import annotations\nx: [x for x, (y, z) in data]\nprint(__annotations__[\"x\"])\n", "[x for x, (y, z) in data]\n" };
        yield return new object[] { "list_comp_target", "from __future__ import annotations\nx: [x for [x, [y, z]] in data]\nprint(__annotations__[\"x\"])\n", "[x for [x, [y, z]] in data]\n" };
        yield return new object[] { "private_comp", "from __future__ import annotations\nx: [__x for __x, *__rest in __data]\nprint(__annotations__[\"x\"])\n", "[__x for __x, *__rest in __data]\n" };
        yield return new object[] { "lambda_walrus", "from __future__ import annotations\nx: lambda: (a := 1)\nprint(__annotations__[\"x\"])\n", "lambda: (a := 1)\n" };
        yield return new object[] { "lambda_walrus_value", "from __future__ import annotations\nx: lambda: (a := b + c)\nprint(__annotations__[\"x\"])\n", "lambda: (a := (b + c))\n" };
        yield return new object[] { "lambda_yield", "from __future__ import annotations\nx: lambda: (yield Missing)\nprint(__annotations__[\"x\"])\n", "lambda: (yield Missing)\n" };
        yield return new object[] { "lambda_yield_from", "from __future__ import annotations\nx: lambda: (yield from Missing)\nprint(__annotations__[\"x\"])\n", "lambda: (yield from Missing)\n" };
        yield return new object[] { "lambda_bare_yield", "from __future__ import annotations\nx: lambda: (yield)\nprint(__annotations__[\"x\"])\n", "lambda: (yield)\n" };
        yield return new object[] { "fstring", "from __future__ import annotations\nx: f\"text{{{{{value!a:>{width}}}}}}\"\nprint(__annotations__[\"x\"])\n", "f'text{{{{{value!a:>{width}}}}}}'\n" };
        yield return new object[] { "fstring_debug", "from __future__ import annotations\nx: f\"{ a + b = }\"\nprint(__annotations__[\"x\"])\n", "f' a + b = {a + b!r}'\n" };
        yield return new object[] { "fstring_dict", "from __future__ import annotations\nx: f\"{{{ {a:b} }}}\"\nprint(__annotations__[\"x\"])\n", "f'{{{ {a: b}}}}'\n" };
        yield return new object[] { "fstring_conditional", "from __future__ import annotations\nx: f\"{a if b else c}\"\nprint(__annotations__[\"x\"])\n", "f'{(a if b else c)}'\n" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesPython313Metadata(string name, string source, string expected)
    {
        _ = name;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
