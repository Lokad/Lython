using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AsciiFieldConversionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedReprIsEscapedAfterDynamicSpecifierEffects(bool imported)
    {
        var source = """
            class C:
             def __repr__(self):
              with open('/value.txt') as f: value=f.read()
              print('repr')
              return value
            def spec():
             with open('/width.txt') as f: value=f.read()
             print('spec')
             return value
            print(f"{C()!a:{spec()}}")
            """;
        var compiled = new LythonEngine().Compile(imported ? "import helper" : source);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions
        {
            SourcePath = "/main.py",
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper", "/helper.py" }
        };
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value.txt", "\u00e9\U0001f600");
        syncHost.SeedFile("/width.txt", ">16");
        syncHost.SeedFile("/helper.py", source);
        var sync = compiled.Run(syncHost, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("repr\nspec\n  \\xe9\\U0001f600\n", sync.StandardOutput);
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "\u00e9\U0001f600");
        host.SeedFile("/width.txt", ">16");
        host.SeedFile("/helper.py", source);
        var result = await compiled.RunAsync(host, options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 2);
    }

    [Fact]
    public async Task AsciiExpansionIsLimitedBeforeFormatTruncation()
    {
        var compiled = new LythonEngine().Compile("print(f'{chr(233)*32!a:.1}')");
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxStringLength = 64 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("maximum string length", result.Failure?.Message);
            Assert.Empty(result.StandardOutput);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EscapedResultsKeepGovernedMemoryOwnership(bool retained)
    {
        var source = retained
            ? "x=[]\nfor i in range(1000):x.append(f'{chr(233)*32!a}')"
            : "for i in range(5000):f'{chr(233)*32!a}'\nprint('done')";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = retained ? 32768 : 524288 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.Equal(!retained, result.Success);
            if (retained) Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            else Assert.Equal("done\n", result.StandardOutput);
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "r_order","class C:\n def __repr__(self):print(\"r\");return \"value\"\ndef spec():print(\"spec\");return \">8\"\nprint(f\"{C()!r:{spec()}}\")","r\nspec\n   value\n" };
        yield return new object[] { "s_order","class C:\n def __str__(self):print(\"s\");return \"value\"\ndef spec():print(\"spec\");return \">8\"\nprint(f\"{C()!s:{spec()}}\")","s\nspec\n   value\n" };
        yield return new object[] { "conversion_failure_precedes_spec","class C:\n def __repr__(self):raise ValueError(\"repr failure\")\ndef spec():print(\"spec\");return \">8\"\ntry:print(f\"{C()!a:{spec()}}\")\nexcept ValueError as e:print(str(e))","repr failure\n" };
        yield return new object[] { "bmp","print(f\"{chr(233)!a}\",f\"{chr(0x100)!a}\",f\"{chr(0xffff)!a}\")","'\\xe9' '\\u0100' '\\uffff'\n" };
        yield return new object[] { "astral","print(f\"{chr(0x1f600)!a}\",f\"{chr(0x10ffff)!a}\")","'\\U0001f600' '\\U0010ffff'\n" };
        yield return new object[] { "containers","x=[chr(233),{chr(0x100):chr(0x1f600)}]\nprint(f\"{x!a}\")","['\\xe9', {'\\u0100': '\\U0001f600'}]\n" };
        yield return new object[] { "control_and_quotes","x=chr(233)+chr(39)+chr(34)+chr(92)+chr(10)+chr(9)\nprint(f'{x!a}')","'\\xe9\\'\"\\\\\\n\\t'\n" };
        yield return new object[] { "ascii_control","x='plain'\nprint(f'{x!a}',f'{x!r}',f'{x!s}')","'plain' 'plain' plain\n" };
        yield return new object[] { "after_conversion_format","x=chr(233)\nprint(f\"{x!a:>12}\",f\"{x!a:.3}\",f\"{x!r:>12}\")","      '\\xe9' '\\x          '\u00e9'\n" };
        yield return new object[] { "debug","x=chr(233)\nprint(f\"{x=!a}\")","x='\\xe9'\n" };
        yield return new object[] { "custom_repr","class C:\n def __repr__(self):return chr(233)+chr(0x100)+chr(0x1f600)\n def __str__(self):return \"wrong\"\nprint(f\"{C()!a}\",f\"{C()!a:>25}\")","\\xe9\\u0100\\U0001f600      \\xe9\\u0100\\U0001f600\n" };
        yield return new object[] { "evaluation_order","def value():print(\"value\");return C()\ndef spec():print(\"spec\");return \">8\"\nclass C:\n def __repr__(self):print(\"repr\");return chr(233)\nprint(f\"{value()!a:{spec()}}\")","value\nrepr\nspec\n    \\xe9\n" };
        yield return new object[] { "bad_repr","class C:\n def __repr__(self):return 7\ntry:print(f\"{C()!a}\")\nexcept TypeError:print(\"type failure\")","type failure\n" };
        yield return new object[] { "raising_repr","class C:\n def __repr__(self):raise ValueError(\"repr failure\")\ntry:print(f\"{C()!a}\")\nexcept ValueError as e:print(str(e))","repr failure\n" };
        yield return new object[] { "bad_format","try:print(f\"{chr(233)!a:d}\")\nexcept ValueError:print(\"format failure\")","format failure\n" };
        yield return new object[] { "string_format_shared","print(\"{0!a:>10}\".format(chr(233)))","    '\\xe9'\n" };
        yield return new object[] { "string_format_map_shared","print(\"{x!a:>10}\".format_map({\"x\":chr(233)}))","    '\\xe9'\n" };
        yield return new object[] { "generator_composition","def g():yield f\"{yield chr(233)!a:{yield \">8\"}}\"\nx=g()\nprint(ascii(next(x)),x.send(chr(233)),x.send(\">8\"))","'\\xe9' >8   '\\xe9'\n" };
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

}
