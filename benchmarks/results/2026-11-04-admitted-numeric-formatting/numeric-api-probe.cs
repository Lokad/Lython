using System.Numerics;
using System.Text.Json;
using System.Diagnostics;
var numeric = typeof(BigInteger);
var mapping=numeric.GetInterfaceMap(typeof(IUtf8SpanFormattable));
Span<byte> bytes=stackalloc byte[128];
var value = new BigInteger(123456);
for(int i=0;i<256;i++) Format(value,bytes,out _);
var start=GC.GetAllocatedBytesForCurrentThread();
for(int i=0;i<4096;i++) { if(!Format(value,bytes,out var n)||n!=6) throw new Exception("format"); }
var allocated=GC.GetAllocatedBytesForCurrentThread()-start;
Console.WriteLine(JsonSerializer.Serialize(new {framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
 target=mapping.TargetMethods.Select(m=>new{type=m.DeclaringType!.FullName,m.Name}).ToArray(),allocated,iterations=4096,
 output=System.Text.Encoding.UTF8.GetString(bytes[..6])}));
static bool Format<T>(T value,Span<byte> output,out int count) where T:IUtf8SpanFormattable => value.TryFormat(output,out count,"D",System.Globalization.CultureInfo.InvariantCulture);
