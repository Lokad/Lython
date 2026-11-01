using System.Reflection;
using System.Collections;
using System.Runtime.Loader;
using System.Text.Json;
var path = Path.GetFullPath(args[0]);
var directory = Path.GetDirectoryName(path)!;
AssemblyLoadContext.Default.Resolving += (_, n) => File.Exists(Path.Combine(directory,n.Name+".dll")) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory,n.Name+".dll")) : null;
var core=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
var type=core.GetType("Lokad.Lython.Frontend.PythonTokenization") ?? core.GetType("Lokad.Lython.Frontend.FormattedStringTokenization",true)!;
var read=type.GetMethod("Read",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!;
var engine=Activator.CreateInstance(core.GetType("Lokad.Lython.LythonEngine",true)!)!;
var compile=engine.GetType().GetMethod("Compile",[typeof(string)])!;
var sources=JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]))!;
var rows=new List<object>();
foreach(var source in sources)
{
 var parameters=read.GetParameters().Select(p=>p.HasDefaultValue?p.DefaultValue:null).ToArray();parameters[0]=source;
 var tokens=read.Invoke(null,parameters)!;
 object Prop(object value,string name)=>value.GetType().GetProperty(name)!.GetValue(value)!;
 var entries=((IEnumerable)Prop(tokens,"Tokens")).Cast<object>().Select(t=>new { token=Prop(t,"Token").ToString(),start=Prop(t,"Start"),length=Prop(t,"Length") }).ToArray();
 using var script=(IDisposable)compile.Invoke(engine,[source])!;
 rows.Add(new{source,entries,invalid=Prop(tokens,"HasInvalidTokens"),diagnostics=Prop(script,"Diagnostics")});
}
Console.WriteLine(JsonSerializer.Serialize(rows));
