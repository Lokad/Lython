using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Owns unpublished I/O bytes. Construction consumes already admitted storage;
// the canonical empty value needs none. Disposal clears the private reference
// before returning its allowance; TakeValue transfers it to the surviving user.
internal sealed class GovernedHostBytes : IDisposable
{
    private PyBytes? _payload;
    private GovernedHostBytes(PyBytes payload)
    {
        if (payload.OwnerMemoryGovernor is null && !payload.Memory.IsEmpty)
            throw new InvalidOperationException("Host input requires admitted storage.");
        _payload = payload;
    }

    internal static GovernedHostBytes FromBuilder(GovernedByteBuilder builder)
        => new(builder.ToPyBytesAndRelease());

    internal static GovernedHostBytes FromValue(PyBytes payload) => new(payload);

    private PyBytes Payload => _payload ?? throw new ObjectDisposedException(nameof(GovernedHostBytes));
    public ReadOnlyMemory<byte> Memory => Payload.Memory;
    public ReadOnlySpan<byte> Span => Payload.Bytes;

    internal PyBytes TakeValue()
    {
        var payload = Payload;
        _payload = null;
        return payload;
    }

    public void Dispose()
    {
        var payload = _payload;
        _payload = null;
        if (payload?.OwnerMemoryGovernor is { } owner) owner.Release(payload.CommittedStorageBytes);
    }
}
