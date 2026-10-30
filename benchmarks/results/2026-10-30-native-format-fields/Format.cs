using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

foreach(var (name,format,expectedSpec,named) in new[] {
 ("plain positional","{0}",(string?)null,false),
 ("small nested","{0:{1}}",">4",false),
 ("large nested","{0:{1}}",new string('x',4096),false),
 ("Unicode names/suffix","{é.😀[é]:{幅}}","😀>4",true) })
{
 var budget=new LythonMemoryBudget();
 using(var owner=budget.CreateScope())
 {
  var governor=new MemoryGovernor(owner);
  var template=PyString.FromString(format,governor);
  var output=PyString.FromString("ok",governor);
  var width=PyString.FromString(expectedSpec??"",governor);
  object[] positional=[new BigInteger(7),width];
  var keywords=named?new Dictionary<string,object>{{"é",new BigInteger(7)},{"幅",width}}:null;
  Func<object,string,object> suffix=(value,path)=>path==".😀[é]"?value:throw new Exception("Incorrect suffix");
  Func<object,char?,string?,PyString> value=(item,conversion,spec)=> {
   if(item is PyString text) return text;
   if(spec!=expectedSpec||conversion is not null) throw new Exception("Incorrect callback metadata");
   return output;
  };
  Check();for(var i=0;i<20;i++)Run();
  var retained=budget.ReservedBytes;
  var before=GC.GetAllocatedBytesForCurrentThread();
  for(var i=0;i<100;i++)Run();
  var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
  var delta=budget.ReservedBytes-retained;
  Check();
  Console.WriteLine(JsonSerializer.Serialize(new {Case=name,TemplateUtf8Bytes=template.Utf8Bytes.Length,ExpandedSpecUtf8Bytes=width.Utf8Bytes.Length,Formats=100,AllocatedBytes=allocated,ReservationDeltaBytes=delta,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LythonEngine).Assembly.Location)))}));
  PyString Run() {
#if GOVERNED_FORMATTER
   return PyStringOps.Format(template,positional,keywords,suffix,value,governor,null);
#else
   return PyStringOps.Format(template,positional,keywords,suffix,value);
#endif
  }
  void Check() {if(Run().AsString()!="ok")throw new Exception("Incorrect output");}
 }
 if(budget.ReservedBytes!=0)throw new Exception("Owner did not return to baseline");
}
