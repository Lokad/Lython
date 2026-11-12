using System.Collections;
using System.Reflection;
using System.Text.Json;
using Lokad.Lython;
var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
using var catalog=JsonDocument.Parse(File.ReadAllBytes(args[0]));
var reports=new List<object>();
foreach(var item in catalog.RootElement.GetProperty("cases").EnumerateArray()) {
 using var script=new LythonEngine().Compile(item.GetProperty("source").GetString()!);
 if(!script.IsValid)throw new Exception(JsonSerializer.Serialize(script.Diagnostics));
 var runner=(Delegate)typeof(LythonCompiledScript).GetField("_runner",flags)!.GetValue(script)!;
 var executable=runner.Target!.GetType().GetFields(flags).Single(f=>f.FieldType.Name=="ExecutableScript").GetValue(runner.Target)!;
 var codes=new List<object>(); Visit(Get(executable,"EntryPoint")!,codes);
 reports.Add(new{caseId=item.GetProperty("id").GetString(),codes});
}
Console.WriteLine(JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
object? Get(object v,string name)=>v.GetType().GetProperty(name,flags)!.GetValue(v);
void Visit(object code,List<object> codes) {
 codes.Add(new{name=Get(code,"Name"),mirrors=Get(code,"RequiresLocalVariableMirroring"),captures=((IEnumerable)Get(code,"CapturedLocalSlots")!).Cast<object>().ToArray(),closures=((IEnumerable)Get(code,"ClosureNames")!).Cast<object>().ToArray(),instructions=((Array)Get(code,"Instructions")!).Length,opcodes=((IEnumerable)Get(code,"Instructions")!).Cast<object>().Select(i=>Get(i,"OpCode")!.ToString()).ToArray(),exceptionRegions=((IEnumerable)Get(code,"ExceptionRegions")!).Cast<object>().Count(),native=Get(code,"NativeBody") is not null,nativeLoops=((IEnumerable)Get(code,"Instructions")!).Cast<object>().Count(i=>Get(i,"OpCode")!.ToString()=="ForNextDictionaryAccumulation"),locals=((IEnumerable)Get(code,"LocalNames")!).Cast<object>().ToArray()});
 foreach(var function in (IEnumerable)Get(code,"Functions")!)Visit(Get(function,"CodeObject")!,codes);
}
