using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
var assemblyPath=Path.GetFullPath(args[0]);
var directory=Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) => { var path=Path.Combine(directory,name.Name+".dll");return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null; };
var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var type=assembly.GetType("Lokad.Lython.Benchmarks.RegexScanBenchmarks",true)!;
var benchmark=Activator.CreateInstance(type)!;
type.GetProperty("MatchCount")!.SetValue(benchmark,100);
type.GetMethod("Compile")!.Invoke(benchmark,null);
var fields=BindingFlags.Instance|BindingFlags.NonPublic;
var scripts=(IDictionary)type.GetField("_scripts",fields)!.GetValue(benchmark)!;
var script=scripts["reuse"]!;
var host=type.GetField("_host",fields)!.GetValue(benchmark)!;
var run=script.GetType().GetMethods().Single(m=>m.Name=="RunAsync" && m.GetParameters().Length==1);
async Task Once()
{
 var pending=(Task)run.Invoke(script,[host])!;await pending;
 var result=pending.GetType().GetProperty("Result")!.GetValue(pending)!;
 using((IDisposable)result)
 {
  if(!(bool)result.GetType().GetProperty("Success")!.GetValue(result)! || result.GetType().GetProperty("ReturnValue")!.GetValue(result)?.ToString()!="100")
   throw new Exception("Regex benchmark golden failed");
 }
}
try
{
 for(var i=0;i<4;i++)await Once();
 var times=new List<double>();var allocations=new List<double>();
 for(var round=0;round<7;round++)
 {
  var before=GC.GetTotalAllocatedBytes(true);var timer=Stopwatch.StartNew();
  for(var i=0;i<5;i++)await Once();
  timer.Stop();times.Add(timer.Elapsed.TotalMilliseconds/5);allocations.Add((GC.GetTotalAllocatedBytes(true)-before)/5.0);
 }
 double Median(List<double> values)=>values.Order().ElementAt(values.Count/2);
 var files=Directory.EnumerateFiles(directory,"Lokad.*.dll").ToDictionary(p=>Path.GetFileName(p)!,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
 Console.WriteLine(JsonSerializer.Serialize(new {revision=args[1],benchmark="RegexScanBenchmarks.ReusePatternAsync",matchCount=100,boundary="warm public RunAsync including result disposal; compilation/startup excluded; reflection overhead common",rounds=7,batch=5,warmups=4,milliseconds=times,allocatedBytes=allocations,medianMilliseconds=Median(times),medianAllocatedBytes=Median(allocations),runtime=Environment.Version.ToString(),os=System.Runtime.InteropServices.RuntimeInformation.OSDescription,processors=Environment.ProcessorCount,files,performanceQualified=false}));
}
finally {foreach(var compiled in scripts.Values)((IDisposable)compiled).Dispose();}
