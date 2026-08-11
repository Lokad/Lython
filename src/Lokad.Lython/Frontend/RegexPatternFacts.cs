namespace Lokad.Lython.Frontend;

internal readonly record struct RegexGroupSummary(
    int CaptureSlotCount,
    IReadOnlyDictionary<string, int> NamedGroups,
    bool IsComplete);

internal static class RegexPatternFacts
{
    private static readonly IReadOnlyDictionary<string, int> EmptyNamedGroups =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public static RegexGroupSummary SummarizeGroups(string pattern)
    {
        var count = 1;
        Dictionary<string, int>? names = null;
        var inClass = false;
        var isComplete = true;
        var noNamedGroupTerminatorRemaining = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch == '\\')
            {
                i++;
                continue;
            }

            if (ch == '[')
            {
                inClass = true;
                continue;
            }

            if (ch == ']' && inClass)
            {
                inClass = false;
                continue;
            }

            if (inClass || ch != '(')
            {
                continue;
            }

            if (i + 1 >= pattern.Length || pattern[i + 1] != '?')
            {
                count++;
                continue;
            }

            if (i + 3 < pattern.Length && pattern[i + 2] == 'P' && pattern[i + 3] == '<')
            {
                if (noNamedGroupTerminatorRemaining)
                {
                    isComplete = false;
                    continue;
                }

                var end = pattern.IndexOf('>', i + 4);
                if (end < 0)
                {
                    // The first failed search proves that every later named-group
                    // opener is also unterminated; keep scanning other group forms.
                    noNamedGroupTerminatorRemaining = true;
                    isComplete = false;
                    continue;
                }

                names ??= new Dictionary<string, int>(StringComparer.Ordinal);
                names[pattern[(i + 4)..end]] = count;
                count++;
                i = end;
                continue;
            }

            if (i + 2 < pattern.Length && pattern[i + 2] is ':' or '=' or '!')
            {
                continue;
            }

            if (i + 3 < pattern.Length && pattern[i + 2] == '<' && pattern[i + 3] is '=' or '!')
            {
                continue;
            }

            if (TrySkipInlineFlags(pattern, i + 2, out var endIndex))
            {
                i = endIndex;
                continue;
            }

            // The regex engine remains authoritative. An unfamiliar extension is
            // treated as non-capturing, while static analysis avoids exact claims.
            isComplete = false;
        }

        return new RegexGroupSummary(count, names ?? EmptyNamedGroups, isComplete);

        static bool TrySkipInlineFlags(string source, int start, out int endIndex)
        {
            var i = start;
            while (i < source.Length && (char.IsLetter(source[i]) || source[i] == '-'))
            {
                i++;
            }

            if (i != start && i < source.Length && source[i] is ')' or ':')
            {
                endIndex = i;
                return true;
            }

            endIndex = start;
            return false;
        }
    }
}
