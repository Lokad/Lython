using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ImmutableRepetitionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepetitionPreservesUnicodeIndexProtocolsAndElementAliases(bool asynchronous)
    {
        const string source = """
            data=b'abc'*4097
            text='é😀'*4097
            marker=[]
            values=(marker,)*4097
            marker.append(7)
            assert len(data)==12291 and data[-7:]==b'cabcabc'
            assert len(text)==8194 and text[-3:]=='😀é😀'
            assert values[0] is marker and values[-1] is marker
            assert values[-1]==[7]
            assert b'abc'*-1==b'' and 'é😀'*0=='' and (marker,)*-1==()
            assert (2**40)*b''==b'' and (2**40)*()==()
            events=[]
            class Count:
                def __index__(self):
                    events.append('index')
                    return 2
            assert b'ab'*Count()==b'abab'
            assert Count()*'é😀'=='é😀é😀'
            assert (marker,)*Count()==(marker,marker)
            assert events==['index','index','index']
            class LargeCount:
                def __index__(self):return 2**40
            assert ''*LargeCount()==''
            class HugeCount:
                def __index__(self):return 2**100
            for operation in [lambda:b''*HugeCount(),lambda:''*HugeCount(),
                              lambda:()*HugeCount(),lambda:b''*-(2**100)]:
                try:operation()
                except OverflowError:pass
                else:assert False
            return data,text,len(values)
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                Assert.Equal(12291, Assert.IsType<byte[]>(values[0]).Length);
                Assert.Equal(12291, Assert.IsType<string>(values[1]).Length);
                Assert.Equal("4097", values[2]?.ToString());
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BytesAdmissionRefusalRemainsTerminalThroughSuppressionAndFinally(bool asynchronous)
    {
        const string source = """
            try:
                try:
                    value=b'x'*1048576
                except MemoryError:
                    print('suppressed')
            finally:
                return 'replacement'
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var budget = new LythonMemoryBudget(256 * 1024);
        using (var result = asynchronous
                   ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                   : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
