using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal static class ComparisonProtocol
{
    public const int Version = 1;
    public const int MaximumFrameBytes = 4 * 1024 * 1024;
    public const int MaximumCatalogBytes = 64 * 1024 * 1024;
    public const int MaximumBatchIterations = 1_000_000;
    public const int MaximumBatchSeconds = 60;
    internal static readonly UTF8Encoding Utf8 = new(false, throwOnInvalidBytes: true);
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Four-byte network-order length followed by strictly decoded UTF-8 JSON.
    // Validate the length before allocating; clean EOF and truncation differ.
    public static async ValueTask<JsonDocument?> ReadAsync(Stream input, CancellationToken cancellationToken = default)
    {
        var header = new byte[4];
        if (await input.ReadAsync(header.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) == 0)
            return null;
        await input.ReadExactlyAsync(header.AsMemory(1), cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > MaximumFrameBytes)
            throw new InvalidDataException("Invalid comparison frame length.");
        var payload = new byte[length];
        await input.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        var document = JsonDocument.Parse(Utf8.GetString(payload), new JsonDocumentOptions { MaxDepth = 32 });
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("A comparison frame must be a JSON object.");
            ValidateUniqueProperties(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    public static async ValueTask WriteAsync(Stream output, object value, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (payload.Length == 0 || payload.Length > MaximumFrameBytes)
            throw new InvalidDataException("Comparison response exceeds the frame limit.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string Digest(string value) => Digest(Utf8.GetBytes(value));
    public static string Digest(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));

    internal static void ValidateUniqueProperties(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate comparison JSON property.");
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in root.EnumerateArray()) ValidateUniqueProperties(child);
        }
    }
}

internal sealed class ComparisonManifest
{
    public string Sha256 { get; }
    public IReadOnlyDictionary<string, ComparisonWorkload> Cases { get; }

    private ComparisonManifest(string sha256, Dictionary<string, ComparisonWorkload> cases)
        => (Sha256, Cases) = (sha256, cases);

    public static ComparisonManifest Load(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length <= 0 || file.Length > ComparisonProtocol.MaximumCatalogBytes)
            throw new InvalidDataException("Invalid comparison catalog size.");
        var bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        using var document = JsonDocument.Parse(ComparisonProtocol.Utf8.GetString(bytes));
        var root = document.RootElement;
        ComparisonProtocol.ValidateUniqueProperties(root);
        if (root.GetProperty("schemaVersion").GetInt32() != 1
            || root.GetProperty("catalogVersion").GetInt32() != WorkloadCatalog.Version)
            throw new InvalidDataException("Unsupported comparison catalog version.");
        var rows = root.GetProperty("cases");
        if (rows.GetArrayLength() is < 1 or > 256)
            throw new InvalidDataException("Invalid comparison case count.");
        var cases = new Dictionary<string, ComparisonWorkload>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            string Text(string name) => row.GetProperty(name).GetString()
                ?? throw new InvalidDataException("Missing catalog text: " + name);
            var workload = new ComparisonWorkload(Text("id"), Text("family"), Text("category"), Text("scale"),
                row.GetProperty("size").GetInt32(), Text("source"), Text("fixtureJson"), Text("expectedOutput"));
            if (workload.Id.Length is < 1 or > 128 || workload.Size < 0
                || workload.Source.Length is < 1 or > LythonEngine.MaxSourceLength
                || ComparisonProtocol.Utf8.GetByteCount(workload.ExpectedOutput) > LythonRunOptions.DefaultMaxStandardOutputBytes)
                throw new InvalidDataException("Catalog case exceeds declared bounds: " + workload.Id);
            using var fixture = JsonDocument.Parse(workload.FixtureJson);
            ComparisonProtocol.ValidateUniqueProperties(fixture.RootElement);
            if (fixture.RootElement.GetProperty("size").GetInt32() != workload.Size
                || fixture.RootElement.GetProperty("text").ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                throw new InvalidDataException("Invalid fixture size/text metadata: " + workload.Id);
            if (ComparisonProtocol.Digest(workload.Source) != Text("sourceSha256")
                || ComparisonProtocol.Digest(workload.FixtureJson) != Text("fixtureSha256")
                || ComparisonProtocol.Digest(workload.ExpectedOutput) != Text("expectedOutputSha256"))
                throw new InvalidDataException("Catalog digest mismatch: " + workload.Id);
            if (!cases.TryAdd(workload.Id, workload))
                throw new InvalidDataException("Duplicate comparison case: " + workload.Id);
        }
        return new ComparisonManifest(ComparisonProtocol.Digest(bytes), cases);
    }
}
