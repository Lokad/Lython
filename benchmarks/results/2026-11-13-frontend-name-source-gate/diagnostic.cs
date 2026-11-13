using Lokad.Lython;
using Lokad.Lython.Frontend;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using var catalog=JsonDocument.Parse(File.ReadAllText(args[0],Encoding.UTF8));
var wanted=new[]{"scripts.csv-invoice.large","scripts.report-render.large","scripts.text-summary-helpers.nonlocal-counter.large","scripts.json-summary-helpers.large","scripts.file-search.large"};
var sources=catalog.RootElement.GetProperty("cases").EnumerateArray()
    .Where(c=>wanted.Contains(c.GetProperty("id").GetString()))
    .Select(c=>(Id:c.GetProperty("id").GetString()!,Source:c.GetProperty("source").GetString()!)).ToList();
sources.Add(("scripts.config-edit.source",File.ReadAllText("benchmarks/scripts/config-edit.py",Encoding.UTF8)));
var engine=new LythonEngine();
var budget=new LythonMemoryBudget(16*1024*1024);
var rows=new List<object>();
foreach(var (id,rawSource)in sources)
{
    var source=rawSource.Replace("\r\n","\n").Replace('\r','\n');
    var tokens=PythonTokenization.Read(source);
    var names=tokens.Tokens.Where(t=>t.Token==Token.Identifier).Select(t=>source.Substring(t.Start,t.Length).Normalize(NormalizationForm.FormKC)).ToArray();
    long Size(string s)=>(24L+2L*s.Length+7)&~7L;
    var allNames=names.Sum(Size);var uniqueNames=names.Distinct(StringComparer.Ordinal).Sum(Size);
    void Parse(){var p=new Parser(tokens).Parse();if(p.Script is null)throw new InvalidDataException(string.Join(";",p.Diagnostics));}
    void Compile(){using var p=engine.Compile(source,budget);if(!p.IsValid)throw new InvalidDataException(string.Join(";",p.Diagnostics));}
    for(var n=0;n<128;n++){Parse();Compile();}
    long Measure(Action action){var start=GC.GetAllocatedBytesForCurrentThread();for(var n=0;n<256;n++)action();return(GC.GetAllocatedBytesForCurrentThread()-start)/256;}
    var parseBytes=Measure(Parse);var compileBytes=Measure(Compile);
    if(budget.ReservedBytes!=0)throw new InvalidDataException("Compiled ownership did not return to baseline.");
    rows.Add(new{id,sourceSHA256=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source))),sourceLength=source.Length,identifierOccurrences=names.Length,uniqueNormalizedSpellings=names.Distinct(StringComparer.Ordinal).Count(),modeledAllIdentifierStringBytes=allNames,modeledDistinctIdentifierStringBytes=uniqueNames,modeledDuplicateStringBytes=allNames-uniqueNames,parserBytes=parseBytes,wholeCompileAndCleanupBytes=compileBytes,modelPercentOfCompile=100d*(allNames-uniqueNames)/compileBytes,returnedSharedAllowanceBytes=budget.ReservedBytes});
}
Console.WriteLine(JsonSerializer.Serialize(new{purpose="R312 frontend source/allocation gate; token-based model is not measured name-allocation traffic or CPU; parser reuses token storage, full compile includes tokens/analyses/lowering/executable and owner cleanup; no production changes",coreSHA256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(LythonEngine).Assembly.Location))),rows},new JsonSerializerOptions{WriteIndented=true}));
