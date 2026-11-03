using System.Diagnostics;
using System.Text.Json;
var root=Path.Combine(Path.GetTempPath(),"lython-comparison-files-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var stage=Path.Combine(root,"job");var data=new byte[42010];Array.Fill(data,(byte)'x');
var rows=new List<object>();
try {
 long deleteTicks=0,createTicks=0,writeTicks=0;
 foreach(var mode in new[]{"WriteAllBytes","FileStream"}) {
  void Reset() {
   var t=Stopwatch.GetTimestamp();
   if(Directory.Exists(stage))Directory.Delete(stage,true);
   deleteTicks+=Stopwatch.GetTimestamp()-t;t=Stopwatch.GetTimestamp();
   Directory.CreateDirectory(stage);
   createTicks+=Stopwatch.GetTimestamp()-t;t=Stopwatch.GetTimestamp();
   var file=Path.Combine(stage,"input.txt");
   if(mode=="WriteAllBytes")File.WriteAllBytes(file,data);
   else { using var stream=new FileStream(file,FileMode.Create,FileAccess.Write,FileShare.Read);stream.Write(data); }
   writeTicks+=Stopwatch.GetTimestamp()-t;
  }
  if(args.Contains("trace")){Console.Error.WriteLine(mode);Reset();continue;}
  for(var i=0;i<128;i++)Reset();
  for(var rnd=0;rnd<9;rnd++) {
   deleteTicks=createTicks=writeTicks=0;
   var cpu=Process.GetCurrentProcess().TotalProcessorTime;var clock=Stopwatch.StartNew();
   for(var i=0;i<64;i++)Reset();clock.Stop();
   rows.Add(new{mode,round=rnd,iterations=64,elapsedMilliseconds=clock.Elapsed.TotalMilliseconds,deleteMilliseconds=deleteTicks*1000.0/Stopwatch.Frequency,createMilliseconds=createTicks*1000.0/Stopwatch.Frequency,writeMilliseconds=writeTicks*1000.0/Stopwatch.Frequency,cpuMilliseconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalMilliseconds});
   if(!File.ReadAllBytes(Path.Combine(stage,"input.txt")).AsSpan().SequenceEqual(data))throw new InvalidDataException();
  }
 }
} finally { if(Path.GetDirectoryName(root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath()))throw new InvalidDataException();Directory.Delete(root,true); }
Console.WriteLine(JsonSerializer.Serialize(new{rows,fixturesRemoved=true,bytesVerified=true}));
