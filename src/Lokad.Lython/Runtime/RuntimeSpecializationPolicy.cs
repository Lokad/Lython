namespace Lokad.Lython.Runtime;

internal static class RuntimeSpecializationPolicy
{
    // These are the explicitly owned specialization seams. New specializations should hang off one of
    // these owners instead of being introduced opportunistically in the generic interpreter loop.
    public static bool SupportsSpecializedListStorage => true;

    public static bool SupportsSpecializedDictionaryStorage => true;

    public static bool SupportsSpecializedNumericPaths => true;

    public static bool SupportsSpecializedProjection => true;
}
