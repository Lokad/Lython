using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

var samples = new List<object>();
foreach (var path in args)
{
    var load = new AssemblyLoadContext(Path.GetFileName(Path.GetDirectoryName(path)), isCollectible: true);
    var asm = load.LoadFromAssemblyPath(Path.GetFullPath(path));
    var type = asm.GetType("Lokad.Lython.Runtime.PyDict", true)!;
    foreach (var entries in new[] { 0, 2 })
    {
        var variable = Expression.Variable(type);
        var body = new List<Expression> { Expression.Assign(variable, Expression.New(type.GetConstructor(Type.EmptyTypes)!)) };
        var setter = type.GetMethod("SetItem", [typeof(object), typeof(object)])!;
        for (var i = 0; i < entries; i++)
            body.Add(Expression.Call(variable, setter, Expression.Constant((object)new BigInteger(i), typeof(object)), Expression.Constant((object)new BigInteger(i + 1), typeof(object))));
        body.Add(Expression.Convert(variable, typeof(object)));
        var create = Expression.Lambda<Func<object>>(Expression.Block([variable], body)).Compile();
        for (var i = 0; i < 256; i++) GC.KeepAlive(create());
        var held = new object[4096];
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < held.Length; i++) held[i] = create();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        samples.Add(new { coreSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))), entries,
            invocations = held.Length, allocatedBytes = allocated, allocatedBytesPerDictionary = (double)allocated / held.Length });
        GC.KeepAlive(held);
    }
    load.Unload();
}
Console.WriteLine(JsonSerializer.Serialize(new { purpose = "Local constructor allocation diagnostic only; no timing or whole-script attribution; held results, no forced GC", framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, samples }));
