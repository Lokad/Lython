namespace Lokad.Lython.Frontend;

internal static class StaticTextContractFacts
{
    public static bool IsSupportedErrorName(string errors)
        => errors.Equals("strict", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("ignore", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("replace", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("backslashreplace", StringComparison.OrdinalIgnoreCase);
}
