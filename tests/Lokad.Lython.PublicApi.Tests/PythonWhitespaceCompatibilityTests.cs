using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PythonWhitespaceCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "python-whitespace-c0-controls", "for code in [9,10,11,12,13,28,29,30,31,32,133,160,8192,8232,8233,8239,8287,12288]:\n    s=chr(code)\n    print(code,s.isspace(),s.strip()=='',s.lstrip()=='',s.rstrip()=='',len(('a'+s+'b').split()),len(('a'+s+'b').rsplit()))\n", "9 True True True True 2 2\n10 True True True True 2 2\n11 True True True True 2 2\n12 True True True True 2 2\n13 True True True True 2 2\n28 True True True True 2 2\n29 True True True True 2 2\n30 True True True True 2 2\n31 True True True True 2 2\n32 True True True True 2 2\n133 True True True True 2 2\n160 True True True True 2 2\n8192 True True True True 2 2\n8232 True True True True 2 2\n8233 True True True True 2 2\n8239 True True True True 2 2\n8287 True True True True 2 2\n12288 True True True True 2 2\n" };
        yield return new object[] { "whitespace-full-inventory", "for code in [9, 10, 11, 12, 13, 28, 29, 30, 31, 32, 133, 160, 5760, 8192, 8193, 8194, 8195, 8196, 8197, 8198, 8199, 8200, 8201, 8202, 8232, 8233, 8239, 8287, 12288, 0, 27, 65, 6158, 8203, 65279, 128512]:\n    s=chr(code)\n    print(code,s.isspace(),s.strip()=='',s.strip(None)=='',s.lstrip()=='',s.rstrip()=='')\n", "9 True True True True True\n10 True True True True True\n11 True True True True True\n12 True True True True True\n13 True True True True True\n28 True True True True True\n29 True True True True True\n30 True True True True True\n31 True True True True True\n32 True True True True True\n133 True True True True True\n160 True True True True True\n5760 True True True True True\n8192 True True True True True\n8193 True True True True True\n8194 True True True True True\n8195 True True True True True\n8196 True True True True True\n8197 True True True True True\n8198 True True True True True\n8199 True True True True True\n8200 True True True True True\n8201 True True True True True\n8202 True True True True True\n8232 True True True True True\n8233 True True True True True\n8239 True True True True True\n8287 True True True True True\n12288 True True True True True\n0 False False False False False\n27 False False False False False\n65 False False False False False\n6158 False False False False False\n8203 False False False False False\n65279 False False False False False\n128512 False False False False False\n" };
        yield return new object[] { "whitespace-maxsplit-and-explicit-chars", "for code in [9,28,29,30,31,32,133,160,8192,12288]:\n    s=chr(code)\n    text=s+'a'+s+s+'b'+s\n    for count in [-1,0,1,2,10]:\n        left=text.split(None,count)\n        right=text.rsplit(None,count)\n        print(code,count,len(left),len(right),left==(['a','b'] if count!=0 and count!=1 else [text.lstrip()] if count==0 else ['a','b'+s]),right==(['a','b'] if count!=0 and count!=1 else [text.rstrip()] if count==0 else [s+'a','b']))\n    print(text.strip('')==text,text.strip(s)=='a'+s+s+'b',text.split(s)==['','a','','b',''])\n", "9 -1 2 2 True True\n9 0 1 1 True True\n9 1 2 2 True True\n9 2 2 2 True True\n9 10 2 2 True True\nTrue True True\n28 -1 2 2 True True\n28 0 1 1 True True\n28 1 2 2 True True\n28 2 2 2 True True\n28 10 2 2 True True\nTrue True True\n29 -1 2 2 True True\n29 0 1 1 True True\n29 1 2 2 True True\n29 2 2 2 True True\n29 10 2 2 True True\nTrue True True\n30 -1 2 2 True True\n30 0 1 1 True True\n30 1 2 2 True True\n30 2 2 2 True True\n30 10 2 2 True True\nTrue True True\n31 -1 2 2 True True\n31 0 1 1 True True\n31 1 2 2 True True\n31 2 2 2 True True\n31 10 2 2 True True\nTrue True True\n32 -1 2 2 True True\n32 0 1 1 True True\n32 1 2 2 True True\n32 2 2 2 True True\n32 10 2 2 True True\nTrue True True\n133 -1 2 2 True True\n133 0 1 1 True True\n133 1 2 2 True True\n133 2 2 2 True True\n133 10 2 2 True True\nTrue True True\n160 -1 2 2 True True\n160 0 1 1 True True\n160 1 2 2 True True\n160 2 2 2 True True\n160 10 2 2 True True\nTrue True True\n8192 -1 2 2 True True\n8192 0 1 1 True True\n8192 1 2 2 True True\n8192 2 2 2 True True\n8192 10 2 2 True True\nTrue True True\n12288 -1 2 2 True True\n12288 0 1 1 True True\n12288 1 2 2 True True\n12288 2 2 2 True True\n12288 10 2 2 True True\nTrue True True\n" };
        yield return new object[] { "byte-whitespace-stays-ascii", "for code in [9,10,11,12,13,28,29,30,31,32,133,160]:\n    s=bytes([code])\n    print(code,s.isspace(),s.strip()==b'',len((b'a'+s+b'b').split()),len((b'a'+s+b'b').rsplit()))\nprint(''.isspace(),''.strip(),''.split(),''.rsplit())\n", "9 True True 2 2\n10 True True 2 2\n11 True True 2 2\n12 True True 2 2\n13 True True 2 2\n28 False False 1 1\n29 False False 1 1\n30 False False 1 1\n31 False False 1 1\n32 True True 2 2\n133 False False 1 1\n160 False False 1 1\nFalse  [] []\n" };
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
