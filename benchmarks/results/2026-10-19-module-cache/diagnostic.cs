using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

var rows=new List<object>();
foreach(var kind in new[]{"script-construction","builtin-name-only","builtin-first-member","builtin-warm-member"})
{
    var budget=new LythonMemoryBudget(64*1024*1024);
    using var owner=budget.CreateScope();
    var context=new LythonRuntime.ExecutionContext(new MockLythonHost(),new LythonRunOptions{MemoryBudget=owner});
    var warm=new FreshModule();
    if(!warm.TryGetCachedMember("callable",out var expected))throw new Exception("Missing control");
    void Run()
    {
        if(kind=="script-construction")
        { GC.KeepAlive(new ScriptPyModule("sample",context,new(0,0,0,0))); return; }
        var module=kind=="builtin-warm-member"?warm:new FreshModule();
        if(!module.TryGetCachedMember(kind=="builtin-name-only"?"__name__":"callable",out var value))throw new Exception("Missing member");
        if(kind=="builtin-warm-member"&&!ReferenceEquals(value,expected))throw new Exception("Lost identity");
        GC.KeepAlive(module);
    }
    for(var i=0;i<16;i++)Run();
    var bytes=new long[8];var ticks=new long[8];
    for(var sample=0;sample<8;sample++)
    {
        var allocated=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
        for(var i=0;i<64;i++)Run();
        ticks[sample]=Stopwatch.GetTimestamp()-start;bytes[sample]=GC.GetAllocatedBytesForCurrentThread()-allocated;
    }
    owner.Dispose();if(budget.ReservedBytes!=0)throw new Exception("Owner cleanup failed");
    rows.Add(new{kind,iterations=64,bytes,ticks});
}
Console.WriteLine(JsonSerializer.Serialize(new{timestamp=DateTimeOffset.UtcNow,runtime=Environment.Version.ToString(),processorCount=Environment.ProcessorCount,
    frequency=Stopwatch.Frequency,assemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),rows}));
sealed class FreshModule():PyModule("sample")
{
    public override bool TryGetMember(string name,out object value)
    {value=new object();return name=="callable";}
}
