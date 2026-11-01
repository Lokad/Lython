using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Lokad.Lython;
const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
object? Get(object owner,string name)=>owner.GetType().GetProperty(name,flags)!.GetValue(owner);
using var catalog=JsonDocument.Parse(File.ReadAllBytes(args[0]));
var result=new List<object>();
foreach(var item in catalog.RootElement.GetProperty("cases").EnumerateArray())
{
 using var script=new LythonEngine().Compile(item.GetProperty("source").GetString()!);
 if(!script.IsValid)throw new InvalidDataException("IR proof needs valid source");
 var runner=(Delegate)typeof(LythonCompiledScript).GetField("_runner",flags)!.GetValue(script)!;
 var executable=runner.Target!.GetType().GetField("executableScript",flags)!.GetValue(runner.Target)!;
 var code=Get(executable,"EntryPoint")!;
 var constants=((IEnumerable)Get(code,"Constants")!).Cast<object?>().ToArray();
 var locals=((IEnumerable)Get(code,"LocalNames")!).Cast<object>().Select(x=>x.ToString()).ToArray();
 var sequences=new List<object>();var count=0;
 foreach(var block in (IEnumerable)Get(code,"Blocks")!)
 {
  var instructions=((IEnumerable)Get(block,"Instructions")!).Cast<object>().ToArray();count+=instructions.Length;
  for(var i=2;i+1<instructions.Length;i++)
  {
   if(Get(instructions[i],"OpCode")!.ToString()!="Augmented")continue;
   var sequence=instructions.Skip(i-2).Take(4).Select(instruction=>{
    var op=Get(instruction,"OpCode")!.ToString();
    var slot=op is "LoadLocal" or "StoreLocal" ? (int?)Get(instruction,"LocalSlot")! : null;
    var ci=op=="LoadConst" ? (int)Get(instruction,"ConstantIndex")! : -1;
    return new {op,slot,local=slot is int localIndex?locals[localIndex]:null,
     constant=op=="LoadConst"&&constants[ci] is BigInteger number?number.ToString():null};
   }).ToArray();
   sequences.Add(sequence);
  }
 }
 result.Add(new {id=item.GetProperty("id").GetString(),sourceSha256=item.GetProperty("sourceSha256").GetString(),locals,instructionCount=count,augmentedSequences=sequences});
}
Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
