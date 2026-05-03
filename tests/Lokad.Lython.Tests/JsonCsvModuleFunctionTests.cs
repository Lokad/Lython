using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class JsonCsvModuleFunctionTests
{
    [Fact]
    public void JsonModule_LoadsAndDumps_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json

value = json.loads(" {\"ok\": true, \"count\": 2, \"items\": [1, null, \"x\"]} ")
text = json.dumps({"ok": value["ok"], "count": value["count"], "items": value["items"]})
write_text("/out.txt", text + "|" + str(value["items"]))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{\"ok\":true,\"count\":2,\"items\":[1,null,\"x\"]}|[1, None, x]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void CsvModule_ReaderAndWriterHelpers_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import csv

writer = csv.writer(delimiter=";")
writer.writerow(["name", "note"])
writer.writerow([True, None])
writer.writerows([["alpha", "x,y"], ["beta", "plain"]])
rows = csv.reader(writer.getvalue().splitlines(), delimiter=";")
vals = []
vals.append(writer.getvalue())
vals.append(str(rows))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("name;note\nTrue;\nalpha;x,y\nbeta;plain|[[name, note], [True, ], [alpha, x,y], [beta, plain]]", host.ReadText("/out.txt"));
    }
}
