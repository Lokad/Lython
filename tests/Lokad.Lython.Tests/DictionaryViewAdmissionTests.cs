using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DictionaryViewAdmissionTests
{
    [Theory]
    [InlineData("items", false, false)] [InlineData("items", false, true)]
    [InlineData("items", true, false)] [InlineData("items", true, true)]
    [InlineData("keys", false, false)] [InlineData("keys", false, true)]
    [InlineData("keys", true, false)] [InlineData("keys", true, true)]
    [InlineData("values", false, false)] [InlineData("values", false, true)]
    [InlineData("values", true, false)] [InlineData("values", true, true)]
    public async Task ViewRefusesBeforePublicationAndPreservesDictionary(string method, bool asynchronous, bool shared)
    {
        const long limit = 65536;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var dict = new PyDict(context.MemoryGovernor); dict.SetItem(BigInteger.One, BigInteger.One);
        var previous = new object(); context.Variables["published"] = previous;
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        using (var fill = new Pad(dict, budget, limit, shared))
        {
            context.Variables["fill"] = fill;
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile($"global published\npublished=fill().{method}()\nreturn published",regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            });
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Equal(64, context.MemoryGovernor.LastDeniedReservationBytes);
            Assert.Equal(2, failure.Span?.Line);
            Assert.Same(previous, context.Variables["published"]);
            Assert.Single(dict); Assert.Equal(BigInteger.One, dict.GetItem(BigInteger.One));
            Assert.Equal(1, fill.Calls);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Pad(PyDict dict, LythonMemoryBudget budget, long limit, bool shared) : LythonRuntime.ICallable, IDisposable
    {
        private MemoryGovernor.TemporaryMemoryReservation? _temporary;
        private ILythonMemoryReservation? _shared;
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            if (shared) Assert.True(budget.TryReserve(limit - budget.ReservedBytes - 63, out _shared));
            else _temporary = context.MemoryGovernor.ReserveTemporary(limit - context.MemoryGovernor.CurrentAccountedBytes - 63, span);
            return dict;
        }
        public void Dispose() { _temporary?.Dispose(); _shared?.Dispose(); }
    }
}
