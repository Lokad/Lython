from pathlib import Path
import hashlib, json, shutil, subprocess, tarfile

lab=Path(__file__).resolve().parent
repo=lab.parents[3]
source=lab.parent/'direct-native-text-dict-keys'
template=lab.parent/'postponed-annotation-cpython'
q=lab/'quiet'
def read(p): return p.read_text(encoding='utf-8')
def load(p): return json.loads(read(p))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def emit(p,s):
    if p.suffix=='.py': compile(s,str(p),'exec')
    p.write_text(s,encoding='utf-8',newline='\n')
def save(p,v): emit(p,json.dumps(v,indent=2,ensure_ascii=False)+'\n')
revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()
assert revision.startswith('c62457d49')
assert not subprocess.check_output(['git','diff','--name-only','HEAD','--','src'],cwd=repo)
verified=load(source/'candidate-verify.json')
index=load(source/'identity.json')
assert index['candidate']==revision and verified['state']=='Completed'
files=index['imageFiles']['candidate']
image=source/'frozen/candidate'
assert len(files)==32 and {n:sha(image/n) for n in files}==files
assert verified['catalogSha256']==index['catalogSHA256']==sha(source/'representative.json')
assert index['pythonWorkerSHA256']==sha(repo/'benchmarks/cpython-worker.py')
staging=load(source/'quiet/config.json')
receipt=load(source/'quiet/archive-receipt.json')
assert receipt['complete'] and receipt['archivedAndVerifiedUnderLease']
assert receipt['archiveSHA256']==sha(source/'quiet/observations.tgz')
q.mkdir()
identity=dict(sourceRevision=revision,imageSource=revision,purpose='Exact string-key dictionary delivery against stock CPython',
              imageFiles=dict(candidate=files),catalogSHA256=index['catalogSHA256'],cpythonWorkerSHA256=index['pythonWorkerSHA256'],
              coreSHA256={name:files['Lokad.Lython.dll'] for name in ('candidate','candidate-control')})
save(q/'identity.json',identity)
shutil.copyfile(repo/'benchmarks/cpython-worker.py',q/'cpython-worker.py')

prior=load(template/'quiet/config.json')
root='/home/lokad/lython-bench-checks/native-text-dict-keys-cpython-b1248-20261120'
unit='lython-b1248-native-text-dict-keys-cpython-20261120'
guard='scripts.config-generated-guards.medium.bad-0'
report='scripts.report-render.alternating-records'
for name in ('collect.py','archive-owned.py','retrieve.py','run.sh'):
    s=read(template/'quiet'/name).replace(prior['root'],root).replace(prior['unit'],unit).replace('B1240','B1248')
    s=s.replace('scripts.annotated-rule-closures.nested-factories.postponed',guard).replace('scripts.report-render.format-map.large',report)
    emit(q/name,s)
assert b'\r' not in (q/'run.sh').read_bytes()
parts=('identity.json','collect.py','archive-owned.py','run.sh','cpython-worker.py')
archive=q/'payload.tgz'
with tarfile.open(archive,'w:gz') as f:
    for n in parts: f.add(q/n,arcname=n)
config=dict(root=root,unit=unit,identity=identity,source=staging['root'],sourceRole='candidate',
            sourceFiles=files,sourceCatalogSHA256=identity['catalogSHA256'],catalogSHA256=identity['catalogSHA256'],
            cpythonWorkerSHA256=identity['cpythonWorkerSHA256'],retireGroups=[],donorArchiveSHA256=receipt['archiveSHA256'],
            payloadBytes=archive.stat().st_size,payloadSHA256=sha(archive),unpackedBytes=sum((q/n).stat().st_size for n in parts))
save(q/'config.json',config)
s=read(template/'quiet/remote-launch.py')
a,b=s.index('\nc=')+1,s.index('\nb=Path(')
s=(s[:a]+'c='+repr(config)+s[b:]).replace('lython-b1240-fixtures','lython-b1248-fixtures')
emit(q/'remote-launch.py',s)
emit(lab/'launch.py',read(template/'launch.py').replace('B1240 annotation and shipment','B1248 validator and shipment'))
