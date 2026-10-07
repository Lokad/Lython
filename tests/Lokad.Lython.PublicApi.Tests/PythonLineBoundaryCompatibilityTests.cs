using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PythonLineBoundaryCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "python-splitlines-controls", "for code in [10,11,12,13,28,29,30,31,133,8232,8233]:\n    s='a'+chr(code)+'b'\n    print(code,len(s.splitlines()),len(s.splitlines(True)),''.join(s.splitlines(True))==s)\n", "10 2 2 True\n11 2 2 True\n12 2 2 True\n13 2 2 True\n28 2 2 True\n29 2 2 True\n30 2 2 True\n31 1 1 True\n133 2 2 True\n8232 2 2 True\n8233 2 2 True\n" };
        yield return new object[] { "linebreak-values-and-terminal-boundaries", "for sep in ['\\n','\\r','\\r\\n','\\v','\\f','\\x1c','\\x1d','\\x1e','\\x85','\\u2028','\\u2029']:\n    for head,tail in [('',''),('a',''),('','b'),('a😀','é')]:\n        text=head+sep+tail\n        expected=[head]+([tail] if tail else [])\n        ended=[head+sep]+([tail] if tail else [])\n        print(text.splitlines()==expected,text.splitlines(True)==ended)\nprint(''.splitlines(),''.splitlines(True))\nprint('a\\r\\n\\n'.splitlines()==['a',''],'a\\r\\n\\n'.splitlines(True)==['a\\r\\n','\\n'])\n", "True True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\n[] []\nTrue True\n" };
        yield return new object[] { "linebreak-negative-controls-and-bytes", "for code in [0,9,31,32,160,0x180e,0x200b,0xfeff,0x1f600]:\n    text='a'+chr(code)+'b'\n    print(code,text.splitlines()==[text],text.splitlines(True)==[text])\nfor code in [10,11,12,13,28,29,30,31,133]:\n    text=b'a'+bytes([code])+b'b'\n    print(code,len(text.splitlines()),len(text.splitlines(True)))\n", "0 True True\n9 True True\n31 True True\n32 True True\n160 True True\n6158 True True\n8203 True True\n65279 True True\n128512 True True\n10 2 2\n11 1 1\n12 1 1\n13 2 2\n28 1 1\n29 1 1\n30 1 1\n31 1 1\n133 1 1\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task MatchesPython(string name, string source, string expected)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
