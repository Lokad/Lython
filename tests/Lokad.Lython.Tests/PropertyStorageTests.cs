using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PropertyStorageTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void EachRetainedNumericFieldCarriesItsOwnAllowance()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        object shared = new BigInteger(7);
        var property = PyProperty.Create(shared, shared, shared, shared, context, Span);
        Assert.Equal(128 + 4 * 64, property.CommittedBytes);
        Assert.True(property.TryGetMember("fget", out var getter));
        Assert.Same(shared, getter);
        Assert.True(property.TryGetMember("__doc__", out var doc));
        Assert.Same(shared, doc);
        property.TrySetMember("__doc__", PyNone.Instance);
        Assert.Equal(128 + 3 * 64, property.CommittedBytes);
    }

    [Fact]
    public void NumericDocReplacementTransfersItsFundedStorageAtAFullBudget()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        var property = PyProperty.Create(PyNone.Instance, PyNone.Instance, PyNone.Instance, new BigInteger(1), context, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        using (context.MemoryGovernor.ReserveTemporary(65536 - context.MemoryGovernor.CurrentAccountedBytes, Span))
        {
            object incoming = new BigInteger(2);
            Assert.True(property.TrySetMember("__doc__", incoming));
            Assert.True(property.TryGetMember("__doc__", out var doc));
            Assert.Same(incoming, doc);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void EmptyDescriptorKeepsItsShellOnly()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var property = PyProperty.Create(PyNone.Instance, PyNone.Instance, PyNone.Instance, PyNone.Instance, context, Span);
        Assert.Equal(128, property.CommittedBytes);
    }

    [Fact]
    public void PositiveFieldGrowthRefusalPreservesMetadata()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        var property = PyProperty.Create(PyNone.Instance, PyNone.Instance, PyNone.Instance, PyNone.Instance, context, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        using (context.MemoryGovernor.ReserveTemporary(65536 - context.MemoryGovernor.CurrentAccountedBytes, Span))
        {
            var error = Assert.Throws<LythonRuntimeException>(() => property.TrySetMember("__doc__", new BigInteger(1)));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.True(property.TryGetMember("__doc__", out var doc));
            Assert.Same(PyNone.Instance, doc);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    public static IEnumerable<object[]> NumericFields()
    {
        foreach (var value in new object[] { true, (sbyte)-1, (byte)1, (short)-1,
            (ushort)1, -1, (uint)1, (long)-1, (ulong)1, new BigInteger(1), 1.5,
            new PyDecimal(1.5m) })
            yield return [value];
    }

    [Theory]
    [MemberData(nameof(NumericFields))]
    public void NumericIngressFieldsHaveUniformAllowanceAndPreserveIdentity(object value)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var property = PyProperty.Create(value, value, value, value, context, Span);
        Assert.Equal(384, property.CommittedBytes);
        foreach (var name in new[] { "fget", "fset", "fdel", "__doc__" })
        {
            Assert.True(property.TryGetMember(name, out var actual));
            Assert.Same(value, actual);
        }
        Assert.True(property.TryDeleteMember("__doc__", Span));
        Assert.Equal(320, property.CommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargeIntegerFieldKeepsItsPayloadFundingAndReleasesOnlyFieldStorage(bool owned)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        object large = BigInteger.One << 4096;
        if (owned) LythonRuntime.OwnFreshInteger(large, context.MemoryGovernor,
            context.Services.State.CallTemporaries, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var fieldBytes = 64 + (owned ? 0 : ((BigInteger)large).GetByteCount());
        var property = PyProperty.Create(PyNone.Instance, PyNone.Instance, PyNone.Instance, large, context, Span);
        Assert.Equal(128 + fieldBytes, property.CommittedBytes);
        Assert.True(property.TryGetMember("__doc__", out var escaped));
        Assert.Same(large, escaped);
        Assert.True(property.TryDeleteMember("__doc__", Span));
        context.Services.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(128, property.CommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= before + 128);
        GC.KeepAlive(escaped);
        GC.KeepAlive(property);
    }

    [Fact]
    public void CompositeConstructorDenialCommitsNoPartialFieldStorage()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        using (context.MemoryGovernor.ReserveTemporary(65536 - context.MemoryGovernor.CurrentAccountedBytes - 383, Span))
        {
            object scalar = new BigInteger(1);
            var error = Assert.Throws<LythonRuntimeException>(() =>
                PyProperty.Create(scalar, scalar, scalar, scalar, context, Span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(384, context.MemoryGovernor.LastDeniedReservationBytes);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
