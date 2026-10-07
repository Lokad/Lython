using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NanSignCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "nan-sign-prerequisite", "import math\nfor spelling in ['nan','NaN','NAN','+nan','-nan',' +NaN ',' -NaN ']:\n    value=float(spelling)\n    print(repr(spelling),math.copysign(1.0,value),math.copysign(1.0,-value))\nprint(math.copysign(1.0,math.nan),math.copysign(1.0,-math.nan))\n", "'nan' 1.0 -1.0\n'NaN' 1.0 -1.0\n'NAN' 1.0 -1.0\n'+nan' 1.0 -1.0\n'-nan' -1.0 1.0\n' +NaN ' 1.0 -1.0\n' -NaN ' -1.0 1.0\n1.0 -1.0\n" };
        yield return new object[] { "nan-fromhex-signs", "import math\nfor spelling in ['nan','NaN','+nan','-nan',' +NaN ',' -NaN ']:\n    value=float.fromhex(spelling)\n    print(repr(spelling),math.copysign(1.0,value),math.copysign(1.0,-value))\n", "'nan' 1.0 -1.0\n'NaN' 1.0 -1.0\n'+nan' 1.0 -1.0\n'-nan' -1.0 1.0\n' +NaN ' 1.0 -1.0\n' -NaN ' -1.0 1.0\n" };
        yield return new object[] { "nan-json-constructor-signs", "import json,math\nfor value in [json.loads('NaN'),json.JSONDecoder().decode('NaN'),json.JSONDecoder().raw_decode('NaN')[0]]:\n    print(math.isnan(value),math.copysign(1.0,value),math.copysign(1.0,-value))\nprint(math.copysign(1.0,float(b'nan')),math.copysign(1.0,float(b'-nan')))\n", "True 1.0 -1.0\nTrue 1.0 -1.0\nTrue 1.0 -1.0\n1.0 -1.0\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task ConstructorsPreservePythonNanSigns(string name, string source, string expected)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
