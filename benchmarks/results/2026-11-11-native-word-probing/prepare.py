from pathlib import Path
import hashlib,json,shutil,subprocess
r=Path(__file__).parent;old=r.parent/'native-iteration-storage-r249';repo=Path.cwd()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
before=json.loads((old/'before.json').read_text(encoding='utf-8'))
for n,h in before['sourceHashes'].items():
 assert sha(repo/n)==h,n
 p=r/'originals'/n;p.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(repo/n,p)
for role in ['before','candidate']:shutil.copytree(old/'images/before',r/'images'/role)
for phase in ['warm-sync','warm-async','compile']:
 (r/phase).mkdir();shutil.copyfile(old/phase/'hash-witness.json',r/phase/'hash-witness.json')
for name in ['before-net10.dll','catalog.json']:shutil.copyfile(old/name,r/name)
before.update(head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),hypothesis='Eliminate compact mapped word-object construction for duplicate-key native histogram probes. Use existing inline UTF8 capacity, cursor and eager preflight; one native loop and shared canonical string probe/counter update. Ordinary object call/return ABI unchanged. Pending private word bindings materialize from funded bytes before fallback/exit/errors; inserted keys retain existing allocation/order. No new ownership/cache/policy.',controls='Existing six complete scripts, both APIs and separate compilation. Preserve Unicode, key/last-word identity, empty source, promotion, namespace/capture, host reentry, cancellation, terminal refusal and cleanup. Source pair diagnostics before bounded quiet comparison; no CPU forecast.',qualification='Exact8f8628a4 source,43087005596fullWindows/Ubuntu actual11/10 stress/packageGREENcached. a2abcac1 adds external regression coverage only; 38internal/78public per actual target pass after nonincremental restoration builds.')
(r/'before.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8');shutil.copyfile('PLAN.md',r/'PLAN-before-prototype.md')
print('Qualified baseline and all sources frozen before R250 prototype edits.')
