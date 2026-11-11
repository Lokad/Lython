from pathlib import Path
import ast,hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b596';sha=lambda b:hashlib.sha256(b).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert sha(Path(n).read_bytes())==h,n
shutil.copyfile(r/'profile-archive.py',q/'archive.py')
p=q/'ops-profile.py';s=p.read_text(encoding='utf-8');needle='            record["warmupSeconds"] = elapsed\n'
assert s.count(needle)==1
s=s.replace(needle,needle+'            count=max(1,round(count*.15/(result["elapsedTicks"]/result["clockFrequency"])))\n            record["count"]=count # Short batches bound overshoot inside each six-second CPU window.\n')
p.write_text(s,encoding='utf-8',newline='\n')
with tarfile.open(q/'payload.tgz','w:gz')as a:
 for n in ['catalog.json','identity.json','source-identity.json','ops-profile.py','analyze-cpu-guest.py','archive.py','run.sh']:a.add(q/n,arcname=n)
with tarfile.open(q/'payload.tgz')as a:expanded=sum(((m.size+4095)//4096)*4096 for m in a.getmembers())
retire=[]
for remote,canonical in [
 ('word-materialization-b595-20261111/observations.tgz','benchmarks/results/2026-11-11-native-word-probing/quiet/observations.tgz'),
 ('word-materialization-b595-20261111/representative.json','benchmarks/results/2026-11-11-native-word-probing/quiet/representative.json'),
 ('dictionary-checkpoint-b590-20261111/observations.tgz','benchmarks/results/2026-11-11-dictionary-cpython-checkpoint/observations.tgz')]:
 p=Path(canonical);data=p.read_bytes();assert data==subprocess.check_output(['git','show','HEAD:'+p.as_posix()])
 retire.append(dict(kind='exact',knownPath=remote,bytes=len(data),sha256=sha(data),canonicalStorage=canonical,reason='Terminal unused duplicate; exact committed Git bytes verified locally. Both current image roles remain protected and linked; new collection supplies its own catalog. Fresh idle/path/UID/inode/all-alias proof required.'))
identity=json.loads((q/'identity.json').read_text(encoding='utf-8'))
config=dict(root='/home/lokad/lython-bench-checks/word-control-profile-b596-20261111',unit='lython-b596-word-control-profile-20261111',payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=sha((q/'payload.tgz').read_bytes()),requiredFreeBytes=252*1024*1024+expanded+65536,retire=retire,identity=identity)
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
for n in ['archive.py','ops-profile.py','analyze-cpu-guest.py']:ast.parse((q/n).read_text(encoding='utf-8'))
ast.parse((q/'remote-launch.py').read_text(encoding='utf-8').replace('config=CONFIG','config='+repr(config)).replace('inventoryOnly=INVENTORY_ONLY','inventoryOnly=True'))
print('Two6s current searchAsync profiles prepared;120sowner/180sobservation. No new core copies. RAM traces archived/SHA-verified under lease before unmount. Canonical committed duplicates verified; both current roles protected. Fresh VM inventory required.')
