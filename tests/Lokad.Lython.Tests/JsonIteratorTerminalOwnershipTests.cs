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
        var iterator = new LythonRuntime.JsonEncodeIterator(input, options, context, span);
        if (asynchronous) Assert.True((await iterator.TryMoveNextAsync()).HasValue);
        else Assert.True(iterator.TryMoveNext(out _));
        var activeCharge = budget.ReservedBytes;
        Assert.True(activeCharge >= inputCharge + 32 * 32);
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) _ = await iterator.TryMoveNextAsync();
            else iterator.TryMoveNext(out _);
        });
        Assert.Equal("TypeError", error.ExceptionType);
        Assert.InRange(budget.ReservedBytes, inputCharge, activeCharge - 32 * 32);
        // A live failed iterator must not retain the private copied entries
        // whose charge has just been returned. Input aliases remain funded.
        var frames = (ICollection)iterator.GetType().GetField("_stack", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(iterator)!;
        Assert.Empty(frames);
        Assert.Null(iterator.GetType().GetField("_rootValue", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(iterator));
        Assert.Equal(32, input.Count);
        Assert.False(iterator.TryMoveNext(out _));
        GC.KeepAlive(iterator);
        GC.KeepAlive(input);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
