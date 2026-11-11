from pathlib import Path
import json,platform
root=Path(__file__).resolve().parent;repo=Path('D:/code/Lython')
cases=[('"bad\\q"',True),('"é😀\\q"',True),('"\\u"',True),('"\\u12g4"',True),('"\\u123"',True),('"é\\u12z4"',True),('"unterminated',True),('"end\\',True),('"a\0b"',True),('"\n\\q"',False),('"\\ud800\\u12g4"',True),('"\\ud800',True)]
cases += [(chr(34)+chr(92)+token,True)for token in ["u0061","u0000","ud800"+chr(92)+"udc00"]]
rows=[]
for doc,strict in cases:
    try: json.loads(doc,strict=strict);raise AssertionError(doc)
    except json.JSONDecodeError as e: rows.append(dict(document=doc,strict=strict,msg=e.msg,pos=e.pos,line=e.lineno,col=e.colno))
values=['"\\\"\\\\\\/\\b\\f\\n\\r\\t"','"\\u0000"','"\\u007f\\u0080\\u07ff\\u0800\\uffff"','"\\ud800\\udc00"','"\\udbff\\udfff"','"café😀\\n\\u03bb"']
vrows=[dict(document=doc,value=json.loads(doc))for doc in values]
literal=lambda x:json.dumps(x,ensure_ascii=False)
data='\n'.join('    [InlineData('+', '.join([literal(r['document']),str(r['strict']).lower(),literal(r['msg']),str(r['pos']),str(r['line']),str(r['col'])])+')]'for r in rows)
value_data='\n'.join('    [InlineData('+literal(r['document'])+', '+literal(r['value'])+')]'for r in vrows)
source='''using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonStringLexingTests
{
    // Independent CPython 3.13.2 error messages and original scalar coordinates.
    [Theory]
ERROR_DATA
    public async Task StringErrorsUsePythonMessagesAndOriginalCoordinates(string document, bool strict,
        string message, int position, int line, int column)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            import json
            try:
                json.loads(source, strict=strict)
            except json.JSONDecodeError as error:
                print(error.msg)
                print(error.pos, error.lineno, error.colno, error.doc is source, error.args[0] == str(error))
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?>
            { ["source"] = document, ["strict"] = strict } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal($"{message}\\n{position} {line} {column} True True\\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
VALUE_DATA
    public async Task EscapesDecodeIntoIndependentRetainedStrings(string document, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("import json\\nreturn json.loads(source)", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = document } };
        using (var first = script.Run(new MockLythonHost(), options))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            using (var second = await script.RunAsync(new MockLythonHost(), options))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, first.ReturnValue);
                Assert.Equal(expected, second.ReturnValue);
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
            Assert.Equal(expected, first.ReturnValue);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("\\"\\\\ud800\\"")]
    [InlineData("\\"\\\\udc00\\"")]
    [InlineData("\\"\\\\ud800x\\"")]
    [InlineData("\\"\\\\ud800\\\\u0041\\"")]
    public async Task UnpairedSurrogatesFailExplicitlyWithoutLeakingBackendErrors(string document)
    {
        using var script = new LythonEngine().Compile("import json\\nreturn json.loads(source)");
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = document } };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options);
            Assert.False(result.Success);
            Assert.Equal("ValueError", result.Failure?.ExceptionType);
            Assert.Equal("Lython does not support unpaired Unicode surrogates in JSON strings.", result.Failure?.Message);
        }
    }
}
'''.replace('ERROR_DATA',data).replace('VALUE_DATA',value_data)
(repo/'tests/Lokad.Lython.PublicApi.Tests/JsonStringLexingTests.cs').write_text(source,encoding='utf-8',newline='\n')
(root/'cpython-string-goldens.json').write_text(json.dumps(dict(reference=platform.python_version(),errors=rows,values=vrows),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Generated independent CPython',len(rows),'error and',len(vrows),'value goldens.')
