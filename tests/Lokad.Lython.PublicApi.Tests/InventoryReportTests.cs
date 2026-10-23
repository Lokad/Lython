using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class InventoryReportTests
{
    private const string Script = """
        import csv
        import re
        from json import dumps

        def clean_name(text, *, pattern=r"\s+"):
            return re.sub(pattern=pattern, repl=" ", string=text.strip())

        selected = []
        with open("inventory.tsv") as handle:
            # Materialize this small input before closing its single-pass reader.
            rows = list(csv.reader(handle, delimiter="\t"))
        for sku, name, qty in rows[1:]:
            if (count := int(qty)) > 0:
                selected.append({"sku": sku, "name": clean_name(name), "qty": count})
        selected = sorted(selected, key=lambda item: item["sku"])

        writer = csv.writer(lineterminator=chr(10), delimiter="\t")
        writer.writerow(["sku", "name", "qty"])
        writer.writerows([[item["sku"], item["name"], item["qty"]] for item in selected])

        with open("available.tsv", "w") as handle:
            handle.write(writer.getvalue())

        with open("available.json", "w") as handle:
            handle.write(dumps(obj=selected))
        """;

    [Fact]
    public void InventoryReportPublishesTsvAndJson()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(Script);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        host.SeedFile(
            "/inventory.tsv",
            "sku\tname\tqty\nb002\t  gizmo  \t0\na001\tWidget\t3\nc003\tSprocket \t12\n");

        using var result = script.Run(host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("sku\tname\tqty\na001\tWidget\t3\nc003\tSprocket\t12", host.ReadText("/available.tsv"));
        Assert.Equal(
            "[{\"sku\": \"a001\", \"name\": \"Widget\", \"qty\": 3}, {\"sku\": \"c003\", \"name\": \"Sprocket\", \"qty\": 12}]",
            host.ReadText("/available.json"));
    }
}
