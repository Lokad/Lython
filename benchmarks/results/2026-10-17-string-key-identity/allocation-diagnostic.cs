using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

// Temporary native evidence; remove before delivery.
public sealed class StringKeyIdentityDiagnostic
{
    [Fact]
    public void Capture()
    {
        const int iterations = 65536;
        var same = PyString.FromString("café😀");
        object nan = double.NaN;
        var pairs = new (string Name, object? Left, object? Right, bool Expected)[]
        {
            ("identity", same, same, true),
            ("empty", PyString.Empty, PyString.Empty, true),
            ("ascii", PyString.FromString("amount"), PyString.FromString("amount"), true),
            ("unicode", same, PyString.FromString("café😀"), true),
            ("long", PyString.FromString(new string('x',4096)), PyString.FromString(new string('x',4096)), true),
            ("unequal", same, PyString.FromString("cafe😀"), false),
            ("mixed", same, new BigInteger(1), false),
            ("integer-float", new BigInteger(1), 1.0, true),
            ("bool-integer", true, new BigInteger(1), true),
            ("nan-identity", nan, nan, true),
            ("tuple", new PyTuple([same, new BigInteger(1)]), new PyTuple([PyString.FromString("café😀"),1.0]), true),
            ("null", null, null, false),
        };
        var comparer = PyValueComparer.Instance;
        foreach (var p in pairs)
            for (var i=0;i<16384;i++) Assert.Equal(p.Expected,comparer.Equals(p.Left,p.Right));
        var rows=new List<object>();
        for (var round=0;round<7;round++)
        {
            for(var offset=0;offset<pairs.Length;offset++)
            {
                var p=pairs[(round+offset)%pairs.Length];
                var matches=0;
                var allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
                var start=Stopwatch.GetTimestamp();
                for(var i=0;i<iterations;i++) if(comparer.Equals(p.Left,p.Right)) matches++;
                var ticks=Stopwatch.GetTimestamp()-start;
                var bytes=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
                Assert.Equal(p.Expected?iterations:0,matches);
                rows.Add(new { round,name=p.Name,iterations,elapsedTicks=ticks,allocatedBytes=bytes,matches });
            }
        }
        var output=Environment.GetEnvironmentVariable("LYTHON_STRING_KEY_OUTPUT") ?? throw new InvalidOperationException("Diagnostic output required");
        File.WriteAllText(output,JsonSerializer.Serialize(new { frequency=Stopwatch.Frequency,rows },new JsonSerializerOptions { WriteIndented=true }));
    }
}
