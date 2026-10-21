namespace Lokad.Lython.Tests.Harness;

internal static class NativeOperationSources
{
    internal static string RefusedConstruction(string operation) => operation switch
    {
        "integer-to-bytes" => "return len((1).to_bytes(200000))",
        "decimal-digits" => "import decimal,itertools\nreturn len(decimal.DecimalTuple(0,itertools.repeat(1,50000),0).digits)",
        "decimal-text" => "import decimal\nreturn decimal.Decimal('" + new string(' ', 100000) + "1').is_zero()",
        "decimal-context-text" => "import decimal\nreturn decimal.Context().create_decimal('" + new string(' ', 100000) + "1').is_zero()",
        "byte-sized" => "return len(bytes(200000))",
        "byte-iterable" => "import itertools\nreturn len(bytes(itertools.repeat(65,500000)))",
        "integer-byte-iterable" => "import itertools\nreturn len(int.from_bytes(itertools.repeat(0,500000)))",
        "byte-fromhex" => "return len(bytes.fromhex('" + new string('a', 100000) + "'))",
        "byte-rsplit" => "return len(b'" + new string(',', 32768) + "'.rsplit(b','))",
        "date-strftime" => "import datetime\nreturn len(datetime.date(2024,1,2).strftime('" + new string('x', 200000) + "'))",
        "date-format" => "import datetime\nreturn len(format(datetime.date(2024,1,2),'" + new string('x', 200000) + "'))",
        "time-strftime" => "import time\nreturn len(time.strftime('" + new string('x', 200000) + "'))",
        "date-strptime" => "import datetime\nreturn len(str(datetime.datetime.strptime('" + new string('x', 100000) + "','" + new string('x', 100000) + "')))",
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
