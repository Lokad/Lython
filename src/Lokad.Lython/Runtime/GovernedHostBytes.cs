using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Owns unpublished whole-file input. Construction consumes already admitted
// storage; disposal clears the private reference before returning its allowance.
internal sealed class GovernedHostBytes : IDisposable
{
    private PyBytes? _payload;
    private GovernedHostBytes(PyBytes payload)
    {
        if (payload.OwnerMemoryGovernor is null) throw new InvalidOperationException("Host input requires admitted storage.");
        _payload = payload;
    }

    internal static GovernedHostBytes FromBuilder(GovernedByteBuilder builder)
        => new(builder.ToPyBytesAndRelease());

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
        if (payload is not null) payload.OwnerMemoryGovernor!.Release(payload.CommittedStorageBytes);
    }
}
