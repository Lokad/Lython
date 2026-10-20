namespace Lokad.Lython.Tests.Harness;

internal static class NativeOperationSources
{
    internal static string RefusedConstruction(string operation) => operation switch
    {
        "bytes" => "part=b'" + new string('x', 100000) + "'\nreturn len(part+part)",
        "replace" => "return len('xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'.replace('x','y'*4096))",
        "expandtabs" => "return len('\\t.'.expandtabs(500000))",
        "translate" => "return len(('x'*2048).translate({120:'y'*2048}))",
        _ => "return len('" + new string('A', 100000) + "'." + operation + "())"
    };
}
