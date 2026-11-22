using System.Collections;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class JsonIteratorTerminalOwnershipTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailureRelinquishesSortedStorageBeforeRefund(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var input = new PyDict(context.MemoryGovernor, span);
        for (var i = 0; i < 32; i++) input.SetItem(PyString.FromString(i.ToString("D2")), new object());
        var inputCharge = budget.ReservedBytes;
        var options = new LythonRuntime.JsonDumpOptions(false, true, true, true, null,
            PyString.FromString(", "), PyString.FromString(": "), null, true);
        var iterator = LythonRuntime.JsonEncodeIterator.Create(input, options, context, span);
        if (asynchronous) Assert.True((await iterator.TryMoveNextAsync()).HasValue);
        else Assert.True(iterator.TryMoveNext().HasValue);
        var activeCharge = budget.ReservedBytes;
        Assert.True(activeCharge >= inputCharge + 32 * 32);
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) _ = await iterator.TryMoveNextAsync();
            else iterator.TryMoveNext();
        });
        Assert.Equal("TypeError", error.ExceptionType);
        Assert.InRange(budget.ReservedBytes, inputCharge, activeCharge - 32 * 32);
        // A live failed iterator must not retain the private copied entries
        // whose charge has just been returned. Input aliases remain funded.
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        var type = iterator.GetType();
        var nested = (ICollection)type.GetField("_nestedFrames", fields)!.GetValue(iterator)!;
        Assert.Empty(nested);
        var outer = type.GetField("_outerFrame", fields)!.GetValue(iterator)!;
        Assert.Null(outer.GetType().GetField("SortedEntries")!.GetValue(outer));
        Assert.Null(type.GetField("_rootValue", fields)!.GetValue(iterator));
        Assert.Equal(32, input.Count);
        Assert.False(iterator.TryMoveNext().HasValue);
        GC.KeepAlive(iterator);
        GC.KeepAlive(input);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
