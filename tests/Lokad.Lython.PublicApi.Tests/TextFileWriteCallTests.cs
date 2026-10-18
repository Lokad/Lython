using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextFileWriteCallTests
{
    public static TheoryData<string, string, string, string> Cases => new()
    {
        { "unicode-counts", "with open('/out','w',newline='') as file:\n print(file.write('a'),file.write('λ\\n'),file.write('😀'),file.write(''),file.tell())", "1 2 1 0 8\n", "aλ\n😀" },
        { "exposed-expanded", "with open('/out','w') as file:\n method=file.write\n print(method('a'),method(*['b']),method(*['c'],**{}))", "1 1 1\n", "abc" },
        { "receiver-before-argument", "file=open('/out','w')\nother=open('/other','w')\ndef argument():\n global file\n file=other\n return 'kept'\nprint(file.write(argument()))", "4\n", "kept" },
        { "custom-property", "events=[]\ndef accept(value):\n events.append(value)\n return len(value)\nclass Sink:\n @property\n def write(self):\n  events.append('lookup')\n  return accept\ndef receiver():\n events.append('receiver')\n return Sink()\ndef argument():\n events.append('argument')\n return 'abc'\nprint(receiver().write(argument()),events)\nwith open('/out','w') as file: file.write('ok')", "3 ['receiver', 'lookup', 'argument', 'abc']\n", "ok" },
        { "binary-fallback", "with open('/out','wb') as file: print(file.write(b'abc'))", "3\n", "abc" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryAndGeneralCallsPreservePythonBehavior(string name, string source, string expectedOutput, string expectedFile)
    {
        using var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            using var result = asynchronous ? await compiled.RunAsync(host) : compiled.Run(host);
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expectedOutput, result.StandardOutput);
            if (name == "binary-fallback")
                Assert.Equal(System.Text.Encoding.UTF8.GetBytes(expectedFile), host.ReadBytes("/out"));
            else Assert.Equal(expectedFile, host.ReadText("/out"));
            if (name == "receiver-before-argument") Assert.Equal("", host.ReadText("/other"));
        }
    }
}
