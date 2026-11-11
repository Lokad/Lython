from pathlib import Path
import hashlib,json,shutil,subprocess
r=Path(__file__).resolve().parent;old=r.parent/'word-materialization-r250';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert not(r/'originals').exists()
current=json.loads((old/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in current['sourceHashes'].items():assert sha(Path(n))==h,n
before=json.loads((old/'before.json').read_text(encoding='utf-8'))
for n,h in before['sourceHashes'].items():
 p=old/'originals'/n;assert sha(p)==h,n;d=r/'originals'/n;d.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,d)
for role in ['before','candidate']:shutil.copytree(old/'images/before',r/'images'/role)
for phase in ['warm-sync','warm-async','compile']:
 (r/phase).mkdir();shutil.copyfile(old/phase/'hash-witness.json',r/phase/'hash-witness.json')
for n in ['before-net10.dll','catalog.json']:shutil.copyfile(old/n,r/n)
before.update(head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),hypothesis='Coherent native storage/input migration over qualified8f862. R250 avoids duplicate inline word objects; R251 removes captured native helper packing/clearing and appended-cell parameter domain. Borrow actual argument prefix and definition-owned live cells in one readonly stack view passed by reference. Ordinary object call/return ABI, real binder and ownership unchanged. No new cache/controller/owner.',controls='Existing six complete scripts through both APIs; separate compilation. Preserve missing-parameter-before-cell-write, live cells and rebinding, defaults/keywords/wide calls, protocol suspension/cancellation, first refusal, aliases and cleanup. Compare combined source against qualified8f862; no isolated R251 CPU attribution. Original R250 async search/CSV adversity remains unresolved and preserved.',priorPrototype=dict(source='benchmarks/results/2026-11-11-native-word-probing/sources-candidate.tar.xz',coreSHA256=current['images']['candidate']['Lokad.Lython.dll'],evidenceCommit='364d9852',profile='B596two current6s windows: shared hot paths; instrumented/single-process evidence does not settle normal B595 control regression. No further unchanged retiming.'))
(r/'before.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8');shutil.copyfile('PLAN.md',r/'PLAN-before-prototype.md')
(r/'intermediate-r250-source.json').write_text(json.dumps(current,indent=2)+'\n',encoding='utf-8')
print('Qualified primary source/core baseline and exact intermediate R250 form preserved before R251 edits. No isolated attribution or final qualification claim.')
