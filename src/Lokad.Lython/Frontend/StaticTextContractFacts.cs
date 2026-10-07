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
           IsLatin1EncodingName(encoding) ||
           IsAsciiEncodingName(encoding) ||
           IsWindows1252EncodingName(encoding);

    public static bool IsWindows1252EncodingName(string encoding)
        => encoding.Equals("cp1252", StringComparison.OrdinalIgnoreCase) ||
           encoding.Equals("1252", StringComparison.OrdinalIgnoreCase) ||
           encoding.Equals("windows-1252", StringComparison.OrdinalIgnoreCase) ||
           encoding.Equals("windows_1252", StringComparison.OrdinalIgnoreCase) ||
           encoding.Equals("windows 1252", StringComparison.OrdinalIgnoreCase);

    public static bool IsAsciiEncodingName(string encoding)
    {
        // Python's finite ASCII alias inventory; normalize common separators
        // in bounded stack scratch rather than allocating a normalized name.
        if (encoding.Length > 64) return false;
        Span<char> buffer = stackalloc char[32];
        var length = 0;
        var separator = false;
        foreach (var ch in encoding)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch == '.')
            {
                if (separator && length > 0)
                {
                    if (length == buffer.Length) return false;
                    buffer[length++] = '_';
                }
                if (length == buffer.Length) return false;
                buffer[length++] = char.ToLowerInvariant(ch);
                separator = false;
            }
            else if (ch is '_' or '-' or ' ') separator = true;
            else return false;
        }
        ReadOnlySpan<char> name = buffer[..length];
        return name is "ascii" or "646" or "ansi_x3.4_1968" or "ansi_x3.4_1986"
            or "ansi_x3_4_1968" or "cp367" or "csascii" or "ibm367" or "iso646_us"
            or "iso_646.irv_1991" or "iso_ir_6" or "us" or "us_ascii";
    }

    public static bool IsSupportedNewlineName(string newline)
        => newline is "" or "\n" or "\r" or "\r\n";
}
