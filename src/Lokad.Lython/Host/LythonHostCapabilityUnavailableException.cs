namespace Lokad.Lython;

/// <summary>
/// Reports that an optional capability was not supplied by an <see cref="ILythonHost"/>.
/// </summary>
public sealed class LythonHostCapabilityUnavailableException : NotSupportedException
{
    /// <summary>Creates an exception for the named unavailable host capability.</summary>
    /// <param name="capability">A user-facing capability name, such as <c>binary file I/O</c>.</param>
    public LythonHostCapabilityUnavailableException(string capability)
        : base($"Host {capability} is not available.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        Capability = capability;
    }

    /// <summary>Gets the user-facing name of the unavailable capability.</summary>
    public string Capability { get; }
}
