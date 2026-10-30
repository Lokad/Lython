using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// OpenPyXL persistence scaling benchmarks: workbook creation plus
/// save, and load plus cell reads, over a mid-size worksheet.
/// </summary>
[MemoryDiagnoser]
public class OpenPyxlPersistenceBenchmarks
{
    private readonly LythonCompiledScript _createAndSave;
    private readonly LythonCompiledScript _loadAndRead;
    private readonly byte[] _sourceWorkbook;

    public OpenPyxlPersistenceBenchmarks()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        _createAndSave = Compile(engine, CreateAndSaveSource);
        _loadAndRead = Compile(engine, LoadAndReadSource);
        var seed = new ZipBinaryHost();
        using var built = new LythonEngine(Utf8RegexProvider.Instance).Run(CreateAndSaveSource, seed);
        if (!built.Success)
        {
            throw new InvalidOperationException(built.Failure?.Message ?? "Workbook benchmark seed failed.");
        }

        _sourceWorkbook = seed.ReadBytes("/book.xlsx");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _createAndSave?.Dispose();
        _loadAndRead?.Dispose();
    }

    [Benchmark(Description = "Create and save a 500-row workbook")]
    public void CreateAndSaveWorkbook() => Run(_createAndSave, new ZipBinaryHost());

    [Benchmark(Description = "Load a workbook and read cells")]
    public void LoadAndReadWorkbook()
    {
        var host = new ZipBinaryHost();
        host.SeedBytes("/book.xlsx", _sourceWorkbook);
        Run(_loadAndRead, host);
    }

    private const string CreateAndSaveSource = """
        from openpyxl import Workbook
        book = Workbook()
        sheet = book.active
        for row in range(500):
            sheet.append([row, row + 1, "name-" + str(row), row * 2.5])
        book.save("/book.xlsx")
        return sheet.max_row
        """;

    private const string LoadAndReadSource = """
        from openpyxl import load_workbook
        book = load_workbook("/book.xlsx")
        sheet = book.active
        total = 0
        for row in sheet.iter_rows(values_only=True):
            total = total + row[0]
        return total
        """;

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
        => BenchmarkScripts.Compile(engine, source);

    private static void Run(LythonCompiledScript script, ZipBinaryHost host)
        => BenchmarkScripts.Run(script, host);

}
