using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyBytes : IEquatable<PyBytes>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPyIndexableValue, IPyGovernedValue, IPySizedValue
{
    private readonly byte[] _bytes;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;

    public PyBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _bytes = bytes;
    }

    public PyBytes(byte[] bytes, MemoryGovernor governor) : this(bytes, governor, null) { }

    public PyBytes(byte[] bytes, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(governor);
        var approximateBytes = EstimateApproximateBytes(bytes.Length);
        governor.Reserve(approximateBytes, allocationSpan);
        governor.Commit(approximateBytes);
        _bytes = bytes;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public int Length => _bytes.Length;

    internal ReadOnlySpan<byte> Bytes => _bytes;

    internal ReadOnlyMemory<byte> Memory => _bytes;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    internal static long EstimateApproximateBytes(int length) => 32L + length;

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
        var slice = MaterializeSlice(indices);
        return _memoryGovernor is null
            ? new PyBytes(slice)
            : new PyBytes(slice, _memoryGovernor, _allocationSpan);
    }

    public byte[] ToArray()
    {
        if (_memoryGovernor is not null)
        {
            _memoryGovernor.EnsureCanReserve(EstimateApproximateBytes(_bytes.Length), _allocationSpan);
        }

        return _bytes.ToArray();
    }

    public bool Equals(PyBytes? other)
    {
        return other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
    }

    public override bool Equals(object? obj) => obj is PyBytes other && Equals(other);

    public override int GetHashCode() => GetPyHashCode();

    public int GetPyHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _bytes)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        var builder = new Utf8ValueBuilder();
        builder.AppendAscii("b'");
        foreach (var value in _bytes)
        {
            AppendEscapedByte(builder, value);
        }

        builder.AppendAscii("'");
        return builder.ToPyString();
    }

    private static void AppendEscapedByte(Utf8ValueBuilder builder, byte value)
    {
        switch (value)
        {
            case (byte)'\\':
                builder.AppendAscii("\\\\");
                return;
            case (byte)'\'':
                builder.AppendAscii("\\'");
                return;
            case (>= 32 and <= 126):
                builder.Append(value);
                return;
            default:
                builder.AppendString($"\\x{value:x2}");
                return;
        }
    }

    private byte[] MaterializeSlice(IEnumerable<int> indices)
    {
        if (indices is ICollection<int> collection)
        {
            if (_memoryGovernor is not null)
            {
                _memoryGovernor.EnsureCanReserve(EstimateApproximateBytes(collection.Count), _allocationSpan);
            }

            var slice = new byte[collection.Count];
            var index = 0;
            foreach (var item in indices)
            {
                slice[index++] = _bytes[item];
            }

            return slice;
        }

        var values = new List<byte>();
        foreach (var item in indices)
        {
            values.Add(_bytes[item]);
        }

        return [.. values];
    }
}
