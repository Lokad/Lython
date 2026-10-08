using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipFileWarningCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "gzipfile-inferred-writing-mode-futurewarning", "import gzip,io\nclass Raw:\n    mode='wb'\n    def __init__(self): self.buffer=io.BytesIO()\n    def write(self,data): return self.buffer.write(data)\nfor index in range(2):\n    raw=Raw()\n    writer=gzip.GzipFile(fileobj=raw,mtime=0)\n    writer.write(b'payload')\n    writer.close()\n    print(writer.mode,gzip.decompress(raw.buffer.getvalue()))\n", "wb b'payload'\nwb b'payload'\n", "/warning.py:8: FutureWarning: GzipFile was opened for writing, but this will change in future Python releases.  Specify the mode argument for opening it for writing.\n" };
        yield return new object[] { "gzipfile-explicit-writing-mode-no-warning", "import gzip,io\nclass Raw:\n    mode='wb'\n    def __init__(self): self.buffer=io.BytesIO()\n    def write(self,data): return self.buffer.write(data)\nfor index in range(2):\n    raw=Raw()\n    writer=gzip.GzipFile(fileobj=raw,mode='wb',mtime=0)\n    writer.write(b'payload')\n    writer.close()\n    print(writer.mode,gzip.decompress(raw.buffer.getvalue()))\n", "wb b'payload'\nwb b'payload'\n", "" };
        yield return new object[] { "gzipfile-warning-different-call-locations", "import gzip,io\nclass Raw:\n    mode='wb'\n    def __init__(self): self.buffer=io.BytesIO()\n    def write(self,data): return self.buffer.write(data)\nfor index in range(2):\n    raw=Raw()\n    writer=gzip.GzipFile(fileobj=raw,mtime=0)\n    writer.write(b'payload')\n    writer.close()\n    print(writer.mode,gzip.decompress(raw.buffer.getvalue()))\nraw=Raw()\nwriter=gzip.GzipFile(fileobj=raw,mtime=0)\nwriter.close()\nprint('other',writer.mode)\n", "wb b'payload'\nwb b'payload'\nother wb\n", "/warning.py:8: FutureWarning: GzipFile was opened for writing, but this will change in future Python releases.  Specify the mode argument for opening it for writing.\n/warning.py:13: FutureWarning: GzipFile was opened for writing, but this will change in future Python releases.  Specify the mode argument for opening it for writing.\n" };
        yield return new object[] { "futurewarning-exception-hierarchy", "print(issubclass(FutureWarning,Warning),issubclass(FutureWarning,Exception))\nprint(FutureWarning.__module__,FutureWarning.__name__,FutureWarning.__bases__)\ntry: raise FutureWarning('notice')\nexcept Warning as error: print(type(error) is FutureWarning,str(error))\n", "True True\nbuiltins FutureWarning (<class 'Warning'>,)\nTrue notice\n", "" };
        yield return new object[] { "gzipfile-inferred-mode-warns-before-invalid-level", "import gzip,io\nclass Raw:\n    mode='wb'\n    def __init__(self): self.buffer=io.BytesIO()\n    def write(self,data): return self.buffer.write(data)\nraw=Raw()\ntry: gzip.GzipFile(fileobj=raw,compresslevel=10,mtime=0)\nexcept ValueError: print('invalid',len(raw.buffer.getvalue()),raw.buffer.closed)\n", "invalid 0 False\n", "/warning.py:7: FutureWarning: GzipFile was opened for writing, but this will change in future Python releases.  Specify the mode argument for opening it for writing.\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task InferredModesAndWarningIdentityFollowPython(string name, string source, string expected, string stderr)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions { SourcePath = "/warning.py" };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            Assert.Equal(stderr, result.StandardError);
        }
    }
}
