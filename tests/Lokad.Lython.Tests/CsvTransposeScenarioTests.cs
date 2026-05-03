using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvTransposeScenarioTests
{
    [Fact]
    public void SwapColumnsFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "Csv", "SwapColumns"));
        var host = new MockLythonHost();

        foreach (var file in fixture.InputFiles)
        {
            host.SeedFile(file.Key, file.Value);
        }

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public void InvalidQuotedRow_FailsWithValueError()
    {
        var result = new LythonEngine().Run(
            """
import csv
rows = csv.reader(["\"broken"])
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
        Assert.Contains("Invalid csv input", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Writerows_RendersQuotedAndScalarFields()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv
writer = csv.writer()
writer.writerow(["name", "note"])
writer.writerow([True, None])
writer.writerows([["alpha", "x,y"], ["beta", "plain"]])
write_text("/out.csv", writer.getvalue())
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal(
            "name,note\nTrue,\nalpha,\"x,y\"\nbeta,plain",
            host.ReadText("/out.csv"));
    }

    [Fact]
    public void Writerows_WithNonIterableArgument_FailsWithTypeError()
    {
        var result = new LythonEngine().Run(
            """
import csv
writer = csv.writer()
writer.writerows(None)
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
    }

    [Fact]
    public void Writerow_WithNonScalarCell_FailsWithTypeError()
    {
        var result = new LythonEngine().Run(
            """
import csv
writer = csv.writer()
writer.writerow([["nested"]])
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("CSV rows must contain scalar values", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reader_ParsesQuotedCommasAndEscapedQuotes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv
rows = csv.reader(["\"a,b\",\"c\"\"d\"", "plain,tail"])
write_text("/out.txt", rows[0][0] + "|" + rows[0][1] + "|" + rows[1][0] + "|" + rows[1][1])
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("a,b|c\"d|plain|tail", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Writerow_QuotesEmbeddedCommasQuotesAndNewlines()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv
writer = csv.writer()
writer.writerow(["a,b", "c\"d", "line1\nline2", None])
write_text("/out.csv", writer.getvalue())
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal(
            "\"a,b\",\"c\"\"d\",\"line1\nline2\",",
            host.ReadText("/out.csv"));
    }

    [Fact]
    public void Writerow_RendersEmptyStringAndNoneAsEmptyFields()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv
writer = csv.writer()
writer.writerow(["", None, "x"])
write_text("/out.csv", writer.getvalue())
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal(",,x", host.ReadText("/out.csv"));
    }

    [Fact]
    public void Reader_PreservesEmptyFields()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv
rows = csv.reader([",,\"\",tail"])
write_text("/out.txt", str(rows[0]))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("[, , , tail]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void ReaderAndWriter_SupportTabDelimitedData()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv
rows = csv.reader(["name\tvalue", "alpha\t1"], delimiter = "\t")
writer = csv.writer(delimiter = "\t")
writer.writerows(rows)
write_text("/out.tsv", writer.getvalue())
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("name\tvalue\nalpha\t1", host.ReadText("/out.tsv"));
    }

    [Fact]
    public void ReaderAndWriter_PreserveUnicodeFields()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv
rows = csv.reader(["😀,é", "\"é,😀\",ok"])
writer = csv.writer()
writer.writerows(rows)
write_text("/out.csv", writer.getvalue())
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("😀,é\n\"é,😀\",ok", host.ReadText("/out.csv"));
    }
}
