using System.Text.Json;
using System.Security.Cryptography;
using Lokad.Lython;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

foreach (var (name,text) in new[] { ("ASCII", new string('a',4096)), ("supplementary",string.Concat(Enumerable.Repeat("😀",1024))) })
{
 var budget=new LythonMemoryBudget();
 var provider=new CountingProvider();
 using (var owner=budget.CreateScope())
 {
  var context=new LythonRuntime.ExecutionContext(new PureProbeHost(),new LythonRunOptions { MemoryBudget=owner },regexProvider:provider);
  object[] arguments=[PyString.FromString(text)];
  var span=new LythonSourceSpan(0,0,0,0);
  var first=LythonRuntime.RegexCompiler.CreatePattern(arguments,"re.compile",span,context);
  for (var i=0;i<200;i++) Hit();
  var before=GC.GetAllocatedBytesForCurrentThread();
  for (var i=0;i<1000;i++) Hit();
  var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
  if(provider.Count!=1) throw new Exception("Cache did not reuse compilation");
  Console.WriteLine(JsonSerializer.Serialize(new { Subject=name,Utf8Bytes=((PyString)arguments[0]).Utf8Bytes.Length,Hits=1000,AllocatedBytes=allocated,Compilations=provider.Count,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LythonEngine).Assembly.Location))) }));
  void Hit() { if (!ReferenceEquals(first,LythonRuntime.RegexCompiler.CreatePattern(arguments,"re.compile",span,context))) throw new Exception("Cache identity changed"); }
 }
 if(budget.ReservedBytes!=0) throw new Exception("Scope did not return to baseline");
}
sealed class CountingProvider : ILythonRegexProvider
{
 public int Count;
 public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern,LythonRegexOptions options,ILythonMemoryBudget memoryOwner,CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested();Count++;return new Pattern(); }
 private sealed class Pattern : ILythonRegexPattern
 {
  public int Flags=>32;
  public int CaptureSlotCount=>1;
  public IReadOnlyDictionary<string,int> NamedGroups { get; }=new Dictionary<string,int>();
  public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> subject,ILythonMemoryBudget memoryOwner,CancellationToken cancellationToken)=>throw new NotSupportedException();
 }
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
