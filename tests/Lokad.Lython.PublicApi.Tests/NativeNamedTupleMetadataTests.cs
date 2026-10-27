using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeNamedTupleMetadataTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MutableNativeMetadataIsIsolatedAndStaysFundedUntilResultCleanup(bool cacheInfo, bool asynchronous)
    {
        var prefix = cacheInfo ? """
            import functools
            @functools.lru_cache()
            def first(): return 1
            @functools.lru_cache()
            def second(): return 2
            a=first.cache_info()
            b=second.cache_info()
            """ : """
            import sys
            a=sys.version_info
            b=sys.implementation.version
            assert a is sys.version_info
            assert sys.implementation is sys.implementation
            """;
        var write = prefix + "\n" + """
            assert type(a) is type(b)
            metadata=a._field_defaults
            assert metadata is b._field_defaults
            metadata['__lython_metadata_payload__']=[None]*4096
            print(len(metadata['__lython_metadata_payload__']))
            return metadata
            """;
        var read = prefix + "\nprint('__lython_metadata_payload__' in a._field_defaults)\n";
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var writer = new LythonEngine().Compile(write, root))
        using (var reader = new LythonEngine().Compile(read, root))
        {
            Assert.True(writer.IsValid, string.Join("; ", writer.Diagnostics.Select(d => d.Message)));
            Assert.True(reader.IsValid, string.Join("; ", reader.Diagnostics.Select(d => d.Message)));
            var baseline = root.ReservedBytes;
            using (var result = asynchronous ? await writer.RunAsync(new MockLythonHost()) : writer.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("4096\n", result.StandardOutput);
                var retained = root.ReservedBytes;
                Assert.True(retained > baseline);
                using (var other = asynchronous ? await reader.RunAsync(new MockLythonHost()) : reader.Run(new MockLythonHost()))
                {
                    Assert.True(other.Success, other.Failure?.Message);
                    Assert.Equal("False\n", other.StandardOutput);
                }
                Assert.Equal(retained, root.ReservedBytes);
            }
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
