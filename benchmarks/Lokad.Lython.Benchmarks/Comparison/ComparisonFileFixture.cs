using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

// Trusted benchmark data, not a runtime capability or a Python sandbox.
internal sealed record ComparisonFileFixture(IReadOnlyDictionary<string, byte[]> Inputs,
    IReadOnlyDictionary<string, byte[]> Outputs)
{
    public const int MaximumFiles = 64;
    public const int MaximumBytes = 16 * 1024 * 1024;

    public static ComparisonFileFixture? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var hasInputs = root.TryGetProperty("inputFiles", out var inputs);
        var hasOutputs = root.TryGetProperty("outputFiles", out var outputs);
        if (!hasInputs && !hasOutputs) return null;
        if (!hasInputs || !hasOutputs) throw new InvalidDataException("File fixtures need inputs and output goldens.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long bytes = 0;
        Dictionary<string, byte[]> Read(JsonElement value, IReadOnlyDictionary<string, byte[]>? overwrites = null)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid file fixture map.");
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in value.EnumerateObject())
            {
                ValidatePath(entry.Name);
                // A job may overwrite its exact input path. Ambiguous case
                // spellings and duplicates within either map remain invalid.
                if (result.ContainsKey(entry.Name) ||
                    (!names.Add(entry.Name) && overwrites?.ContainsKey(entry.Name) != true) || names.Count > MaximumFiles)
                    throw new InvalidDataException("Duplicate or excessive file fixture paths.");
                var encoded = entry.Value.GetString() ?? throw new InvalidDataException("Missing file payload.");
                if (encoded.Length > 4 * ((MaximumBytes + 2) / 3)) throw new InvalidDataException("File fixture is too large.");
                byte[] payload;
                try { payload = Convert.FromBase64String(encoded); }
                catch (FormatException error) { throw new InvalidDataException("Invalid file base64.", error); }
                if (Convert.ToBase64String(payload) != encoded || (bytes += payload.Length) > MaximumBytes)
                    throw new InvalidDataException("Noncanonical or excessive file fixture bytes.");
                result.Add(entry.Name, payload);
            }
            return result;
        }
        var inputFiles = Read(inputs);
        var outputFiles = Read(outputs, inputFiles);
        foreach (var name in names)
            for (var slash = name.IndexOf('/'); slash >= 0; slash = name.IndexOf('/', slash + 1))
                if (names.Contains(name[..slash])) throw new InvalidDataException("A fixture file is also a parent directory.");
        return new(inputFiles, outputFiles);
    }

    public static void ValidatePath(string path)
    {
        if (path.Length is < 1 or > 256 || path.Any(c => c < 32 || "\\:<>\"|?*".Contains(c)))
            throw new InvalidDataException("Use bounded portable relative fixture paths.");
        foreach (var part in path.Split('/'))
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' '))
                throw new InvalidDataException("Invalid fixture path segment.");
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] is >= '0' and <= '9'))
                throw new InvalidDataException("Reserved fixture path.");
        }
        _ = ComparisonProtocol.Utf8.GetByteCount(path);
    }

    // Count and length-prefixed UTF-8 paths/content; ordinal UTF-16 path order.
    // Artifact hashing stays outside persistent batch clocks.
    public static string Digest(IReadOnlyDictionary<string, byte[]> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var length = new byte[4];
        void Number(int value) { BinaryPrimitives.WriteInt32BigEndian(length, value); hash.AppendData(length); }
        Number(files.Count);
        foreach (var file in files.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var path = ComparisonProtocol.Utf8.GetBytes(file.Key);
            Number(path.Length); hash.AppendData(path);
            Number(file.Value.Length); hash.AppendData(file.Value);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

}
