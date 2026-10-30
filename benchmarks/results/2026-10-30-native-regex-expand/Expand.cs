using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using Lokad.Lython;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Utf8Regex;

foreach (var (name,input,template,expected) in new[] {
 ("ASCII literal", "x",new string('a',4096),new string('a',4096)),
 ("supplementary capture", string.Concat(Enumerable.Repeat("😀",256)),string.Concat(Enumerable.Repeat(@"[\g<name>]",8)),string.Concat(Enumerable.Repeat("["+string.Concat(Enumerable.Repeat("😀",256))+"]",8))) })
{
 var budget=new LythonMemoryBudget();
 using (var owner=budget.CreateScope())
 {
  var context=new LythonRuntime.ExecutionContext(new PureProbeHost(),new LythonRunOptions { MemoryBudget=owner },regexProvider:Utf8RegexProvider.Instance);
  var span=new LythonSourceSpan(0,0,0,0);
  var pattern=LythonRuntime.RegexCompiler.CreatePattern([PyString.FromString("(?P<name>.+)")],"re.compile",span,context);
  if(!LythonRuntime.RePatternMembers.TryGetMember(pattern,"match",out var callable)) throw new Exception("Missing match");
  var match=(LythonRuntime.ReMatchObject)((LythonRuntime.ICallable)callable).Invoke([CallArgumentValue.Positional(PyString.FromString(input))],span,context);
  var text=PyString.FromString(template);
  for(var i=0;i<20;i++) Check();
  var before=GC.GetAllocatedBytesForCurrentThread();
  for (var i=0;i<100;i++) Expand();
  var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
  Check();
  Console.WriteLine(JsonSerializer.Serialize(new { Case=name,InputUtf8Bytes=match.Value.Utf8Bytes.Length,TemplateUtf8Bytes=text.Utf8Bytes.Length,Expansions=100,AllocatedBytes=allocated,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LythonEngine).Assembly.Location))) }));
  PyString Expand()=>LythonRuntime.ReMatchMembers.ExpandReplacementTemplate(match,text,context,span);
  void Check() { if(Expand().AsString()!=expected) throw new Exception("Incorrect expansion"); }
 }
 if(budget.ReservedBytes!=0) throw new Exception("Scope did not return to baseline");
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
