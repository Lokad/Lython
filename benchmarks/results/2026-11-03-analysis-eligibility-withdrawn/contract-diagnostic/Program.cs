using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
var cases = new Dictionary<string,string> {
 ["protected-assignment"] = "import re\ntry: p=re.compile('a')\nexcept Exception: pass",
 ["dead-assignment"] = "import re\nif False: p=re.compile('a')",
 ["deferred-global"] = "import re\ndef f():\n global p\n p=re.compile('a')",
 ["invoked-global"] = "import re\ndef f():\n global p\n p=re.compile('a')\nf()",
 ["ordinary-deferred"] = "import re\ndef f():\n p=re.compile('a')",
 ["lambda"] = "import re\np=lambda: re.compile('a')",
 ["generator"] = "import re\np=(re.compile('a') for i in [1])",
 ["dead-scope"] = "if False:\n nonlocal missing",
};
var result=new List<object>();
foreach(var path in args) {
 var load=new AssemblyLoadContext(path,true);var asm=load.LoadFromAssemblyPath(Path.GetFullPath(path));
 var type=asm.GetType("Lokad.Lython.LythonEngine",true)!;var engine=Activator.CreateInstance(type)!;var compile=type.GetMethod("Compile",[typeof(string)])!;
 foreach(var c in cases) {
  using var script=(IDisposable)compile.Invoke(engine,[c.Value])!;var st=script.GetType();
  var diagnostics=((IEnumerable)st.GetProperty("Diagnostics")!.GetValue(script)!).Cast<object>().Select(d=>d.GetType().GetProperty("Code")!.GetValue(d)).ToArray();
  result.Add(new{core=path,id=c.Key,valid=st.GetProperty("IsValid")!.GetValue(script),diagnostics});
 }
 load.Unload();
}
Console.WriteLine(JsonSerializer.Serialize(result));
