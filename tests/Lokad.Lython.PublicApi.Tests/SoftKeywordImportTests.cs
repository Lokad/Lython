using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SoftKeywordImportTests
{
    [Fact]
    public async Task SoftKeywordsWorkAsHostMediatedModuleAndMemberNames()
    {
        var compiled = new LythonEngine().Compile("""
            import match.case
            print(match.value, match.case.value)
            from match.case import match as case, case as type
            print(case, type)
            import case as match
            from case import match as case
            print(match.case, case)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions
        {
            SourcePath = "/main.py",
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal)
            {
                "match", "match.case", "case", "/match/__init__.py", "/match/case.py", "/case.py"
            }
        };
        foreach (var asynchronous in new[] { false, true })
        {
            ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
            Action<string, string> seed = host is DelayedLythonHost delayedHost
                ? delayedHost.SeedFile : ((MockLythonHost)host).SeedFile;
            seed("/match/__init__.py", "value=1\n");
            seed("/match/case.py", "value=2\nmatch=3\ncase=4\n");
            seed("/case.py", "match=5\ncase=6\n");
            var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("1 2\n3 4\n6 5\n", result.StandardOutput);
            if (host is DelayedLythonHost delayed) Assert.True(delayed.CompletedAsynchronously > 0);
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "alias_match","import math as match\nprint(match.sqrt(9))","3.0\n" };
        yield return new object[] { "alias_case","import math as case\nprint(case.factorial(4))","24\n" };
        yield return new object[] { "member_alias_match","from math import sqrt as match\nprint(match(16))","4.0\n" };
        yield return new object[] { "member_alias_case","from math import sqrt as case\nprint(case(25))","5.0\n" };
        yield return new object[] { "multiple_aliases","import math as match, math as case\nprint(match is case)","True\n" };
        yield return new object[] { "grouped_aliases","from math import (sqrt as match, factorial as case,)\nprint(match(36),case(3))","6.0 6\n" };
        yield return new object[] { "star_control","from math import *\nprint(sqrt(49))","7.0\n" };
        yield return new object[] { "class_alias","class C:\n import math as match\n from math import sqrt as case\nprint(C.match.sqrt(64),C.case(81))","8.0 9.0\n" };
        yield return new object[] { "directive_alias","match=None\ndef load():\n global match\n import math as match\nload()\nprint(match.sqrt(100))","10.0\n" };
        yield return new object[] { "pattern_composition","import math as match\nfrom math import sqrt as case\nmatch 1:\n case 1:print(match.sqrt(121),case(144))","11.0 12.0\n" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CompatibleBindings(string name, string source, string expected)
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

    [Theory]
    [InlineData("import math as if")]
    [InlineData("from math import sqrt as class")]
    [InlineData("from math import return")]
    [InlineData("import if")]
    [InlineData("from math.if import x")]
    public void InvalidNamesAndScopeConflictsFailBeforeEffects(string source)
        => Assert.False(new LythonEngine().Compile("print('effect')\n" + source).IsValid);
}
