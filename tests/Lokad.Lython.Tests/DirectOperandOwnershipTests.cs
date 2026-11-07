using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DirectOperandOwnershipTests
{
    [Theory]
    [InlineData(false, 0), InlineData(true, 0)]
    [InlineData(false, 1), InlineData(true, 1)]
    [InlineData(false, 2), InlineData(true, 2)]
    public async Task ThreeInputStoreKeepsSnapshotsThroughProtocolRebindingAndCleanup(bool asynchronous, int failureKind)
    {
        const string source = """
            def place(receiver,key,value):
             receiver[key]=value
             return value
            class Target:
             def __init__(self):self.items=[]
             def __setitem__(self,key,value):
              global receiver,token,rhs
              receiver=None
              token=None
              rhs=None
              hold(self,key,value)
              self.items.append((key,value))
            receiver=Target()
            token=['key']
            rhs=['original']
            original_receiver=receiver
            original_key=token
            original_value=rhs
            items=receiver.items
            finished=False
            observe()
            result=place(receiver,token,rhs)
            assert result is original_value
            finished=True
            return original_receiver.items[0]
            """;
        var frontend = LythonFrontend.Compile(source);
        Assert.NotNull(frontend.Script);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script)).EntryPoint;
        var place = code.Functions.Single(binding => binding.CodeObject?.Name == "place").CodeObject!;
        var store = Assert.Single(place.Instructions, instruction => instruction.OpCode == ExecutableOpCode.StoreSubscript);
        Assert.Equal(place.LocalNameToSlot["value"], store.InputStart);
        Assert.Equal(place.LocalNameToSlot["receiver"], store.SecondInput);
        Assert.Equal(place.LocalNameToSlot["key"], store.ThirdInput);
        Assert.Equal(store.LiveExtent, store.ResultSlot);

        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new()
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var observation = new Observation();
            var hold = new Hold(observation, failureKind, cancellation);
            context.Variables["observe"] = observation;
            context.Variables["hold"] = hold;
            try
            {
                async Task<object?> Execute() => asynchronous
                    ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                var pending = Execute();
                try
                {
                    if (asynchronous)
                    {
                        await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                        Assert.False(pending.IsCompleted);
                        Assert.Empty(observation.Items!);
                        Assert.Same(PyNone.Instance, context.Variables["rhs"]);
                        var funded = budget.ReservedBytes;
                        Assert.True(funded > 0);
                        if (failureKind == 1) cancellation.Cancel();
                        Assert.False(pending.IsCompleted);
                        Assert.Equal(funded, budget.ReservedBytes);
                        hold.Release.TrySetResult();
                    }
                    if (failureKind == 0)
                    {
                        var result = Assert.IsType<PyTuple>(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                        Assert.Same(observation.Key, result[0]);
                        Assert.Same(observation.Value, result[1]);
                        Assert.Single(observation.Items!);
                        Assert.Equal(true, context.Variables["finished"]);
                    }
                    else
                    {
                        var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                        if (failureKind == 1) Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                        else
                        {
                            Assert.Same(hold.Refusal, error);
                            Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                            Assert.Equal(777L, error.Attribution?.Requested);
                        }
                        Assert.Empty(observation.Items!);
                        Assert.Equal(false, context.Variables["finished"]);
                    }
                }
                finally
                {
                    hold.Release.TrySetResult();
                    try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (LythonRuntimeException) when (failureKind != 0) { }
                }
                Assert.True(hold.Finished);
                Assert.Equal(PyString.FromString("original"), Assert.Single(observation.Value!));
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.True(budget.ReservedBytes > 0); // live aliases still use this owner
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Observation : LythonRuntime.ICallable
    {
        internal object? Receiver, Key;
        internal PyList? Value, Items;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Receiver = context.Variables["receiver"];
            Key = context.Variables["token"];
            Value = Assert.IsType<PyList>(context.Variables["rhs"]);
            Items = Assert.IsType<PyList>(context.Variables["items"]);
            return PyNone.Instance;
        }
    }

    private sealed class Hold(Observation observation, int failureKind, CancellationTokenSource cancellation) : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LythonRuntimeException? Refusal;
        internal bool Finished;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Equal(3, arguments.Length);
            Assert.Same(observation.Receiver, arguments[0].Value);
            Assert.Same(observation.Key, arguments[1].Value);
            Assert.Same(observation.Value, arguments[2].Value);
            if (failureKind == 1) cancellation.Cancel();
            if (failureKind == 2) Refusal = context.MemoryGovernor.RefuseAdmission("controlled subscript refusal", 777, span);
            Finished = true;
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            try
            {
                Entered.TrySetResult();
                await Release.Task.ConfigureAwait(false);
                return Invoke(arguments, span, context);
            }
            finally { Finished = true; }
        }
    }
}
