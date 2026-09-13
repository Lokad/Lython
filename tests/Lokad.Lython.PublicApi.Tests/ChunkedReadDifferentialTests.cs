using System.Diagnostics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01/MG21: chunked file reads match CPython exactly across encodings, error
/// handlers and newline modes, including split multi-byte characters, split
/// CRLF pairs and BOM handling. Two seeded blobs (BOM plus invalid bytes, and
/// a clean one) run through every supported combination in one CPython process
/// and in both Lython execution modes; outcomes compare byte-exactly.
/// Ultra-short files (undecidable BOM prefixes) stay with the dedicated unit
/// pins: chunked and whole-buffer Lython decoding agree there by construction.
/// </summary>
public sealed class ChunkedReadDifferentialTests
{
    private const string Driver = """
        import sys, base64
        LF = chr(10)
        CR = chr(13)
        encs = ["utf-8", "utf-8-sig", "latin-1"]
        errs = ["strict", "ignore", "replace", "backslashreplace"]
        nls = [None, "", LF, CR, CR + LF]
        SEP = chr(30)
        path = sys.argv[1]
        lines_out = []
        for enc in encs:
            for err in errs:
                for nl in nls:
                    try:
                        f = open(path, "r", encoding=enc, errors=err, newline=nl)
                        rows = list(f)
                        f.close()
                        payload = base64.b64encode(SEP.join(rows).encode("utf-8")).decode("ascii")
                    except Exception as e:
                        payload = "ERROR:" + type(e).__name__
                    lines_out.append(payload)
        sys.stdout.write("\n".join(lines_out) + "\n")
        """;

    private const string Probe = """
        encs = ["utf-8", "utf-8-sig", "latin-1"]
        errs = ["strict", "ignore", "replace", "backslashreplace"]
        nls = [None, "", chr(10), chr(13), chr(13) + chr(10)]
        sep = chr(30)
        results = []
        for enc in encs:
            for err in errs:
                for nl in nls:
                    try:
                        f = open(PATH, encoding=enc, errors=err, newline=nl)
                        rows = [line for line in f]
                        f.close()
                        results.append(sep.join(rows))
                    except Exception as e:
                        results.append("ERROR:" + type(e).__name__)
        return results
        """;

    [Fact]
    public async Task ChunkedReadsMatchCpython()
    {
        var blobs = new[] { BuildBlob(withBom: true, seed: 42), BuildBlob(withBom: false, seed: 1337) };
        var paths = new[] { "/b0.bin", "/b1.bin" };
        // The repo compares against CPython as a matter of course (see the
        // probe tool); a differential pin without it would prove nothing, so
        // a missing interpreter fails loudly instead of skipping silently.
        var python = ResolveCpython();

        var driverPath = Path.Combine(Path.GetTempPath(), "lython-fuzz-" + Guid.NewGuid().ToString("N") + ".py");
        await File.WriteAllTextAsync(driverPath, Driver);
        try
        {
            for (var b = 0; b < blobs.Length; b++)
            {
                var blobPath = Path.Combine(Path.GetTempPath(), "lython-blob-" + Guid.NewGuid().ToString("N") + ".bin");
                await File.WriteAllBytesAsync(blobPath, blobs[b]);
                try
                {
                    var expected = RunCpython(python, driverPath, blobPath);
                    await CheckBlobAsync(blobs[b], paths[b], expected, sync: true);
                    await CheckBlobAsync(blobs[b], paths[b], expected, sync: false);
                }
                finally
                {
                    File.Delete(blobPath);
                }
            }
        }
        finally
        {
            File.Delete(driverPath);
        }
    }

