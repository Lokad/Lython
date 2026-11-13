using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DictionaryIntegerValueTests
{
    [Fact]
    public void SharedKeysAndCopiesKeepPublishedIntegerAliasesAndIndependentValues()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var key = PyString.FromString("count", context.MemoryGovernor, span);
            var original = new PyDict(2, context.MemoryGovernor, span);
            original.SetItem(key, new BigInteger(1000));
            Assert.True(original.TryAccumulateWithoutAdmission(key, BigInteger.Zero, BigInteger.One, context, span));
            var copy = new PyDict(original);
            Assert.True(original.TryGetValue(key, out var value));
            Assert.True(copy.TryGetValue(key, out var copied));
            Assert.Same(value, copied);
            var layout = Assert.IsType<PyDict.RecordKeys>(original.ShareRecordKeys(context, span));
            var record = layout.CreateRecord(context, span, 1);
            record.SetRecordItem(key, new BigInteger(7), context, span);
            Assert.True(original.TryGetValue(key, out var shared));
            Assert.Same(value, shared);
            Assert.True(record.TryAccumulateWithoutAdmission(key, BigInteger.Zero, BigInteger.One, context, span));
            Assert.True(record.TryGetValue(key, out var updated));
            Assert.Equal(new BigInteger(8), updated);
            Assert.True(original.TryGetValue(key, out var unchanged));
            Assert.Same(value, unchanged);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
