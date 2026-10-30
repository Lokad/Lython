using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Utf8Regex;

foreach (var (name,text,expected) in new[] {
 ("ASCII", new string('a',4096),"a"),
 ("BMP/supplementary",string.Concat(Enumerable.Repeat("é😀",2048)),"é") })
{
 var budget=new LythonMemoryBudget();
 using(var owner=budget.CreateScope())
 {
  var context=new LythonRuntime.ExecutionContext(new PureProbeHost(),new LythonRunOptions {MemoryBudget=owner},regexProvider:Utf8RegexProvider.Instance);
  try
  {
   var span=new LythonSourceSpan(0,0,0,0);
   var pattern=LythonRuntime.RegexCompiler.CreatePattern([PyString.FromString(".")],"re.compile",span,context);
   if(!LythonRuntime.RePatternMembers.TryGetMember(pattern,"search",out var callable)) throw new Exception("Missing search");
   var input=PyString.FromString(text,context.MemoryGovernor);
   CallArgumentValue[] arguments=[CallArgumentValue.Positional(input),CallArgumentValue.Positional(new BigInteger(1024)),CallArgumentValue.Positional(new BigInteger(3072))];
   for(var i=0;i<20;i++) Check();
   var before=GC.GetAllocatedBytesForCurrentThread();
   for(var i=0;i<100;i++) Run();
   var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
   Check();
   Console.WriteLine(JsonSerializer.Serialize(new {Case=name,SubjectUtf8Bytes=input.Utf8Bytes.Length,SubjectScalars=input.Length,Pos=1024,EndPos=3072,Searches=100,AllocatedBytes=allocated,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LythonEngine).Assembly.Location))),AdapterSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Utf8RegexProvider).Assembly.Location)))}));
   LythonRuntime.ReMatchObject Run() => (LythonRuntime.ReMatchObject)((LythonRuntime.ICallable)callable).Invoke(arguments,span,context);
   void Check() {var m=Run(); if(m.Start!=1024||m.End!=1025||m.Value.AsString()!=expected||m.String.AsString()!=text) throw new Exception("Incorrect match");}
  }
  finally {context.State.CloseResources();}
 }
 if(budget.ReservedBytes!=0) throw new Exception("Ownership did not return to baseline");
}

sealed class PureProbeHost : ILythonHost
{
    public string Cwd => "/";

    public DateTimeOffset LocalNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(1));

    public DateTimeOffset UtcNow => new(2024, 1, 2, 2, 4, 5, TimeSpan.Zero);

    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken) =>
        throw Unsupported($"read files: {path}");

    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
        throw Unsupported($"write files: {path}");

    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
        throw Unsupported($"append files: {path}");

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(false);

    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken) =>
        throw Unsupported($"list directories: {path}");

    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken) =>
        throw Unsupported($"create directories: {path}");

    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken) =>
        throw Unsupported($"remove paths: {path}");

    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken) =>
        throw Unsupported($"copy paths: {source} -> {destination}");

    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken) =>
        throw Unsupported($"move paths: {source} -> {destination}");

    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new LythonPathStat(
            kind: LythonPathKind.Missing,
            size: 0,
            modifiedAt: null));

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Pure probe host cannot {operation}.");
}
