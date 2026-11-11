from pathlib import Path
import gzip,hashlib,json,shutil,subprocess
r=Path(__file__).parent/'b586'
assert not(r/'inventory.json').exists()and not(r/'observe-not-before.json').exists()
for n in ['inventory.stdout','inventory.stderr']:
 shutil.copyfile(r/n,r/('initial-space-refusal-'+n))
p=Path('benchmarks/results/2026-11-11-private-counter-segments/candidate-net11.0.dll.gz')
data=p.read_bytes();assert data==subprocess.run(['git','show','HEAD:'+p.as_posix()],capture_output=True,check=True).stdout
core=gzip.decompress(data);c=json.loads((r/'config.json').read_text(encoding='utf-8'))
c['retire'].append(dict(kind='all-aliases',knownPath='private-counter-b579-20261111/candidate/Lokad.Lython.dll',bytes=len(core),sha256=hashlib.sha256(core).hexdigest(),canonicalStorage=p.as_posix(),canonicalArchiveSHA256=hashlib.sha256(data).hexdigest(),reason='Obsolete qualifiedb584 runtime image, predates5f/690; next reference is690/ccdd. Exact decompressed committed Git bytes verified. Fresh idle/absolute path/UID/inode/all-alias proof must exclude protected current roles before retirement.'))
(r/'config.json').write_text(json.dumps(c,indent=2)+'\n',encoding='utf-8')
print('Original space refusal preserved; one additional obsolete core verified against committed canonical gzip. Floors unchanged; fresh inventory required.')
