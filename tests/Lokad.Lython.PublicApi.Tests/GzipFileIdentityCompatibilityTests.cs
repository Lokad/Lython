using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GzipFileIdentityCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "gzipfile-abcmeta-hierarchy-stable-identity", "import gzip\nmeta=type(gzip.GzipFile)\nprint(meta.__name__,meta.__module__)\nprint(meta.__bases__[0] is type,meta.__mro__[0] is meta,meta.__mro__[1] is type,meta.__mro__[2] is object)\nprint(meta.__bases__ is meta.__bases__,meta.__mro__ is meta.__mro__)\nprint(isinstance(meta,type),issubclass(meta,type))\n", "ABCMeta abc\nTrue True True True\nTrue True\nTrue True\n" };
        yield return new object[] { "gzipfile-io-error-type-without-import", "import gzip\nclass Raw:\n    def write(self,data): return len(data)\nwriter=gzip.GzipFile(fileobj=Raw(),mode='wb',mtime=0)\ntry: writer.readline()\nexcept Exception as error:\n    print(type(error).__module__,type(error).__name__,isinstance(error,OSError),isinstance(error,ValueError))\nwriter.close()\n", "io UnsupportedOperation True True\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task IdentityFollowsPython(string name,string source,string expected)
    {
        var script=new LythonEngine().Compile(source);
        Assert.True(script.IsValid,name+": "+string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
        foreach(var result in new[]{script.Run(new MockLythonHost()),await script.RunAsync(new MockLythonHost())})
        {
            Assert.True(result.Success,name+": "+result.Failure?.Message);
            Assert.Equal(expected,result.StandardOutput);
        }
    }
}
