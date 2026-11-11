from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).parent;q=r/'b592'
sha=lambda b:hashlib.sha256(b).hexdigest()
for n in ['config.json','inventory.stderr','inventory.stdout']:
 shutil.copyfile(q/n,q/('initial-'+n))
p=Path('benchmarks/results/2026-11-11-current-storage-costs/observations.tgz')
data=p.read_bytes();assert data==subprocess.check_output(['git','show','HEAD:'+p.as_posix()])
config=json.loads((q/'config.json').read_text(encoding='utf-8'))
with tarfile.open(p)as archive:
 for n in ['profiles/report-async/perf.map','profiles/text-warm/perf.map']:
  b=archive.extractfile(n).read()
  config['retire'].append(dict(kind='exact',knownPath='current-value-profile-b591-20261111/'+n,bytes=len(b),sha256=sha(b),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=sha(data),canonicalEntry=n,reason='Duplicate terminal profile map. Exact canonical committed archive and entry verified locally; fresh path/UID/inode/single-alias proof before removal. No active users or selected image affected.'))
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
print('Original disk refusal retained; two exact committed profile-map duplicates selected for fresh inventory. Reserve unchanged.')
