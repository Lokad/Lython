using System.Text;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Runtime.Text;

internal sealed class Utf8ValueBuilder
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private byte[] _buffer;
    private int _length;
    private long _committedCapacity;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private readonly int? _maxLengthBytes;
    private readonly string? _maxLengthOwner;

    public Utf8ValueBuilder(int capacity = 0)
    {
        _buffer = capacity > 0 ? new byte[capacity] : [];
    }

    public Utf8ValueBuilder(
        MemoryGovernor governor,
        LythonSourceSpan? allocationSpan = null,
        int capacity = 0,
        int? maxLengthBytes = null,
        string? maxLengthOwner = null)
    {
        ArgumentNullException.ThrowIfNull(governor);
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _maxLengthBytes = maxLengthBytes;
        _maxLengthOwner = maxLengthOwner;
        if (capacity <= 0)
        {
            _buffer = [];
        }
        else
        {
            EnsureMaxLength(capacity);
            _memoryGovernor.Reserve(capacity, allocationSpan);
            _buffer = new byte[capacity];
            _memoryGovernor.Commit(capacity);
            _committedCapacity = capacity;
        }
    }

    public int Length => _length;

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
        ArgumentNullException.ThrowIfNull(text);
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
        ArgumentNullException.ThrowIfNull(text);
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
        if (_length == 0)
        {
            return PyString.Empty;
        }

        if (_memoryGovernor is not null)
        {
            _memoryGovernor.EnsureCanReserve(PyString.EstimateApproximateBytes(_length), _allocationSpan);
        }

        var exact = new byte[_length];
        _buffer.AsSpan(0, _length).CopyTo(exact);
        return _memoryGovernor is null
            ? PyString.FromOwnedUtf8(exact)
            : PyString.FromOwnedUtf8(exact, _memoryGovernor, _allocationSpan);
    }

    public byte[] ToArray()
    {
        if (_length == 0)
        {
            return [];
        }

        if (_memoryGovernor is not null)
        {
            _memoryGovernor.Reserve(_length, _allocationSpan);
        }

        var exact = new byte[_length];
        _buffer.AsSpan(0, _length).CopyTo(exact);
        if (_memoryGovernor is not null)
        {
            _memoryGovernor.Commit(exact.Length);
        }

        return exact;
    }

    public byte[] ToArrayAndRelease()
    {
        if (_length == 0)
        {
            Release();
            return [];
        }

        _memoryGovernor?.EnsureCanReserve(_length, _allocationSpan);
        var exact = new byte[_length];
        _buffer.AsSpan(0, _length).CopyTo(exact);
        Release();
        return exact;
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
            throw RuntimeErrors.Runtime("UTF-8 buffer allocation is too large.", _allocationSpan);
        }

        var requiredCapacity = _length + additionalBytes;
        EnsureMaxLength(requiredCapacity);

        if (requiredCapacity <= _buffer.Length)
        {
            return;
        }

        var doubledCapacity = _buffer.Length == 0
            ? 8L
            : (long)_buffer.Length * 2L;
        var newCapacityLong = Math.Max(doubledCapacity, requiredCapacity);
        if (_maxLengthBytes is { } maximumLength)
        {
            newCapacityLong = Math.Min(newCapacityLong, maximumLength);
        }

        if (newCapacityLong > int.MaxValue)
        {
            throw RuntimeErrors.Runtime("UTF-8 buffer allocation is too large.", _allocationSpan);
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

    private void EnsureMaxLength(int requiredCapacity)
    {
        if (_maxLengthBytes is { } maxLengthBytes && requiredCapacity > maxLengthBytes)
        {
            var owner = _maxLengthOwner ?? "UTF-8 buffer";
            throw RuntimeErrors.Runtime($"{owner} exceeded maximum captured output bytes ({maxLengthBytes})", _allocationSpan);
        }
    }
}
