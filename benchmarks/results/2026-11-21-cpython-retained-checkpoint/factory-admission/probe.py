from pathlib import Path
from hashlib import sha256
import json, os, shutil, subprocess

lab=Path(__file__).resolve().parent
repo=lab.parents[3]
prior=lab.parent/"joint-lexical-admission-on-current"
bench=lab.parent/"latest-checkpoint-cpython-20261121"
catalog=lab.parent/"inline-keyword-default-20261121/representative.json"
core=bench/"frozen/Lokad.Lython.dll"
before=lab.parent/"inline-keyword-default-20261121/frozen/reference/Lokad.Lython.dll"
assert sha256(core.read_bytes()).hexdigest()==json.loads((bench/"quiet/identity.json").read_text(encoding="utf-8"))["imageFiles"]["candidate"]["Lokad.Lython.dll"]
assert sha256(before.read_bytes()).hexdigest()==json.loads((lab.parent/"inline-keyword-default-20261121/identity.json").read_text(encoding="utf-8"))["imageFiles"]["reference"]["Lokad.Lython.dll"]
source=(prior/"growth-diagnostic.cs").read_text(encoding="utf-8")
source="using System.Diagnostics;\nusing System.Runtime.CompilerServices;\n"+source
source=source.replace('&& id != "scripts.ordinary-fully-supplied-helper"', '&& id != "scripts.ordinary-fully-supplied-helper" && id != "scripts.factory-observed-defaults"')
source=source.replace("    budget.Growths = 0;", "    budget.Capturing = true;")
source=source.replace("    rows.Add(new { id, budget.Growths, baseline });",
                      """    budget.Capturing = false;
    rows.Add(new { id, budget.Growths, baseline, sources = budget.Sources.Select(e => new
        { site = e.Key.Site, bytes = e.Key.Delta, count = e.Value }).OrderByDescending(e => e.count) });""")
source=source.replace('    internal int Growths { get; set; }', '''    internal int Growths { get; private set; }
    internal bool Capturing { get; set; }
    internal Dictionary<(string Site, long Delta), int> Sources { get; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddGrowth(long delta)
    {
        if (!Capturing || delta <= 0) return;
        Growths++;
        var site = "unresolved";
        foreach (var frame in new StackTrace().GetFrames())
        {
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            if (type?.Assembly != typeof(LythonEngine).Assembly || type.FullName!.Contains("MemoryGovernor", StringComparison.Ordinal))
                continue;
            site = type.Name + "." + method!.Name;
            break;
        }
        var key = (site, delta);
        Sources[key] = Sources.GetValueOrDefault(key) + 1;
    }
''')
source=source.replace("        if (bytes > 0) Growths++;", "        AddGrowth(bytes);")
source=source.replace("            var growth = bytes > inner.Bytes;", "            var growth = bytes - inner.Bytes;")
source=source.replace("            if (growth) root.Growths++;", "            root.AddGrowth(growth);")
(lab/"Program.cs").write_text(source,encoding="utf-8",newline="\n")
(lab/"Probe.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup>
<OutputType>Exe</OutputType>
<TargetFramework>net11.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
</PropertyGroup>
<ItemGroup><Reference Include="Lokad.Lython"><HintPath>$(InspectionCorePath)</HintPath></Reference></ItemGroup>
</Project>
''',encoding="utf-8",newline="\n")
sdk=Path.home()/".dotnet11"
dotnet=sdk/"dotnet.exe"
env=dict(os.environ,DOTNET_ROOT=str(sdk),DOTNET_ROOT_X64=str(sdk),PATH=str(sdk)+os.pathsep+os.environ["PATH"])
with (lab/"build.log").open("wb") as log:
    subprocess.run([dotnet,"build",lab/"Probe.csproj","-c","Release",f"-p:InspectionCorePath={core}"],
                   cwd=repo,env=env,check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
images={}
for role,payload in (("reference",before),("current",core)):
    image=lab/role
    image.mkdir()
    for p in (lab/"bin/Release/net11.0").iterdir():
        if p.is_file(): shutil.copyfile(p,image/p.name)
    shutil.copyfile(payload,image/payload.name)
    assert sha256((image/payload.name).read_bytes()).hexdigest()==sha256(payload.read_bytes()).hexdigest()
    images[role]={p.name:sha256(p.read_bytes()).hexdigest() for p in image.iterdir()}
    with (lab/f"{role}.json").open("wb") as output, (lab/f"{role}.stderr").open("wb") as err:
        subprocess.run([dotnet,image/"Probe.dll",catalog],cwd=repo,env=env,check=True,stdout=output,stderr=err,timeout=45)
    rows=json.loads((lab/f"{role}.json").read_text(encoding="utf-8-sig"))
    assert len(rows)==4
    for row in rows:
        assert sum(s["count"] for s in row["sources"])==row["Growths"]
        print(role,row["id"],row["Growths"],row["sources"][:6],flush=True)
(lab/"identity.json").write_text(json.dumps(dict(snapshot="4337b7765",prior="3a6accc97",workerImages=images,
    catalogSha256=sha256(catalog.read_bytes()).hexdigest(),
    method="Observed managed callers of successful host allowance growth. Stack capture perturbs execution; no CPU or timing inference."),indent=2)+"\n",encoding="utf-8",newline="\n")
