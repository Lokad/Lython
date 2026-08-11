using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvCompatibilityTests
{
    [Fact]
    public void ModuleExceptionTypes_RetainQualifiedIdentityAndResolveAliasesInExceptClauses()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import copy
import csv as c
import shutil

values = [c.Error == copy.Error, c.Error == shutil.Error, shutil.Error == shutil.SameFileError]
try:
    raise c.Error("csv failure")
except c.Error as ex:
    values.append(ex.type == "Error")

__lython_file = open("/out.txt", "w")
__lython_file.write(str(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[False, False, False, True]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CsvWriter_RejectsUndocumentedDelimiterShorthand()
    {
        var result = new LythonEngine().Run("import csv\ncsv.writer(';')", new MockLythonHost());

        Assert.False(result.Success);
        var failure = result.Failure.RequireNotNull();
        Assert.Equal("TypeError", failure.ExceptionType);
        Assert.Contains("expects a text file handle", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvWriters_PropagateUnderlyingWriteCounts()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv

with open("/rows.csv", "w", newline="") as handle:
    row_count = csv.writer(handle, lineterminator="\n").writerow(["a"])

with open("/dict.csv", "w", newline="") as handle:
    header_count = csv.DictWriter(handle, fieldnames=["x"], lineterminator="\n").writeheader()

__lython_file = open("/out.txt", "w")
__lython_file.write(str(row_count) + "|" + str(header_count))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CsvWriters_QuoteSingleEmptyFieldsAndReportExactCounts()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import csv

memory = csv.writer(lineterminator="\n")
memory_count = memory.writerow([""])
with open("/empty.csv", "w", newline="") as handle:
    file_count = csv.writer(handle, lineterminator="\n").writerow([""])

__lython_file = open("/out.txt", "w")
__lython_file.write(str(memory_count) + "|" + repr(memory.getvalue()) + "|" + str(file_count))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("3|'\"\"'|3", host.ReadText("/out.txt"));
        Assert.Equal("\"\"\n", host.ReadText("/empty.csv"));
    }

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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha:2:[]|beta:0:[]|gamma:3:['extra']|name,qty|alpha:2|beta:0|None", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write(str(len(physical)) + "|" + rows[1][1] + "|" + manual[1][1] + "|" + str(rows[0]) + "|" + str(rows[2]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4|hello\nworld|hello\nworld|['name', 'note']|['beta', 'z']", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Reader_TreatsNonBmpDelimiterAndQuoteAsSinglePythonCharacters()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import csv

delimited = list(csv.reader(["alpha💬beta💬gamma"], delimiter="💬"))
quoted = list(csv.reader(["💬alpha💬💬beta💬,gamma"], quotechar="💬"))
__lython_file = open("/out.txt", "w")
__lython_file.write(repr(delimited) + "|" + repr(quoted))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[['alpha', 'beta', 'gamma']]|[['alpha💬beta', 'gamma']]", host.ReadText("/out.txt"));
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

__lython_file = open("/memory.csv", "w")
__lython_file.write(memory.getvalue())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("'a;b';'c'\n'1';'2.5';'True';''", host.ReadText("/memory.csv"));
        Assert.Equal("name\tqty\nalpha\t2\n", host.ReadText("/out.csv"));
    }

    [Fact]
    public void WriterMembers_AcceptPythonKeywordArgumentNames()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv

memory = csv.writer()
memory.writerow(row=["name", "qty"])
memory.writerows(rows=[["alpha", 2], ["beta", 3]])

with open("/dict.csv", "w", newline="") as handle:
    writer = csv.DictWriter(handle, fieldnames=["name", "qty"])
    writer.writeheader()
    writer.writerow(rowdict={"name": "gamma", "qty": 4})
    writer.writerows(rowdicts=[{"name": "delta", "qty": 5}])

__lython_file = open("/memory.csv", "w")
__lython_file.write(memory.getvalue())
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("name,qty\nalpha,2\nbeta,3", host.ReadText("/memory.csv"));
        Assert.Equal("name,qty\ngamma,4\ndelta,5\n", host.ReadText("/dict.csv"));
    }

    [Theory]
    [InlineData(
        """
import csv
writer = csv.writer()
method = getattr(writer, "writerow")
method()
""",
        "Method 'csv.writerow' is missing argument 'row'.")]
    [InlineData(
        """
import csv
writer = csv.writer()
method = getattr(writer, "writerow")
method(["a"], row=["b"])
""",
        "Method 'csv.writerow' got multiple values for argument 'row'.")]
    [InlineData(
        """
import csv
writer = csv.writer()
method = getattr(writer, "writerows")
method(row=[["a"]])
""",
        "Method 'csv.writerows' got an unexpected keyword argument 'row'.")]
    [InlineData(
        """
import csv
writer = csv.DictWriter(open("/out.csv", "w"), ["name"])
method = getattr(writer, "writerow")
method()
""",
        "Method 'csv.DictWriter.writerow' is missing argument 'rowdict'.")]
    [InlineData(
        """
import csv
writer = csv.DictWriter(open("/out.csv", "w"), ["name"])
method = getattr(writer, "writerow")
method({"name": "a"}, rowdict={"name": "b"})
""",
        "Method 'csv.DictWriter.writerow' got multiple values for argument 'rowdict'.")]
    [InlineData(
        """
import csv
writer = csv.DictWriter(open("/out.csv", "w"), ["name"])
method = getattr(writer, "writerows")
method(rowdict=[{"name": "a"}])
""",
        "Method 'csv.DictWriter.writerows' got an unexpected keyword argument 'rowdict'.")]
    public void WriterMembers_ReportRuntimeCallShapeErrors(string source, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure.ExceptionType);
        Assert.Contains(message, result.Failure.Message, StringComparison.Ordinal);
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

__lython_file = open("/status.txt", "w")
__lython_file.write(status)
__lython_file.close()
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
__lython_file = open("/out.txt", "w")
__lython_file.write(status)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("caught:Invalid csv input.", host.ReadText("/out.txt"));
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
