using System.Security.Cryptography;
using System.Text.Json;
using Lokad.Lython;
using Lokad.Lython.Runtime;

var span = new LythonSourceSpan(0, 0, 0, 0);
var host = new Host();
var root = new LythonMemoryBudget();
foreach (var text in new[] { false, true })
{
    for (var i = 0; i < 20; i++) Run();
    var before = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < 100; i++) Run();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Console.WriteLine(JsonSerializer.Serialize(new {
        Case = text ? "Whole-file UTF-8 text acquisition" : "Whole-file binary acquisition",
        InputBytes = host.Data.Length, Iterations = 100, AllocatedBytes = allocated,
        RootBalanceBytes = root.ReservedBytes,
        CoreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LythonEngine).Assembly.Location)))
    }));
    void Run()
    {
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner });
            if (text)
            {
                var value = LythonRuntime.ReadGovernedHostText("/source", context, span);
                if (!value.Utf8Bytes.Span.SequenceEqual(host.Data)) throw new Exception("Text content changed");
            }
            else
            {
                using var value = LythonRuntime.ReadGovernedHostBytes("/source", context, span);
                if (!value.Span.SequenceEqual(host.Data)) throw new Exception("Binary content changed");
            }
        }
        if (root.ReservedBytes != 0) throw new Exception("Owner not at baseline");
    }
}
sealed class Host:ILythonHost,ILythonSynchronousHostCapability
{
    public readonly byte[] Data=Enumerable.Repeat((byte)'x',65536).ToArray();
    public int Published;
    public bool CompletesSynchronously=>true;
    public string Cwd=>"/";
    public DateTimeOffset LocalNow=>DateTimeOffset.UnixEpoch;
    public DateTimeOffset UtcNow=>DateTimeOffset.UnixEpoch;
    private void Publish(ReadOnlySpan<byte> bytes)
    {
        if(!bytes.SequenceEqual(Data.AsSpan(Published,bytes.Length)))throw new Exception("Write content changed");
        Published+=bytes.Length;
    }
#if OWNED_FILES
    public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path,LythonFileOpenOptions options,ILythonMemoryBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var owner=budget.CreateScope();
        if(!owner.TryReserve(256,out _)){owner.Dispose();throw new LythonHostException(LythonHostFailureKind.MemoryAdmission,"file refused",requestedBytes:256);}
        return ValueTask.FromResult<ILythonByteFile>(new ByteFile(this,owner,options.Access==LythonFileAccess.Read));
    }
    private sealed class ByteFile(Host host,ILythonMemoryScope owner,bool read):ILythonByteFile,ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously=>true;
        public long Position{get;private set;}
        public long? LengthHint=>read?host.Data.Length:0;
        public ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();var count=Math.Min(buffer.Length,host.Data.Length-(int)Position);
            host.Data.AsMemory((int)Position,count).CopyTo(buffer);Position+=count;return ValueTask.FromResult(count);
        }
        public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();host.Publish(buffer.Span);Position+=buffer.Length;return ValueTask.FromResult(new LythonFileWriteResult(buffer.Length));
        }
        public ValueTask FlushAsync(CancellationToken token){token.ThrowIfCancellationRequested();return ValueTask.CompletedTask;}
        public ValueTask DisposeAsync(){owner.Dispose();return ValueTask.CompletedTask;}
    }
#endif
    public ValueTask<ReadOnlyMemory<byte>> ReadBytesRangeAsync(string path,long offset,int count,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();return ValueTask.FromResult<ReadOnlyMemory<byte>>(Data.AsMemory((int)offset,Math.Min(count,Data.Length-(int)offset)).ToArray());
    }
    public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();return ValueTask.FromResult<ReadOnlyMemory<byte>>(Data.ToArray());
    }
    public ValueTask WriteBytesAsync(string path,ReadOnlyMemory<byte> bytes,CancellationToken token){token.ThrowIfCancellationRequested();Publish(bytes.Span);return ValueTask.CompletedTask;}
    public ValueTask AppendBytesAsync(string path,ReadOnlyMemory<byte> bytes,CancellationToken token)=>WriteBytesAsync(path,bytes,token);
    public ValueTask<LythonPathStat> StatAsync(string path,CancellationToken token)=>ValueTask.FromResult(new LythonPathStat(path=="/source"?LythonPathKind.File:LythonPathKind.Missing,path=="/source"?Data.Length:0,null));
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path,CancellationToken token)=>ValueTask.FromResult<ReadOnlyMemory<byte>>(Data);
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8RangeAsync(string path,long offset,int count,CancellationToken token)=>ReadBytesRangeAsync(path,offset,count,token);
    public ValueTask WriteTextUtf8Async(string path,ReadOnlyMemory<byte> bytes,CancellationToken token)=>WriteBytesAsync(path,bytes,token);
    public ValueTask AppendTextUtf8Async(string path,ReadOnlyMemory<byte> bytes,CancellationToken token)=>AppendBytesAsync(path,bytes,token);
    public ValueTask<bool> ExistsAsync(string path,CancellationToken token)=>throw new NotSupportedException();
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path,CancellationToken token)=>throw new NotSupportedException();
    public ValueTask MkDirAsync(string path,CancellationToken token)=>throw new NotSupportedException();
    public ValueTask RemoveAsync(string path,CancellationToken token)=>throw new NotSupportedException();
    public ValueTask CopyAsync(string path,string target,CancellationToken token)=>throw new NotSupportedException();
    public ValueTask MoveAsync(string path,string target,CancellationToken token)=>throw new NotSupportedException();
}
