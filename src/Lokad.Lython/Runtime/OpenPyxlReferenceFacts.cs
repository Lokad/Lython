namespace Lokad.Lython.Runtime;

internal static class OpenPyxlReferenceFacts
{
    public static bool IsCellReference(string text)
    {
        var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var index = 0;
        while (index < normalized.Length && char.IsLetter(normalized[index]))
        {
            index++;
        }

        return index > 0 &&
            index < normalized.Length &&
            normalized.AsSpan(index).IndexOfAnyExceptInRange('0', '9') < 0;
    }
}
