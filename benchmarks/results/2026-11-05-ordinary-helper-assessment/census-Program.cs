using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
var core=Assembly.LoadFrom(Path.GetFullPath(args[0]));
var frontend=core.GetType("Lokad.Lython.Frontend.LythonFrontend",true)!;
var lower=core.GetType("Lokad.Lython.Frontend.LoweredScript",true)!.GetMethod("Lower")!;
var compile=core.GetType("Lokad.Lython.Frontend.ExecutableScript",true)!.GetMethod("Compile")!;
using var catalog=JsonDocument.Parse(File.ReadAllBytes(args[1]));
var records=new List<object>();
foreach(var item in catalog.RootElement.GetProperty("cases").EnumerateArray().DistinctBy(c=>c.GetProperty("id").GetString())) {
 var parsed=frontend.GetMethod("Compile")!.Invoke(null,[item.GetProperty("source").GetString(),null,null,default(CancellationToken)])!;
 var executable=compile.Invoke(null,[lower.Invoke(null,[Get(parsed,"Script")])])!;
 var codes=new List<object>();Visit(Get(executable,"EntryPoint")!);
 records.Add(new{id=item.GetProperty("id").GetString(),codes});
 void Visit(object code) {
  var instructions=((IEnumerable)Get(code,"Instructions")!).Cast<object>().ToArray();
  var body=Get(code,"NativeBody");
  var values=body is null?[]:((IEnumerable)Get(body,"Values")!).Cast<object>().ToArray();
  codes.Add(new{name=Get(code,"Name"),instructionCount=instructions.Length,nativeValueCount=values.Length,
   requiresMirroring=Get(code,"RequiresLocalVariableMirroring"),
   valueStorageCapacity=Get(code,"ValueStorageCapacity"),memberCacheCount=Get(code,"MemberCacheCount"),
   localCount=((IEnumerable)Get(code,"LocalNames")!).Cast<object>().Count(),
   nativeOperations=values.Select(n=>Get(n,"Operation")!.ToString()).ToArray(),
   sourceCoordinatesValid=values.All(n=>(uint)(int)Get(n,"SourceInstruction")!<(uint)instructions.Length)});
  foreach(var binding in (IEnumerable)Get(code,"Functions")!)Visit(Get(binding,"CodeObject")!);
  foreach(var value in (IEnumerable)Get(code,"Constants")!)
   if(value is not null && value.GetType().Name is "ExecutableCreateLambda" or "ExecutableCreateGeneratorExpression")Visit(Get(value,"CodeObject")!);
 }
}
Console.WriteLine(JsonSerializer.Serialize(new{coreSHA256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(args[0]))),
 purpose="Read-only compiler frame/native-body inventory, not guest allocation/CPU/elapsed/admission measurement",records},
 new JsonSerializerOptions{WriteIndented=true}));
static object? Get(object value,string name)=>value.GetType().GetProperty(name)!.GetValue(value);
