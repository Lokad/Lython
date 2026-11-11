from pathlib import Path
import hashlib,json,shutil,tarfile
r=Path(__file__).parent;out=r/'refined';out.mkdir(exist_ok=False);repo=Path.cwd()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
before=json.loads((r/'before.json').read_text(encoding='utf-8'))
for name,h in meta['sourceHashes'].items():assert sha(repo/name)==h,name
for role,hashes in [('before',before['sourceHashes']),('candidate',meta['sourceHashes'])]:
 with tarfile.open(r/('sources-'+role+'.tar.xz'),'w:xz')as t:
  for name,h in hashes.items():
   source=r/'originals'/name if role=='before'and(r/'originals'/name).exists()else repo/name
   assert sha(source)==h,name;t.add(source,arcname=name)
for name in meta['changedProductionFiles']:
 p=out/'originals'/name;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(r/'originals'/name,p)
for role in ['before','candidate']:shutil.copytree(r/'images/before',out/'images'/role)
shutil.copyfile(r/'before-net10.dll',out/'before-net10.dll');shutil.copyfile(r/'catalog.json',out/'catalog.json')
for phase in ['warm-sync','warm-async','compile']:
 (out/phase).mkdir();shutil.copyfile(r/phase/'hash-witness.json',out/phase/'hash-witness.json')
before['qualification']='Fully qualified6904093e, terminalGREEN43058223560cached. Exact same frozenccdd baseline in both desktop and quiet pairs; no metadata/source clock mixing.'
before['hypothesis']='Source-supported R244 refinement removes presence writes after complete mapped user-function binding (no consumer uses those bits; deferred protocols explicitly excluded). Keeps real presence for generic/deferred and builtin binding. Removes redundant small-loop entry check; caller/body already check and wide pure fills retain64slot cancellation checks. No new mode/carrier/cache/owner/ABI. Preserve original first-form quietsync adversity and all semantics.'
(out/'before.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8')
t=(r/'freeze-and-measure.py').read_text(encoding='utf-8').replace("r.parent/'iteration-returns-r225","r.parent.parent/'iteration-returns-r225").replace("r.parent/'call-preparation-r236","r.parent.parent/'call-preparation-r236")
(out/'freeze-and-measure.py').write_text(t,encoding='utf-8')
print('First form exact sources/core/data frozen before refinement; before source remains qualified690. No first-form evidence overwritten.')
