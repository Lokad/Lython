global using Lokad.Lython.Internal;
global using System.Diagnostics.CodeAnalysis;

namespace Lokad.Lython.Internal;

internal static class NullabilityExtensions
{
    public static T RequireNotNull<T>(this T? value)
        where T : class
        => value ?? throw new InvalidOperationException("An internal Lython invariant produced an absent value.");

    public static T RequireNotNull<T>(this T? value)
        where T : struct
        => value ?? throw new InvalidOperationException("An internal Lython invariant produced an absent value.");
}
