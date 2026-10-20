using System.Diagnostics.CodeAnalysis;

namespace Lokad.Lython;

/// <summary>A borrowed shared allowance for estimated owned Lython storage.</summary>
/// <remarks>
/// When Lython receives a refused admission, that command fails after cleanup;
/// guest exception handling cannot resume it. The borrowed allowance remains
/// usable by later commands. See docs/RUNTIME.md for ownership and headroom requirements.
/// </remarks>
public interface ILythonMemoryBudget
{
    /// <summary>Creates an empty owner sharing the same root allowance and no independent cap.</summary>
    ILythonMemoryScope CreateScope();

    /// <summary>Immediately reserves nonnegative bytes; refusal leaves accounting unchanged and returns null.</summary>
    bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation);
}

/// <summary>An owned memory lifetime whose disposal refunds and invalidates its subtree.</summary>
/// <remarks>
/// The creator owns disposal. Await all storage users and cleanup first; disposal
/// does not cancel work or dispose runtime objects. Serialize disposal with subtree
/// operations. The supplied parent budget is borrowed and must outlive its users.
/// </remarks>
public interface ILythonMemoryScope : ILythonMemoryBudget, IDisposable { }

/// <summary>An owned conservative storage estimate against a shared allowance.</summary>
public interface ILythonMemoryReservation : IDisposable
{
    /// <summary>Gets its estimate, or zero after this reservation or an ancestor is disposed.</summary>
    long Bytes { get; }

    /// <summary>Immediately replaces the nonnegative estimate; refusal leaves the reservation unchanged.</summary>
    bool TryResize(long bytes);
}
