namespace Lokad.Lython.Tests.Harness;

internal static class FixtureAssertions
{
    public static void AssertTextEqual(string expected, string actual)
    {
        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    private static string Normalize(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n');
    }
}
