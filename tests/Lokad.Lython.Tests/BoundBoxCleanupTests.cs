using Slot = Lokad.Lython.Runtime.LythonRuntime.ExecutableSlot;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Bound workspace returns on every exit path and stays cleared for reuse.
// Arity failures inside BindFunctionArguments, recursion/budget failures in
// EnterInvocationFrame, and the frameless dataclass dunders all hand the box
// back; otherwise each failure strands one pooled array (losing the pool slot
// and retaining argument refs until GC). Pool reuse is observed through array
// identity, so the tests are exact instead of timing-sensitive.
public sealed class BoundBoxCleanupTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    private static FunctionBindingPlan TwoParameterPlan() => FunctionBindingPlan.CreateDynamic(
        "f",
        PythonCallableKind.Function,
        [
            new LoweredFunctionParameter("a", FunctionParameterKind.Positional, null, null),
            new LoweredFunctionParameter("b", FunctionParameterKind.Positional, null, null),
        ],
        new Dictionary<string, object>());

    [Fact]
    public void FailingBindsReturnRentedBox()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var plan = TwoParameterPlan();
        var first = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)], Span, plan, context);
        LythonRuntime.ReturnBoundArguments(first);

        // One argument for two required parameters: the rent already happened,
        // so a leaked box would strand the pooled array here.
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            LythonRuntime.BindFunctionArguments([CallArgumentValue.Positional(1)], Span, plan, context));
        Assert.Equal("TypeError", failure.ExceptionType);

        var second = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)], Span, plan, context);
        LythonRuntime.ReturnBoundArguments(second);
        Assert.Same(first.Values, second.Values);
        Assert.All(second.Values, value => Assert.Null(value.Object));
        context.State.CloseResources();
    }

    [Fact]
    public void PositionalOverflowKeepsOnlyTheFinalTupleCharged()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var plan = FunctionBindingPlan.CreateDynamic("f", PythonCallableKind.Function,
                [new("items", FunctionParameterKind.VariadicList, null, null),
                    new("key", FunctionParameterKind.KeywordOnly, null, null)], new Dictionary<string, object>());
            var alias = new object();
            var slots = new Slot[2];
            var before = budget.ReservedBytes;

            var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.BindFunctionArgumentsInto(slots,
                [CallArgumentValue.Positional(alias), CallArgumentValue.Positional(alias)], Span, plan, context));
            Assert.Equal("TypeError", error.ExceptionType);
            Assert.Equal(before, budget.ReservedBytes);

            // The receiver is the first spill and a keyword can sit between
            // expanded positionals. All tuple items retain the original alias.
            LythonRuntime.BindFunctionArgumentsInto(slots,
                [CallArgumentValue.Positional(alias), CallArgumentValue.Keyword("key", PyNone.Instance),
                    CallArgumentValue.Positional(alias)], Span, plan, context, receiver: alias);
            var tuple = Assert.IsType<PyTuple>(slots[plan.VariadicListIndex].Object);
            Assert.Equal(3, tuple.Count);
            for (var i = 0; i < tuple.Count; i++) Assert.Same(alias, tuple[i]);
            Assert.Equal(before + tuple.CommittedStorageBytes, budget.ReservedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedPositionalOverflowReturnsUnpublishedStorage(bool cancelOnAdmission)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var plan = FunctionBindingPlan.CreateDynamic("f", PythonCallableKind.Function,
                [new("items", FunctionParameterKind.VariadicList, null, null)], new Dictionary<string, object>());
            var slots = new Slot[1];
            var before = budget.ReservedBytes;
            if (cancelOnAdmission) hook.AfterGrowth = _ => cancellation.Cancel();
            else hook.RefuseGrowth = _ => true;

            var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.BindFunctionArgumentsInto(slots,
                [CallArgumentValue.Positional(PyNone.Instance)], Span, plan, context));
            if (cancelOnAdmission) Assert.Equal("execution canceled", error.Message);
            else
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
            }
            Assert.Same(Span, error.Span);
            Assert.Null(slots[0].Object);
            Assert.Equal(before, budget.ReservedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeywordBindingErrorRefundsPrivateStorageAndKeepsBorrowedKeys(bool duplicate)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var plan = FunctionBindingPlan.CreateDynamic("f", PythonCallableKind.Function,
                [new("required", FunctionParameterKind.KeywordOnly, null, null),
                    new("kw", FunctionParameterKind.VariadicDictionary, null, null)], new Dictionary<string, object>());
            var borrowed = PyString.FromString("original", context.MemoryGovernor, Span);
            var slots = new Slot[2];
            var before = budget.ReservedBytes;
            CallArgumentValue[] supplied =
            [
                CallArgumentValue.Keyword("original", PyNone.Instance, borrowed),
                CallArgumentValue.Keyword("extra", PyNone.Instance)
            ];
            if (duplicate) supplied = [.. supplied, CallArgumentValue.Keyword("extra", PyNone.Instance)];

            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.BindFunctionArgumentsInto(slots, supplied, Span, plan, context));
            Assert.Equal("TypeError", error.ExceptionType);
            Assert.Contains(duplicate ? "multiple values" : "missing argument", error.Message);
            Assert.Null(slots[plan.VariadicDictionaryIndex].Object);
            Assert.Equal(before, budget.ReservedBytes);
            Assert.Equal("original", borrowed.AsString());
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedKeywordOverflowReturnsPrivateStorage(bool cancelOnAdmission)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var plan = FunctionBindingPlan.CreateDynamic("f", PythonCallableKind.Function,
                [new("kw", FunctionParameterKind.VariadicDictionary, null, null)], new Dictionary<string, object>());
            var slots = new Slot[1];
            var before = budget.ReservedBytes;
            if (cancelOnAdmission) hook.AfterGrowth = _ => cancellation.Cancel();
            else hook.RefuseGrowth = _ => true;

            var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.BindFunctionArgumentsInto(slots,
                [CallArgumentValue.Keyword("extra", PyNone.Instance)], Span, plan, context));
            if (cancelOnAdmission) Assert.Equal("execution canceled", error.Message);
            else
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
            }
            Assert.Null(slots[0].Object);
            Assert.Equal(before, budget.ReservedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CancelledNativeKeywordHashDoesNotInsertKey()
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token });
        var dictionary = new PyDict(context.MemoryGovernor, Span);
        var key = PyString.FromString(new string('x', 8192), context.MemoryGovernor, Span);
        cancellation.Cancel();

        var error = Assert.Throws<LythonRuntimeException>(() =>
            dictionary.TryAddNativeString(key, PyNone.Instance, context, Span));
        Assert.Equal("execution canceled", error.Message);
        Assert.Same(Span, error.Span);
        Assert.Equal(0, dictionary.Count);
        context.State.CloseResources();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandedArgumentsAdmitOldAndNewBuffersDuringGrowth(bool retained)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var baseline = budget.ReservedBytes;
        using (var accumulator = new CallExpansion.CallArgumentAccumulator(4, context, PyNone.Instance, retained))
        {
            for (var i = 0; i < 4; i++) accumulator.Add(CallArgumentValue.Positional(PyNone.Instance), Span);
            var before = budget.ReservedBytes;
            // Capacity for four additional slots is available only once the old
            // buffer is gone. It cannot cover simultaneous backing arrays.
            Assert.True(owner.TryReserve(limit - before - 4 * 32, out var occupied));
            using (occupied)
            {
                var beforeGrowth = budget.ReservedBytes;

                var error = Assert.Throws<LythonRuntimeException>(() =>
                    accumulator.Add(CallArgumentValue.Positional(PyNone.Instance), Span));
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(beforeGrowth, budget.ReservedBytes);
            }
            Assert.Equal(before, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        context.State.CloseResources();
    }

    [Fact]
    public void FrameEntryFailuresReturnRentedBox()
    {
        var host = new MockLythonHost();
        // The frame hangs off the closure's guards, so the closure itself
        // carries the zero recursion cap that trips frame entry.
        var closure = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxRecursionDepth = 0 },regexProvider:Utf8RegexProvider.Instance);
        var parameters = new List<LoweredFunctionParameter>
        {
            new("a", FunctionParameterKind.Positional, null, null),
            new("b", FunctionParameterKind.Positional, null, null),
        };
        var definition = Assert.IsType<LoweredFunctionDefinitionStatement>(Assert.Single(
            LoweredScript.Lower(LythonFrontend.Compile("def f(a,b):pass").Script!).Statements));
        var fun = new PyExecutableFunction(
            "f",
            FunctionParameterLayout.Create(parameters),
            ExecutableScript.CompileClassFunction(definition),
            closure,
            [],
            new Dictionary<string, object>(),
            ScopeDirectiveFacts.Empty);
        var box = fun.BindCallOperands([Slot.FromObject(1), Slot.FromObject(2)], Span, closure, receiver: null, callSite: null);
        LythonRuntime.ReturnBoundArguments(box);

        // Zero recursion depth trips inside EnterInvocationFrame, after the
        // Invoke rent: the box must come back before the error propagates.
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            fun.Invoke([CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)], Span, closure));
        Assert.Equal("RecursionError", failure.ExceptionType);

        var again = fun.BindCallOperands([Slot.FromObject(1), Slot.FromObject(2)], Span, closure, receiver: null, callSite: null);
        LythonRuntime.ReturnBoundArguments(again);
        Assert.Same(box.Values, again.Values);
        Assert.All(again.Values, value => Assert.Null(value.Object));
        closure.State.CloseResources();
    }

    [Fact]
    public void DataclassInitsReturnRentedBox()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var fields = new List<DataclassFieldSpec>
        {
            new("x", PyNone.Instance, DataclassFieldKind.Normal, false, PyNone.Instance, false, PyNone.Instance, true, true, true, null, false, new PyDataclassFieldObject { Name="x", Metadata=PyNone.Instance }, true),
            new("y", PyNone.Instance, DataclassFieldKind.Normal, false, PyNone.Instance, false, PyNone.Instance, true, true, true, null, false, new PyDataclassFieldObject { Name="y", Metadata=PyNone.Instance }, true),
        };
        var type = new PyType("P", [], new Dictionary<string, object>());
        var init = new PyDataclass.DataclassInitMethod(type, fields, null, context);
        var plan = FunctionBindingPlan.CreateDynamic(
            "P.__init__",
            PythonCallableKind.Function,
            [
                new LoweredFunctionParameter("self", FunctionParameterKind.Positional, null, null),
                new LoweredFunctionParameter("x", FunctionParameterKind.Positional, null, null),
                new LoweredFunctionParameter("y", FunctionParameterKind.Positional, null, null),
            ],
            new Dictionary<string, object>());
        var box = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2), CallArgumentValue.Positional(3)],
            Span, plan, context);
        LythonRuntime.ReturnBoundArguments(box);

        var instance = new PyInstance(type, context.MemoryGovernor, null);
        init.Invoke(
            [CallArgumentValue.Positional(instance), CallArgumentValue.Positional(1), CallArgumentValue.Positional(2)],
            Span, context);

        var again = LythonRuntime.BindFunctionArguments(
            [CallArgumentValue.Positional(1), CallArgumentValue.Positional(2), CallArgumentValue.Positional(3)],
            Span, plan, context);
        LythonRuntime.ReturnBoundArguments(again);
        Assert.Same(box.Values, again.Values);
        Assert.All(again.Values, value => Assert.Null(value.Object));
        context.State.CloseResources();
        GC.KeepAlive(instance);
    }
}
