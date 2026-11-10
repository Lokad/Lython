using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Options = Lokad.Lython.Runtime.LythonRuntime.JsonLoadOptions;

namespace Lokad.Lython.Tests;

public sealed class JsonDecodeAdmissionTests
{
    private static readonly Type ModuleType = typeof(LythonRuntime).GetNestedType("JsonModule", BindingFlags.NonPublic)!;
    private static readonly object Module = ModuleType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    private static readonly Func<PyString, Options, Context, LythonSourceSpan, object> Decode =
        ModuleType.GetMethod("ParseJsonText", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<Func<PyString, Options, Context, LythonSourceSpan, object>>(Module);
    private static readonly Func<PyString, Options, Context, LythonSourceSpan, ValueTask<object>> DecodeAsync =
        ModuleType.GetMethod("ParseJsonTextAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<Func<PyString, Options, Context, LythonSourceSpan, ValueTask<object>>>(Module);
    private static readonly Func<PyString, long, Options, Context, LythonSourceSpan, (object, int)> Raw =
        ModuleType.GetMethod("DecodeJsonRawValue", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<Func<PyString, long, Options, Context, LythonSourceSpan, (object, int)>>(Module);
    private static readonly Func<PyString, long, Options, Context, LythonSourceSpan, ValueTask<(object, int)>> RawAsync =
        ModuleType.GetMethod("DecodeJsonRawValueAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<Func<PyString, long, Options, Context, LythonSourceSpan, ValueTask<(object, int)>>>(Module);

    public static IEnumerable<object[]> Cases() => new[] { "true", "[]", "NaN", "[[[[[[[[[true]]]]]]]]]" }
        .SelectMany(source => new[] { false, true }.SelectMany(raw => new[] { false, true }
            .Select(asynchronous => new object[] { source, raw, asynchronous })));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task DecoderDeniesBeforeConstructionAndRecovers(string source, bool raw, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var input = PyString.FromString(source, context.MemoryGovernor, null);
            var span = new LythonSourceSpan(0, source.Length, 1, 1);
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            context.State.CurrentInterpreterDepth = ExecutionState.MaxInterpreterDepth;
            try
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Read());
                Assert.Equal(LythonFailureReason.InterpreterStackLimit, error.Attribution?.Reason);
                Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CurrentInterpreterDepth = 0; }
            Assert.NotNull(await Read());
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);

            async ValueTask<object> Read()
            {
                if (raw) return (asynchronous ? await RawAsync(input, 0, Options.Default, context, span)
                    : Raw(input, 0, Options.Default, context, span)).Item1;
                return asynchronous ? await DecodeAsync(input, Options.Default, context, span)
                    : Decode(input, Options.Default, context, span);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
