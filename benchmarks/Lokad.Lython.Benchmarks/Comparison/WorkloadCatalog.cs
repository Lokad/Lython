using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed record ComparisonWorkload(
    string Id, string Family, string Category, string Scale, int Size,
    string Source, string FixtureJson, string ExpectedOutput)
{
    public int SourceUtf8Bytes => Encoding.UTF8.GetByteCount(Source);
    public int ExpectedOutputUtf8Bytes => Encoding.UTF8.GetByteCount(ExpectedOutput);
    public int FixtureTextUtf8Bytes
    {
        get
        {
            using var document = JsonDocument.Parse(FixtureJson);
            var text = document.RootElement.GetProperty("text").GetString();
            return text is null ? 0 : Encoding.UTF8.GetByteCount(text);
        }
    }
    public int FixtureTextCodePoints
    {
        get
        {
            using var document = JsonDocument.Parse(FixtureJson);
            var text = document.RootElement.GetProperty("text").GetString();
            return text?.EnumerateRunes().Count() ?? 0;
        }
    }
    public string SourceSha256 => Digest(Source);
    public string FixtureSha256 => Digest(FixtureJson);
    public string ExpectedOutputSha256 => Digest(ExpectedOutput);
    internal static string Digest(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

// Expected outputs use independent CLR algorithms rather than invoking either
// interpreter or accepting its output as the golden value. Fixtures and sources
// are rendered outside timing; mutable job state is constructed by the shared
// script during every invocation. All rendered source uses canonical LF bytes.
internal static class WorkloadCatalog
{
    public const int Version = 1;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly int[] CoreSizes = [256, 2048, 16384];
    private static readonly int[] TextSizes = [128, 1024, 8192];
    private static readonly int[] RecordSizes = [16, 128, 1024];
    private static readonly string[] ScaleNames = ["small", "medium", "large"];

    public static IReadOnlyList<ComparisonWorkload> Create()
    {
        var cases = new List<ComparisonWorkload>();
        Add("control.empty", "controls", "control", [0], "pass", _ => "", controls: true);
        Add("control.tiny", "controls", "control", [0], "print(42)", _ => "42\n", controls: true);
        Add("loops.integer", "loops", "core", CoreSizes, """
total = 0
for i in range(N):
    total += i
print(total)
""", n => Line((long)n * (n - 1) / 2));
        Add("loops.wide-integer", "loops", "core", CoreSizes, """
base = 9007199254740993
total = 0
for i in range(N):
    total += base + i
print(total)
""", n => (BigInteger.Parse("9007199254740993", Invariant) * n
            + (long)n * (n - 1) / 2).ToString(Invariant) + "\n");
        Add("loops.branch", "loops", "core", CoreSizes, """
total = 0
for i in range(N):
    if i % 3 == 0:
        total += i
    else:
        total -= i
print(total)
""", n => Line(Enumerable.Range(0, n).Sum(i => i % 3 == 0 ? (long)i : -i)));
        Add("loops.dyadic-float", "loops", "core", CoreSizes, """
total = 0.0
for i in range(N):
    total += (i % 16) / 8.0
print(total)
""", n => (15.0 * (n / 16)).ToString("0.0", Invariant) + "\n");
        Add("math.dyadic-fsum", "math", "library", CoreSizes, """
import math
print(math.fsum((i % 16) / 8.0 for i in range(N)))
""", n => (15.0 * (n / 16)).ToString("0.0", Invariant) + "\n");
        Add("calls.positional", "calls", "core", CoreSizes, """
def add(value):
    return value + 3
total = 0
for i in range(N):
    total += add(i)
print(total)
""", n => Line((long)n * (n - 1) / 2 + 3L * n));
        Add("calls.keyword", "calls", "core", CoreSizes, """
def add(value, offset=1):
    return value + offset
total = 0
for i in range(N):
    total += add(i, offset=5)
print(total)
""", n => Line((long)n * (n - 1) / 2 + 5L * n));
        Add("calls.closure", "calls", "core", CoreSizes, """
def outer(offset):
    def add(value):
        return value + offset
    return add
add = outer(3)
total = 0
for i in range(N):
    total += add(i)
print(total)
""", n => Line((long)n * (n - 1) / 2 + 3L * n));
        Add("calls.method", "calls", "core", CoreSizes, """
class Accumulator:
    def __init__(self):
        self.value = 0
    def add(self, value):
        self.value += value + 3
item = Accumulator()
for i in range(N):
    item.add(i)
print(item.value)
""", n => Line((long)n * (n - 1) / 2 + 3L * n));
        Add("lists.copy-slice", "lists", "core", CoreSizes, """
original = [i % 97 for i in range(N)]
copied = original[:]
copied.append(-1)
del copied[::3]
print(original)
print(copied)
""", n => IntList(Enumerable.Range(0, n).Select(i => i % 97)) + "\n"
            + IntList(Enumerable.Range(0, n).Select(i => i % 97).Append(-1)
                .Where((_, i) => i % 3 != 0)) + "\n");
        Add("lists.stable-sort", "lists", "core", CoreSizes, """
values = [(i % 7, i) for i in reversed(range(N))]
values.sort(key=lambda row: row[0])
print(values)
""", n => List(Enumerable.Range(0, n).Reverse().OrderBy(i => i % 7)
            .Select(i => Tuple(i % 7, i))) + "\n");
        Add("tuples.construct-slice", "tuples", "core", CoreSizes, """
values = tuple(range(N))
print(values[::3])
""", n => TupleList(Enumerable.Range(0, n).Where(i => i % 3 == 0)) + "\n");
        Add("dicts.tuple-key-update", "dictionaries", "core", CoreSizes, """
values = {}
for i in range(N):
    key = (i % 37, i % 31)
    values[key] = values.get(key, 0) + i
print(sorted(values.items()))
""", DictionaryExpected);
        Add("sets.difference-union", "sets", "core", CoreSizes, """
values = set(range(N))
excluded = set(range(0, N, 3))
result = (values - excluded) | set(range(N, N + 16))
print(sorted(result))
""", n => IntList(Enumerable.Range(0, n).Where(i => i % 3 != 0)
            .Concat(Enumerable.Range(n, 16))) + "\n");
        Add("generators.drain", "iterators", "core", CoreSizes, """
def values():
    for i in range(N):
        yield (i % 17) ** 2
print(list(values()))
""", n => IntList(Enumerable.Range(0, n).Select(i => (i % 17) * (i % 17))) + "\n");
        Add("generators.early-exit", "iterators", "core", CoreSizes, """
import itertools
def values():
    for i in range(N):
        yield (i % 17) ** 2
print(list(itertools.islice(values(), N // 8)))
""", n => IntList(Enumerable.Range(0, n / 8).Select(i => (i % 17) * (i % 17))) + "\n");
        Add("iterators.zip-enumerate", "iterators", "core", CoreSizes, """
print(list(enumerate(zip(range(N), range(N, 2 * N)), 5)))
""", n => List(Enumerable.Range(0, n).Select(i => $"({i + 5}, ({i}, {i + n}))")) + "\n");

        foreach (var (name, block) in new[] { ("ascii", "abZ!"), ("bmp", "aλ中z"), ("supplementary", "a😀𝄞z") })
        {
            Add("strings.scan-" + name, "strings", "core", TextSizes, """
total = 0
for character in TEXT:
    total += ord(character)
print(len(TEXT), total)
""", n => $"{4 * n} {(long)n * block.EnumerateRunes().Sum(r => r.Value)}\n", n => Repeat(block, n));
            Add("strings.index-" + name, "strings", "core", TextSizes, """
total = 0
for i in range(512):
    total += ord(TEXT[(i * 257 + 17) % len(TEXT)])
print(total)
""", n => Line(Enumerable.Range(0, 512).Sum(i => (long)block.EnumerateRunes().ToArray()
                [((i * 257 + 17) % (4 * n)) % 4].Value)), n => Repeat(block, n));
            Add("strings.pipeline-" + name, "strings", "core", TextSizes,
                "print('|'.join(TEXT.replace('::', '/').split('/')))",
                n => Repeat(block + "|tail|", n) + "\n", n => Repeat(block + "::tail/", n));
        }

        Add("json.parse", "json", "library", RecordSizes, """
import json
values = json.loads(TEXT)
print(values)
""", n => JsonRowsRepr(n), JsonRows);
        Add("json.transform-roundtrip", "json", "library", RecordSizes, """
import json
values = json.loads(TEXT)
for row in values:
    row['active'] = not row['active']
print(json.dumps(values, ensure_ascii=True, separators=(',', ':')))
""", n => JsonRows(n, invert: true) + "\n", JsonRows);
        Add("csv.retain", "csv", "library", RecordSizes, """
import csv
import io
print(list(csv.DictReader(io.StringIO(TEXT, newline=''))))
""", n => CsvRowsRepr(n), CsvText);
        Add("csv.discard", "csv", "library", RecordSizes, """
import csv
import io
count = 0
total = 0
text_total = 0
for row in csv.DictReader(io.StringIO(TEXT, newline='')):
    count += 1
    total += int(row['id'])
    for character in row['text']:
        text_total += ord(character)
print(count, total, text_total)
""", n => $"{n} {(long)n * (n - 1) / 2} {Enumerable.Range(0, n).Sum(i => (long)("row," + i.ToString(Invariant) + " λ😀").EnumerateRunes().Sum(r => r.Value))}\n", CsvText);
        Add("csv.early-exit", "csv", "library", RecordSizes, """
import csv
import io
rows = []
for row in csv.DictReader(io.StringIO(TEXT, newline='')):
    rows.append(row)
    if len(rows) == N // 8:
        break
print(rows)
""", n => CsvRowsRepr(n / 8), CsvText);
        Add("csv.write-memory", "csv", "library", RecordSizes, """
import csv
import io
output = io.StringIO(newline='')
writer = csv.writer(output, lineterminator='\r\n')
writer.writerow(['id', 'text', 'flag'])
for i in range(N):
    writer.writerow([str(i), 'row,' + str(i) + ' λ😀', str(i % 2)])
print(repr(output.getvalue()))
""", n => Quote(CsvText(n)) + "\n");
        Add("collections.counter", "collections", "library", CoreSizes, """
from collections import Counter
print(sorted(Counter(TEXT).items()))
""", n => $"[('a', {2 * n}), ('b', {n}), ('c', {n})]\n", n => Repeat("abca", n));
        Add("collections.deque", "collections", "library", CoreSizes, """
from collections import deque
values = deque(range(N))
for i in range(N):
    values.appendleft(values.pop())
print(list(values))
""", n => IntList(Enumerable.Range(0, n)) + "\n");
        Add("collections.chainmap", "collections", "library", CoreSizes, """
from collections import ChainMap
base = dict((i, i) for i in range(N))
overrides = dict((i, -i) for i in range(0, N, 3))
values = ChainMap(overrides, base)
print([(i, values[i]) for i in range(N)])
""", n => List(Enumerable.Range(0, n).Select(i => Tuple(i, i % 3 == 0 ? -i : i))) + "\n");
        Add("text.html", "text", "library", TextSizes, """
import html
print(html.unescape(html.escape(TEXT)))
""", n => Repeat("<a title=\"a&b\">x'y</a>", n) + "\n", n => Repeat("<a title=\"a&b\">x'y</a>", n));
        Add("text.textwrap", "text", "library", TextSizes, """
import textwrap
print(textwrap.indent(textwrap.dedent(TEXT), '> '), end='')
""", n => Repeat("> alpha\n>   beta\n", n), n => Repeat("    alpha\n      beta\n", n));
        Add("text.url-roundtrip", "text", "library", RecordSizes, """
from urllib.parse import urlencode, parse_qsl
pairs = [(str(i), 'a λ😀&=' + str(i)) for i in range(N)]
print(parse_qsl(urlencode(pairs), keep_blank_values=True))
""", n => List(Enumerable.Range(0, n).Select(i => $"({Quote(i.ToString(Invariant))}, {Quote("a λ😀&=" + i.ToString(Invariant))})")) + "\n");
        Add("codecs.utf16", "codecs", "library", TextSizes, """
data = TEXT.encode('utf-16-le')
print(data.hex())
print(data.decode('utf-16-le'))
""", n => Convert.ToHexStringLower(Encoding.Unicode.GetBytes(Repeat("aλ😀𝄞", n))) + "\n"
            + Repeat("aλ😀𝄞", n) + "\n", n => Repeat("aλ😀𝄞", n));
        Add("struct.pack-unpack", "struct", "library", RecordSizes, """
import struct
packets = [struct.pack('<iH', i, i % 256) for i in range(N)]
print(b''.join(packets).hex())
print([struct.unpack('<iH', data) for data in packets])
""", StructExpected);
        Add("xml.parse-select", "xml", "library", RecordSizes, """
import xml.etree.ElementTree as ET
root = ET.fromstring(TEXT)
print([(item.get('id'), item.text) for item in root.findall('item')])
""", n => List(Enumerable.Range(0, n).Select(i => $"({Quote(i.ToString(Invariant))}, 'aλ😀')")) + "\n",
            n => "<root>" + string.Concat(Enumerable.Range(0, n).Select(i => $"<item id=\"{i}\">aλ😀</item>")) + "</root>");
        Add("compression.gzip", "compression", "native-library", TextSizes, """
import gzip
data = TEXT.encode('utf-8')
compressed = gzip.compress(data, compresslevel=6, mtime=0)
print(gzip.decompress(compressed).hex())
""", n => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(Repeat("0123456789abcdef", n))) + "\n",
            n => Repeat("0123456789abcdef", n));
        Add("compression.zlib", "compression", "native-library", TextSizes, """
import zlib
data = TEXT.encode('utf-8')
compressed = zlib.compress(data, level=6)
print(zlib.decompress(compressed).hex())
""", n => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(EntropyText(n))) + "\n", EntropyText);
        return cases;

        void Add(string id, string family, string category, int[] sizes, string body,
            Func<int, string> expected, Func<int, string>? text = null, bool controls = false)
        {
            for (var index = 0; index < sizes.Length; index++)
            {
                var size = sizes[index];
                var fixture = text?.Invoke(size);
                var fixtureJson = JsonSerializer.Serialize(new { size, text = fixture });
                var prefix = controls ? "" : "N = " + size.ToString(Invariant) + "\n";
                if (fixture is not null) prefix += "TEXT = " + Quote(fixture) + "\n";
                var source = prefix + body.Replace("\r\n", "\n").TrimEnd() + "\n";
                var scale = controls ? "control" : ScaleNames[index];
                cases.Add(new ComparisonWorkload(id + "." + scale, family, category, scale,
                    size, source, fixtureJson, expected(size)));
            }
        }
    }

    private static string DictionaryExpected(int n)
    {
        var totals = new Dictionary<(int, int), long>();
        foreach (var i in Enumerable.Range(0, n))
        {
            var key = (i % 37, i % 31);
            totals.TryGetValue(key, out var previous);
            totals[key] = previous + i;
        }
        return List(totals.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2)
            .Select(p => $"(({p.Key.Item1}, {p.Key.Item2}), {p.Value})")) + "\n";
    }

    private static string JsonRows(int n) => JsonRows(n, invert: false);
    private static string JsonRows(int n, bool invert)
        => "[" + string.Join(",", Enumerable.Range(0, n).Select(i =>
            $"{{\"id\":{i},\"text\":\"a\\u03bb\\ud83d\\ude00\",\"active\":{((i % 2 == 0) != invert ? "true" : "false")}}}")) + "]";

    private static string JsonRowsRepr(int n)
        => List(Enumerable.Range(0, n).Select(i =>
            $"{{'id': {i}, 'text': 'aλ😀', 'active': {(i % 2 == 0 ? "True" : "False")}}}")) + "\n";

    private static string CsvText(int n)
        => "id,text,flag\r\n" + string.Concat(Enumerable.Range(0, n).Select(i =>
            $"{i},\"row,{i} λ😀\",{i % 2}\r\n"));

    private static string CsvRowsRepr(int n)
        => List(Enumerable.Range(0, n).Select(i =>
            $"{{'id': '{i}', 'text': 'row,{i} λ😀', 'flag': '{i % 2}'}}")) + "\n";

    private static string StructExpected(int n)
    {
        var bytes = new byte[n * 6];
        for (var i = 0; i < n; i++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 6), i);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 6 + 4), (ushort)(i % 256));
        }
        return Convert.ToHexStringLower(bytes) + "\n"
            + List(Enumerable.Range(0, n).Select(i => Tuple(i, i % 256))) + "\n";
    }

    private static string EntropyText(int n)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var buffer = new char[n * 16];
        uint state = 0x5EED1234;
        for (var i = 0; i < buffer.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            buffer[i] = alphabet[(int)(state & 63)];
        }
        return new string(buffer);
    }

    private static string Repeat(string block, int n) => string.Concat(Enumerable.Repeat(block, n));
    private static string Line(long value) => value.ToString(Invariant) + "\n";
    private static string List(IEnumerable<string> values) => "[" + string.Join(", ", values) + "]";
    private static string IntList(IEnumerable<int> values) => List(values.Select(v => v.ToString(Invariant)));
    private static string Tuple(int first, int second) => $"({first.ToString(Invariant)}, {second.ToString(Invariant)})";
    private static string TupleList(IEnumerable<int> values)
    {
        var items = values.Select(v => v.ToString(Invariant)).ToArray();
        return "(" + string.Join(", ", items) + (items.Length == 1 ? "," : "") + ")";
    }

    internal static string Quote(string text)
    {
        var quote = text.Contains('\'') && !text.Contains('"') ? '"' : '\'';
        var result = new StringBuilder(text.Length + 2).Append(quote);
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == quote || rune.Value == '\\') result.Append('\\').Append((char)rune.Value);
            else if (rune.Value == '\n') result.Append("\\n");
            else if (rune.Value == '\r') result.Append("\\r");
            else if (rune.Value == '\t') result.Append("\\t");
            else if (rune.Value < 32 || rune.Value == 127) result.Append("\\x").Append(rune.Value.ToString("x2", Invariant));
            else result.Append(rune.ToString());
        }
        return result.Append(quote).ToString();
    }
}
