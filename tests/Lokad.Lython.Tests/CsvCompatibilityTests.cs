using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvCompatibilityTests
{
    [Fact]
    public void DictReader_HandlesHeadersExplicitFieldnamesRestValuesAndIteration()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv

derived = csv.DictReader(["name,qty", "alpha,2", "beta", "gamma,3,extra"], restkey="extra", restval="0")
vals = []
for row in derived:
    vals.append(row["name"] + ":" + row["qty"] + ":" + str(row.get("extra", [])))

explicit = csv.DictReader(["alpha,2", "beta"], fieldnames=["name", "qty"], restval="0")
vals.append(",".join(explicit.fieldnames))
for row in explicit:
    vals.append(row["name"] + ":" + row["qty"])

empty = csv.DictReader([])
vals.append(str(empty.fieldnames))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha:2:[]|beta:0:[]|gamma:3:[extra]|name,qty|alpha:2|beta:0|None", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Reader_HandlesFileHandlesAndMultilineQuotedRecords()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.csv", "name,note\nalpha,\"hello\nworld\"\nbeta,z\n");

        var result = new LythonEngine().Run(
            """
import csv

physical = []
with open("/input.csv", newline="") as handle:
    for line in handle:
        physical.append(line)

with open("/input.csv", newline="") as handle:
    rows = list(csv.reader(handle))

manual = list(csv.reader(["name,note", "alpha,\"hello", "world\"", "beta,z"]))
write_text("/out.txt", str(len(physical)) + "|" + rows[1][1] + "|" + manual[1][1] + "|" + str(rows[0]) + "|" + str(rows[2]))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4|hello\nworld|hello\nworld|[name, note]|[beta, z]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Writer_SupportsFileHandlesConstantsOptionsAndScalarConversion()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv

memory = csv.writer(delimiter=";", quotechar="'", quoting=csv.QUOTE_ALL)
memory.writerow(["a;b", "c"])
memory.writerow([1, 2.5, True, None])

with open("/out.csv", "w", newline="") as handle:
    writer = csv.writer(handle, delimiter="\t")
    writer.writerow(["name", "qty"])
    writer.writerow(["alpha", 2])

write_text("/memory.csv", memory.getvalue())
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("'a;b';'c'\n'1';'2.5';'True';''", host.ReadText("/memory.csv"));
        Assert.Equal("name\tqty\nalpha\t2\n", host.ReadText("/out.csv"));
    }

    [Fact]
    public void DictWriter_WritesHeadersMissingFieldsIgnoresOrRaisesExtras()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv

status = "missing"
with open("/out.csv", "w", newline="") as handle:
    writer = csv.DictWriter(handle, ["name", "qty"], restval="0")
    writer.writeheader()
    writer.writerow({"name": "alpha"})
    try:
        writer.writerow({"name": "beta", "qty": 2, "extra": True})
    except csv.Error as ex:
        status = "caught"

with open("/ignored.csv", "w", newline="") as handle:
    writer = csv.DictWriter(handle, ["name"], extrasaction="ignore")
    writer.writerow({"name": "gamma", "extra": 1})

write_text("/status.txt", status)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("caught", host.ReadText("/status.txt"));
        Assert.Equal("name,qty\nalpha,0\n", host.ReadText("/out.csv"));
        Assert.Equal("gamma\n", host.ReadText("/ignored.csv"));
    }

    [Fact]
    public void CsvError_IsCatchableForReaderFailures()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv

status = "missing"
try:
    list(csv.reader(["\"broken"]))
except csv.Error as ex:
    status = "caught:" + str(ex)
write_text("/out.txt", status)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Contains("caught:Error", host.ReadText("/out.txt"), StringComparison.Ordinal);
    }

    [Fact]
    public void Csv_StaticDiagnosticsCoverExpandedSurface()
    {
        var compiled = new LythonEngine().Compile(
            """
import csv

csv.nope()
csv.reader(1)
csv.reader(["a"], delimiter=1)
csv.reader(["a"], quotechar="")
csv.reader(["a"], quoting=99)
csv.reader(["a"], dialect="excel-tab")
csv.DictReader(["a"], fieldnames=[1])
csv.DictWriter(1, ["name"])
csv.DictWriter(open("/repo/in.csv", "r"), ["name"])
csv.DictWriter(open("/repo/out.csv", "w"), [1])
csv.DictWriter(open("/repo/out.csv", "w"), ["name"], extrasaction="skip")
writer = csv.DictWriter(open("/repo/out.csv", "w"), ["name"])
writer.writerow(["not", "a", "dict"])
csv.register_dialect("x")
csv.Sniffer()
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("module 'csv' has no member 'nope'", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3066");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3067");
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3068");
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("fieldnames", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("writable text file handle", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("file is not open for writing", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("rowdict", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("register_dialect", StringComparison.Ordinal));
        Assert.Contains(compiled.Diagnostics, d => d.Message.Contains("Sniffer", StringComparison.Ordinal));
    }
}
