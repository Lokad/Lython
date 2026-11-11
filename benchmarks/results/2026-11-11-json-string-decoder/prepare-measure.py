from pathlib import Path
import base64,copy,hashlib,json,shutil,subprocess

root=Path(__file__).resolve().parent;repo=Path('D:/code/Lython')
prior=root.parent/'regex-dev67-trusted-r215'
tool=root/'tool';tool.mkdir()
for name in ['Directory.Build.props','complete-jobs.csproj']:shutil.copyfile(prior/'tool'/name,tool/name)
source=(prior/'tool/Program.cs').read_text(encoding='utf-8-sig')
assert source.count('kv.Key != "Lokad.Lython.Utf8Regex.dll"')==1
source=source.replace('kv.Key != "Lokad.Lython.Utf8Regex.dll"','kv.Key != "Lokad.Lython.dll"')
source=source.replace('Require six UTF-8 admission/control diagnostics.','Require five existing complete scripts and one escaped-JSON encoding variant.')
source=source.replace('R215 same-producer trusted-input diagnostic','R217 complete JSON string decoding diagnostic')
(tool/'Program.cs').write_text(source,encoding='utf-8',newline='\n')
catalog=json.loads((root.parent/'bound-native-calls-r214/diagnostic/catalog.json').read_text(encoding='utf-8-sig'))
assert len(catalog['cases'])==5
target=copy.deepcopy(next(c for c in catalog['cases']if c['id']=='scripts.report-render.large'))
fixture=json.loads(target['fixtureJson']);records=json.loads(base64.b64decode(fixture['inputFiles']['records.json']))
encoded=json.dumps(records,ensure_ascii=True,separators=(',',':')).encode('utf-8')
fixture['inputFiles']['records.json']=base64.b64encode(encoded).decode('ascii')
target['id']='diagnostic.report-render.escaped-unicode.large';target['category']='diagnostic'
target['fixtureJson']=json.dumps(fixture,ensure_ascii=False,separators=(',',':'))
target['fixtureSha256']=hashlib.sha256(target['fixtureJson'].encode('utf-8')).hexdigest()
catalog['cases'].append(target)
(root/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
images=root/'images';images.mkdir()
for role in ['before','candidate']:
    target=images/role;shutil.copytree(root/'baseline/net11.0',target)
    if role=='candidate':shutil.copyfile(repo/'src/Lokad.Lython/bin/Release/net11.0/Lokad.Lython.dll',target/'Lokad.Lython.dll')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
identities={role:{p.name:sha(p)for p in (images/role).glob('*.dll')}for role in ['before','candidate']}
assert [n for n in identities['before']if identities['before'][n]!=identities['candidate'][n]]==['Lokad.Lython.dll']
before=json.loads((root/'before.json').read_text(encoding='utf-8'))
assert identities['before']['Lokad.Lython.dll']==before['coreSHA256']['net11.0']
names=subprocess.check_output(['git','ls-files','src/Lokad.Lython'],cwd=repo,text=True).splitlines()
candidate={n:sha(repo/n)for n in names if n.endswith('.cs')}
(root/'diagnostic-identity.json').write_text(json.dumps(dict(images=identities,candidateSourceSHA256=candidate,
    catalogSHA256=sha(root/'catalog.json'),scope='Complete public scripts; same fixed0.4 adapter/benchmark host. Only core differs. Five existing whole scripts plus unchanged report script with Python-default escaped Unicode input. Plain report/class report and text/CSV controls retained; no whole-script speedup forecast.'),indent=2)+'\n',encoding='utf-8')
for phase in ['warm','warm-async','compile']:
    folder=root/phase;folder.mkdir()
    (folder/'hash-witness.json').write_text(json.dumps(dict(source="return [hash('Lython-é😀'), hash((1, 2.0, True))]\n"),ensure_ascii=False)+'\n',encoding='utf-8')
print('Prepared existing complete-job driver, six scripts, only core differs; escaped file bytes',len(encoded),flush=True)
