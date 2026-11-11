from pathlib import Path
import hashlib,json,tarfile
r=Path(__file__).parent;repo=Path.cwd()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
before=json.loads((r/'before.json').read_text(encoding='utf-8'))
for role,data in [('before',before),('candidate',meta)]:
 out=r/('sources-'+role+'.tar.xz');assert not out.exists()
 with tarfile.open(out,'w:xz')as archive:
  for name,expected in data['sourceHashes'].items():
   original=r/'originals'/name
   source=original if role=='before'and original.exists()else repo/name
   assert sha(source)==expected,name
   archive.add(source,arcname=name)
print('Exact unchanged qualified baseline and candidate sources frozen; all hashes match actual source pair. No edits after freeze.')
