namespace Lokad.Lython.Tests.Harness;

// Test setup/inspection through the current public capability. The test owns
// its input/output arrays; this owner funds only host acquisition resources.
internal static class FixtureFiles
{
    internal static async ValueTask WriteAsync(ILythonHost host,string path,ReadOnlyMemory<byte> bytes,CancellationToken token,bool append=false)
    {
        using var owner=new LythonMemoryBudget().CreateScope();
        await using var file=await host.OpenFileAsync(new(path,host.Cwd),new(LythonFileAccess.Write,create:true,truncate:!append,append:append),owner,token);
        while(!bytes.IsEmpty)
        {
            var result=await file.WriteAsync(bytes,token);
            if(result.Failure is { } failure)throw failure;
            if(result.AcceptedBytes is not >0 || result.AcceptedBytes>bytes.Length)throw new IOException("Invalid fixture write count");
            bytes=bytes[result.AcceptedBytes.Value..];
        }
        await file.FlushAsync(token);
    }

    internal static async ValueTask<ReadOnlyMemory<byte>> ReadAsync(ILythonHost host,string path,CancellationToken token)
    {
        using var owner=new LythonMemoryBudget().CreateScope();
        await using var file=await host.OpenFileAsync(new(path,host.Cwd),new(LythonFileAccess.Read),owner,token);
        using var output=new MemoryStream();
        var buffer=new byte[4096];
        while(true)
        {
            var count=await file.ReadAsync(buffer,token);
            if(count==0)break;
            output.Write(buffer,0,count);
        }
        return output.ToArray();
    }
}
