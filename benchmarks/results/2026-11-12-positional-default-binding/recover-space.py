from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b611'
for name in ['config.json','inventory.stdout','inventory.stderr']:shutil.copyfile(q/name,q/(name+'.first-space-short'))
archive=Path('benchmarks/results/2026-11-12-ordinary-call-source-gate/observations.tgz')
assert archive.read_bytes()==subprocess.check_output(['git','show','HEAD:'+archive.as_posix()])
config=json.loads((q/'config.json').read_text(encoding='utf-8'))
with tarfile.open(archive)as t:
 for name in ['profiles/guarded-search/perf-stacks.txt','profiles/guarded-search/perf.data','profiles/guarded-search/perf.map','profiles/prefix-jit.txt']:
  data=t.extractfile(name).read()
  config['retire'].append(dict(kind='exact',knownPath='call-boundary-profile-b610-20261112/'+name,bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),canonicalStorage=archive.as_posix(),canonicalEntry=name,canonicalArchiveSHA256=hashlib.sha256(archive.read_bytes()).hexdigest(),reason='Onlyunusedterminal rawfileduplicate, completecommitted0964e84c canonicalprofile/code archive independentlyverified. Originalfailedfloorconditionpreserved; no remote mutation occurred. Freshidle/UID/path/inode/allaliasesproof required; currentb2protected, floors unchanged.'))
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
print('Originalshortfloor preserved; four exact committedprofileduplicates added, no floorchange.')
