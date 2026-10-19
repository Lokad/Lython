using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class SliceValueDispatchTests
{
    private const string Source = "return mapping[stamp(1):stamp(5):stamp(2), stamp(9)]";
    private static readonly ExecutableCodeObject Code = ExecutableScript.Compile(
        LoweredScript.Lower(LythonFrontend.Compile(Source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    private static readonly string[] ExpectedCheckpoints =
    [
        "-:",
        "7,7,1,8:",
        "15,5,1,16:",
        "21,1,1,22:",
        "15,8,1,16:",
        "15,8,1,16:",
        "15,8,1,16:",
        "24,5,1,25:1",
        "30,1,1,31:1",
        "24,8,1,25:1",
        "24,8,1,25:1",
        "24,8,1,25:1",
        "33,5,1,34:1,5",
        "39,1,1,40:1,5",
        "33,8,1,34:1,5",
        "33,8,1,34:1,5",
        "33,8,1,34:1,5",
        "15,26,1,16:1,5,2",
        "43,5,1,44:1,5,2",
        "49,1,1,50:1,5,2",
        "43,8,1,44:1,5,2",
        "43,8,1,44:1,5,2",
        "43,8,1,44:1,5,2",
        "15,36,1,16:1,5,2,9",
        "7,45,1,8:1,5,2,9",
        "0,52,1,1:1,5,2,9",
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TupleSliceKeyPreservesEffectsAndCleanup(bool asynchronous)
    {
        Assert.Single(Code.Blocks.SelectMany(block => block.Instructions), i => i.OpCode == ExecutableOpCode.MakeSlice);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, tape, expected) = Prepare(budget, owner, false);
            using (tape)
            {
                Assert.Same(expected, await Execute(context, asynchronous));
                Assert.Equal([1, 5, 2, 9], tape.Events);
                Unwound(context);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedKeyConstructionPrecedesTailAndLookup(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, tape, expected) = Prepare(budget, owner, true);
            using (tape)
            {
                var committedBeforeExecution = context.MemoryGovernor.CurrentCommittedBytes;
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => { await Execute(context, asynchronous); });
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(new LythonSourceSpan(Source.IndexOf("stamp(1)", StringComparison.Ordinal),
                    "stamp(1):stamp(5):stamp(2)".Length, 1, 16), error.Span);
                Assert.Equal([1, 5, 2], tape.Events);
                Assert.Equal(committedBeforeExecution, context.MemoryGovernor.CurrentCommittedBytes);
                var mapping = Assert.IsType<PyDict>(context.Variables["mapping"]);
                Assert.Equal(1, mapping.Count);
                Assert.Same(expected, Assert.Single(mapping.Values));
            }
            Unwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static (Context, Stamp, object) Prepare(LythonMemoryBudget budget, ILythonMemoryBudget owner, bool deny)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var tape = new Stamp(budget, deny);
        var expected = PyString.FromString("found", context.MemoryGovernor);
        var mapping = new PyDict(context.MemoryGovernor, null);
        mapping.SetItem(new PyTuple([new PySlice(new BigInteger(1), new BigInteger(5), new BigInteger(2)), new BigInteger(9)]), expected);
        context.Variables["mapping"] = mapping;
        context.Variables["stamp"] = tape;
        return (context, tape, expected);
    }

    private static async ValueTask<object?> Execute(Context context, bool asynchronous)
        => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(Code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(Code, context, default, null);

    private static void Unwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Stamp(LythonMemoryBudget budget, bool deny) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _pin;
        internal List<int> Events { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var value = Assert.IsType<BigInteger>(Assert.Single(arguments).Value);
            Events.Add((int)value);
            if (deny && value == 2)
            {
                var governor = context.MemoryGovernor;
                _pin = governor.ReserveTemporary(budget.LimitBytes!.Value - governor.CurrentAccountedBytes - 63, span);
            }
            return value;
        }
        public void Dispose() => _pin?.Dispose();
    }
}
