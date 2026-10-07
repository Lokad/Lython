using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace Lokad.Lython.Runtime.Text;

// Finite, immutable language data shared like other builtin literals. Guest
// output always copies these values into independently governed strings.
internal static partial class HtmlEntityData
{
    internal const string ResourceName = "Lokad.Lython.html5.json";
    private static readonly FrozenDictionary<string, Entity> Named = Load();
    internal readonly record struct Entity(PyString Text, int Scalars);
    internal static int NamedCount => Named.Count;

    internal static bool TryGetNamed(ReadOnlySpan<char> name, out Entity value)
        => Named.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out value);

    private static FrozenDictionary<string, Entity> Load()
    {
        using var stream = typeof(HtmlEntityData).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Missing embedded HTML5 entity table.");
        using var document = JsonDocument.Parse(stream);
        var values = new Dictionary<string, Entity>(2231, StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var text = property.Value.GetString()!;
            var scalars = 0;
            foreach (var rune in text.EnumerateRunes()) scalars++;
            values.Add(property.Name, new Entity(PyString.FromString(text), scalars));
        }
        return values.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
