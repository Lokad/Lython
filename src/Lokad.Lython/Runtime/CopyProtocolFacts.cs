namespace Lokad.Lython.Runtime;

internal static class CopyProtocolFacts
{
    public static IReadOnlyList<string> UnsupportedReductionHooks { get; } =
    [
        "__reduce_ex__",
        "__reduce__",
        "__getstate__",
        "__setstate__"
    ];
}
