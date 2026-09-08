namespace Lokad.Lython.Frontend;

internal static class StaticTextContractFacts
{
    public static bool IsSupportedErrorName(string errors)
        => errors.Equals("strict", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("ignore", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("replace", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("backslashreplace", StringComparison.OrdinalIgnoreCase);

    public static bool IsUtf8EncodingName(string encoding)
        => encoding.Equals("utf-8", StringComparison.OrdinalIgnoreCase) ||
           encoding.Equals("utf8", StringComparison.OrdinalIgnoreCase);

    public static bool IsUtf8SigEncodingName(string encoding)
        => encoding.Equals("utf-8-sig", StringComparison.OrdinalIgnoreCase);

    public static bool IsLatin1EncodingName(string encoding)
        => encoding.Equals("latin-1", StringComparison.OrdinalIgnoreCase) ||
           encoding.Equals("latin1", StringComparison.OrdinalIgnoreCase) ||
           encoding.Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedEncodingName(string encoding)
        => IsUtf8EncodingName(encoding) ||
           IsUtf8SigEncodingName(encoding) ||
           IsLatin1EncodingName(encoding);

    public static bool IsSupportedNewlineName(string newline)
        => newline is "" or "\n" or "\r" or "\r\n";
}
