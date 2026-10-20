using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyBytes : IEquatable<PyBytes>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPyIndexableValue, IPyGovernedValue, IPySizedValue
{
    private readonly byte[] _bytes;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private int _hashCode;
    private bool _hashCodeComputed;

    public PyBytes(byte[] bytes)
    {
        _bytes = bytes;
    }

    public PyBytes(byte[] bytes, MemoryGovernor governor) : this(bytes, governor, null) { }

    public PyBytes(byte[] bytes, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        var approximateBytes = EstimateApproximateBytes(bytes.Length);
        governor.Reserve(approximateBytes, allocationSpan);
        governor.Commit(approximateBytes);
        _bytes = bytes;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    // The caller admitted this private backing before allocating it. Transfer
    // its reservation without charging twice or refunding a still-live array.
    internal PyBytes(byte[] bytes, MemoryGovernor governor, LythonSourceSpan? allocationSpan,
        MemoryGovernor.TemporaryMemoryReservation storage)
    {
        _bytes = bytes;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        storage.Commit(EstimateApproximateBytes(bytes.Length));
    }

    public int Length => _bytes.Length;

    internal ReadOnlySpan<byte> Bytes => _bytes;

    internal ReadOnlyMemory<byte> Memory => _bytes;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    internal static long EstimateApproximateBytes(int length) => 32L + length;

    // Payload funding available for rollback while this value is unpublished.
    // Published bytes stay charged to their batch; shared unowned constants carry none.
    internal long CommittedStorageBytes => OwnerMemoryGovernor is null ? 0 : EstimateApproximateBytes(_bytes.Length);

    public bool IsTruthy() => _bytes.Length != 0;

    public IEnumerable<object> Iterate()
    {
        foreach (var value in _bytes)
        {
            yield return new BigInteger(value);
        }
    }

    public object GetIndex(int index) => new BigInteger(_bytes[index]);

    public object GetSlice(IEnumerable<int> indices)
    {
        var builder = _memoryGovernor is null
            ? new GovernedByteBuilder()
            : new GovernedByteBuilder(_memoryGovernor, _allocationSpan);
        try
        {
            foreach (var index in indices) builder.Append(_bytes[index]);
            return builder.ToPyBytesAndRelease();
        }
        finally { builder.Release(); }
    }

    internal PyBytes Slice(PyIndexing.SliceBounds bounds, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        var count = bounds.Count;
        using var storage = context.MemoryGovernor.ReserveTemporary(EstimateApproximateBytes(count), span);
        context.CheckExecution(span);
        var result = new byte[count];
        if (bounds.Step == 1) _bytes.AsSpan(bounds.Start, count).CopyTo(result);
        else
        {
            long sourceIndex = bounds.Start;
            for (var i = 0; i < count; i++, sourceIndex += bounds.Step)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                result[i] = _bytes[(int)sourceIndex];
            }
        }
        context.CheckExecution(span);
        return new PyBytes(result, context.MemoryGovernor, span, storage);
    }

    public byte[] ToArray()
    {
        var bytes = EstimateApproximateBytes(_bytes.Length);
        using var storage = _memoryGovernor?.ReserveTemporary(bytes, _allocationSpan);
        var result = _bytes.ToArray();
        storage?.Commit(bytes);
        return result;
    }

    public bool Equals(PyBytes? other)
    {
        return other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
    }

    public override bool Equals(object? obj) => obj is PyBytes other && Equals(other);

    public override int GetHashCode() => GetPyHashCode();

    public int GetPyHashCode()
    {
        if (Volatile.Read(ref _hashCodeComputed))
        {
            return _hashCode;
        }

        var hash = new HashCode();
        hash.AddBytes(_bytes);

        var computed = hash.ToHashCode();
        _hashCode = computed;
        Volatile.Write(ref _hashCodeComputed, true);
        return computed;
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public PyString RenderPython(PyRenderingContext context)
    {
        var execution = context.Context;
        var span = PyStructuralGuard.AmbientSpan;
        execution.CheckExecution(span);
        var builder = new GovernedByteBuilder(execution.MemoryGovernor, span);
        try
        {
            var quote = _bytes.AsSpan().Contains((byte)'\'') && !_bytes.AsSpan().Contains((byte)'"') ? (byte)'"' : (byte)'\'';
            builder.AppendAscii("b");
            builder.Append(quote);
            for (var i = 0; i < _bytes.Length; i++)
            {
                if ((i & 1023) == 0) execution.CheckExecution(span);
                AppendEscapedByte(builder, _bytes[i], quote);
            }

            builder.Append(quote);
            execution.CheckExecution(span);
            return builder.ToPyString();
        }
        finally { builder.Release(); }
    }

    private static void AppendEscapedByte(GovernedByteBuilder builder, byte value, byte quote)
    {
        switch (value)
        {
            case (byte)'\n':
                builder.AppendAscii("\\n");
                return;
            case (byte)'\r':
                builder.AppendAscii("\\r");
                return;
            case (byte)'\t':
                builder.AppendAscii("\\t");
                return;
            case (byte)'\\':
                builder.AppendAscii("\\\\");
                return;
            case var quoted when quoted == quote:
                builder.Append((byte)'\\');
                builder.Append(value);
                return;
            case (>= 32 and <= 126):
                builder.Append(value);
                return;
            default:
                builder.AppendString($"\\x{value:x2}");
                return;
        }
    }

}
