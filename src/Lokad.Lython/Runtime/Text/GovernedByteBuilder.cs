using System.Text;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Runtime.Text;

/// <summary>
/// Accumulates arbitrary bytes while charging its retained capacity to an optional execution
/// governor. Callers that finish with a value should use a consuming conversion or
/// <see cref="Release"/> so the mutable capacity is not retained in the execution budget.
/// </summary>
internal sealed class GovernedByteBuilder
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private byte[] _buffer;
    private int _length;
    private long _committedCapacity;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;

    public GovernedByteBuilder() : this(0) { }

    public GovernedByteBuilder(int capacity)
    {
        _buffer = capacity > 0 ? new byte[capacity] : [];
    }

    public GovernedByteBuilder(MemoryGovernor governor) : this(governor, null, 0) { }

    public GovernedByteBuilder(MemoryGovernor governor, LythonSourceSpan? allocationSpan) : this(governor, allocationSpan, 0) { }

    public GovernedByteBuilder(MemoryGovernor governor, LythonSourceSpan? allocationSpan, int capacity)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        if (capacity <= 0)
        {
            _buffer = [];
        }
        else
        {

            _memoryGovernor.Reserve(capacity, allocationSpan);
            _buffer = new byte[capacity];
            _memoryGovernor.Commit(capacity);
            _committedCapacity = capacity;
        }
    }

    public int Length => _length;

    internal long CommittedCapacity => _committedCapacity;

    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _length);

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _length);

    public void Append(byte value)
    {
        EnsureAdditionalCapacity(1);
        _buffer[_length++] = value;
    }

    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        EnsureAdditionalCapacity(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_length));
        _length += bytes.Length;
    }

    public void Append(PyString value) => Append(value.Utf8Bytes.Span);

    public void Append(Rune value)
    {
        EnsureAdditionalCapacity(value.Utf8SequenceLength);
        _length += value.EncodeToUtf8(_buffer.AsSpan(_length));
    }

    public void AppendRepeated(byte value, int count)
    {
        if (count <= 0)
        {
            return;
        }

        EnsureAdditionalCapacity(count);
        _buffer.AsSpan(_length, count).Fill(value);
        _length += count;
    }

    public void AppendAscii(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        EnsureAdditionalCapacity(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            _buffer[_length + i] = (byte)text[i];
        }

        _length += text.Length;
    }

    public void AppendString(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        var byteCount = Utf8.GetByteCount(text);
        EnsureAdditionalCapacity(byteCount);
        _length += Utf8.GetBytes(text.AsSpan(), _buffer.AsSpan(_length));
    }

    public PyString ToPyString()
    {
        return _memoryGovernor is null
            ? PyString.FromOwnedUtf8(WrittenSpan.ToArray())
            : PyString.CopyFromUtf8(WrittenSpan, _memoryGovernor, _allocationSpan);
    }

    public PyString ToPyStringAndRelease()
    {
        var result = ToPyString();
        Release();
        return result;
    }

    public byte[] ToArrayAndRelease()
    {
        if (_length == 0)
        {
            Release();
            return [];
        }

        var bytes = PyBytes.EstimateApproximateBytes(_length);
        using var storage = _memoryGovernor?.ReserveTemporary(bytes, _allocationSpan);
        var exact = new byte[_length];
        _buffer.AsSpan(0, _length).CopyTo(exact);
        storage?.Commit(bytes);
        Release();
        return exact;
    }

    // Consume the builder into a funded value, transferring the final-copy
    // reservation instead of charging the same backing again in a factory.
    public PyBytes ToPyBytesAndRelease()
    {
        try
        {
            if (_memoryGovernor is null) return new PyBytes(WrittenSpan.ToArray());
            using var storage = _memoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(_length), _allocationSpan);
            var exact = WrittenSpan.ToArray();
            return new PyBytes(exact, _memoryGovernor, _allocationSpan, storage);
        }
        finally { Release(); }
    }

    public void Release()
    {
        _memoryGovernor?.Release(_committedCapacity);
        _buffer = [];
        _length = 0;
        _committedCapacity = 0;
    }

    private void EnsureAdditionalCapacity(int additionalBytes)
    {
        if (additionalBytes <= 0)
        {
            return;
        }

        if (_length > int.MaxValue - additionalBytes)
        {
            throw RuntimeErrors.Runtime("byte buffer allocation is too large.", _allocationSpan);
        }

        var requiredCapacity = _length + additionalBytes;


        if (requiredCapacity <= _buffer.Length)
        {
            return;
        }

        var doubledCapacity = _buffer.Length == 0
            ? 8L
            : (long)_buffer.Length * 2L;
        var newCapacityLong = Math.Max(doubledCapacity, requiredCapacity);

        if (newCapacityLong > int.MaxValue)
        {
            throw RuntimeErrors.Runtime("byte buffer allocation is too large.", _allocationSpan);
        }

        var newCapacity = (int)newCapacityLong;

        if (_memoryGovernor is not null)
        {
            _memoryGovernor.Reserve(newCapacity, _allocationSpan);
        }

        var expanded = new byte[newCapacity];
        _buffer.AsSpan(0, _length).CopyTo(expanded);

        if (_memoryGovernor is not null)
        {
            _memoryGovernor.Release(_committedCapacity);
            _memoryGovernor.Commit(newCapacity);
            _committedCapacity = newCapacity;
        }

        _buffer = expanded;
    }

}