    private static async Task CheckBlobAsync(byte[] blob, string path, List<string> expected, bool sync)
    {
        var script = new LythonEngine().Compile(Probe.Replace("PATH", "\"" + path + "\""));
        Assert.True(script.IsValid);
        var host = new MockLythonHost();
        host.SeedRawTextUtf8(path, blob);
        // Latin-1 reads travel the binary capability, which the mock stores
        // separately like whole-buffer reads always have; seed both stores
        // with identical bytes so every encoding reads the same fixture.
        host.SeedBytes(path, blob);
        var result = sync
            ? script.Run(host)
            : await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        var actual = Assert.IsAssignableFrom<List<object?>>(result.ReturnValue);
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i] == actual[i]?.ToString(), $"config {i}: expected {expected[i]} but was {actual[i]}");
        }
    }

    private static List<string> RunCpython(string python, string driverPath, string blobPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = python,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(driverPath);
        startInfo.ArgumentList.Add(blobPath);
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEnd();
        var errors = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(120000), "CPython differential run timed out: " + errors);
        Assert.Equal(0, process.ExitCode);
        // CPython text-mode stdout translates newlines on Windows, so every
        // line may trail a carriage return; payloads never legitimately start
        // or end with whitespace.
        var decoded = new List<string>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            decoded.Add(line.StartsWith("ERROR:", StringComparison.Ordinal)
                ? line
                : Encoding.UTF8.GetString(Convert.FromBase64String(line)));
        }

        Assert.Equal(60, decoded.Count);
        return decoded;
    }

    private static string ResolveCpython()
    {
        var configured = Environment.GetEnvironmentVariable("LYTHON_DIFFTEST_PYTHON");
        var candidates = configured is null
            ? new[] { "python", "python3" }
            : new[] { configured };
        foreach (var candidate in candidates)
        {
            try
            {
                var probe = new ProcessStartInfo
                {
                    FileName = candidate,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                probe.ArgumentList.Add("--version");
                using var process = Process.Start(probe);
                if (process is null)
                {
                    continue;
                }

                process.WaitForExit(15000);
                if (process.ExitCode == 0)
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // Try the next candidate.
            }
        }

        throw new InvalidOperationException(
            "The chunked-read differential pin requires a CPython executable (tried '" +
            string.Join("', '", candidates) +
            "'); set LYTHON_DIFFTEST_PYTHON to its path.");
    }

    private static byte[] BuildBlob(bool withBom, int seed)
    {
        var bytes = new List<byte>();
        if (withBom)
        {
            bytes.AddRange(new byte[] { 0xEF, 0xBB, 0xBF });
        }

        void Ascii(string text)
        {
            bytes.AddRange(Encoding.ASCII.GetBytes(text));
        }

        void Utf8(string text)
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(text));
        }

        Ascii("id,name,note\n");
        Utf8("1,h\u00e9llo,\"a,b\"\n");
        Utf8("2,\U0001F600,\"multi\r\nline\"\n");
        Ascii("3,lone\rmid\n");
        Ascii("4,crlf\r\nline\n");
        Ascii("5,tab\there\n");
        Ascii("6,empty,,fields\n");
        if (withBom)
        {
            bytes.Add(0xFF);
            bytes.Add(0x80);
            bytes.AddRange(new byte[] { 0xE2, 0x28 });
        }

        Ascii("pad,pad,pad\n");
        var random = new Random(seed);
        var probes = new[] { "a", "bc", "def", "axis", "row", "value", "xyzzy" };
        var multis = new[] { "\u00e9", "\u4e2d", "\U0001F600", "\u00f1" };
        var seps = new[] { "\n", "\r\n", "\r" };
        var puncts = new[] { ",", "\"", "'", " ", "\t" };
        while (bytes.Count < 20480)
        {
            var roll = random.Next(100);
            if (roll < 55)
            {
                Ascii(probes[random.Next(probes.Length)]);
            }
            else if (roll < 65)
            {
                Utf8(multis[random.Next(multis.Length)]);
            }
            else if (roll < 80)
            {
                Ascii(seps[random.Next(seps.Length)]);
            }
            else if (roll < 95)
            {
                Ascii(puncts[random.Next(puncts.Length)]);
            }
            else if (withBom)
            {
                bytes.Add(random.Next(3) switch { 0 => (byte)0xFF, 1 => (byte)0x80, _ => (byte)0xE2 });
            }
            else
            {
                Ascii("q");
            }
        }

        Ascii("tail,without,newline");
        return bytes.ToArray();
    }
}
