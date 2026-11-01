using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class SetDefaultAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(5, 3, 2, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullAllowanceUsesFundedSlotsAndRefusesBeforeGrowth(bool hit)
    {
        var root = new LythonMemoryBudget(65536);
        using var owner = root.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var dictionary = new PyDict(context.MemoryGovernor, Span);
        dictionary.SetItem(BigInteger.One, PyNone.Instance);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        using (var pressure = context.MemoryGovernor.ReserveTemporary(65536 - root.ReservedBytes, Span))
        {
            if (hit)
                Assert.Same(PyNone.Instance, LythonRuntime.DictMembers.SetDefault(dictionary, BigInteger.One, true, Span, context));
            else
            {
                Assert.Equal(true, LythonRuntime.DictMembers.SetDefault(dictionary, new BigInteger(2), true, Span, context));
                // Fill only prepaid slots. The first growth must fail without
                // publishing its key, and cannot be retried in this execution.
                LythonRuntimeException? failure = null;
                for (var key = 3; key < 32; key++)
                {
                    var count = dictionary.Count;
                    try { LythonRuntime.DictMembers.SetDefault(dictionary, new BigInteger(key), true, Span, context); }
                    catch (LythonRuntimeException error)
                    {
                        failure = error;
                        Assert.Equal(count, dictionary.Count);
                        Assert.False(dictionary.ContainsKey(new BigInteger(key)));
                        break;
                    }
                }
                Assert.NotNull(failure);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(Span, failure.Span);
            }
            if (hit) Assert.Equal(1, dictionary.Count);
            Assert.Same(PyNone.Instance, dictionary.GetItem(BigInteger.One));
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(65536, root.ReservedBytes);
        }
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }
}
