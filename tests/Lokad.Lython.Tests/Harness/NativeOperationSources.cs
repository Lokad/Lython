namespace Lokad.Lython.Tests.Harness;

internal static class NativeOperationSources
{
    internal static string RefusedConstruction(string operation) => operation switch
    {
        "bytes" => "part=b'" + new string('x', 100000) + "'\nreturn len(part+part)",
        "byte-slice" => "part=b'" + new string('x', 100000) + "'\nreturn len(part[1:])",
        "byte-stepped-slice" => "part=b'" + new string('x', 100000) + "'\nreturn len(part[::-2])",
        "byte-repr" => "return len(repr(b'" + new string('x', 100000) + "'))",
        "byte-join" => "part=b'" + new string('x', 100000) + "'\nreturn len(b''.join([part,part]))",
        "byte-join-input" => "import itertools\nreturn len(b''.join(itertools.repeat(b'',8192)))",
        "byte-hex" => "return len(b'" + new string('x', 100000) + "'.hex())",
        "byte-translate" => "return len(b'" + new string('x', 100000) + "'.translate(bytes.maketrans(b'x',b'y')))",
        "byte-expandtabs" => "return len(b'\\t.'.expandtabs(500000))",
        "byte-decode" => "return len(b'" + new string('x', 200000) + "'.decode())",
        "byte-replace" => "return len(b'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'.replace(b'x',b'y'*4096))",
        "byte-pad" => "return len(b'x'.ljust(500000))",
        "byte-zfill" => "return len(b'1'.zfill(500000))",
        "byte-lower" => "return len(b'" + new string('A', 100000) + "'.lower())",
        "json-integer-indent" => "import json\nreturn len(json.dumps([1],indent=100000))",
        "json-string-indent" => "import json\nreturn len(json.dumps([1],indent='" + new string('x', 100000) + "'))",
        "replace" => "return len('xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'.replace('x','y'*4096))",
        "expandtabs" => "return len('\\t.'.expandtabs(500000))",
        "translate" => "return len(('x'*2048).translate({120:'y'*2048}))",
        _ => "return len('" + new string('A', 100000) + "'." + operation + "())"
    };
}
