from pathlib import Path
import ast
parent=Path(__file__).parent
t=(parent/'prepare-quiet-expanded.py').read_text(encoding='utf-8')
t=t.replace("r=Path(__file__).parent;q=r/'b588';q.mkdir(exist_ok=False);base=r.parent", "parent=Path(__file__).parent;r=parent/'refined';q=r/'b589';q.mkdir(exist_ok=False);base=parent.parent")
t=t.replace('dictionary-values-b588','dictionary-values-b589').replace('lython-b588-dictionary-values','lython-b589-dictionary-values').replace('lython-b588-fixtures','lython-b589-fixtures')
t=t.replace('3566focused actual11/10 and4isolated','Refined focused actual11/10 and4isolated')
t=t.replace('R245 private native dictionary integer storage','R245 refined dictionary value storage, direct shared-record array reads eliminate dead inlined integer-allocation branch')
start=t.index('jobs=');end=t.index('\nt=replace',start)
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large','warm'],['scripts.text-summary-helpers.large','warm'],['scripts.csv-validated-records.large','warm'],['scripts.report-render.large','warm-async'],['scripts.json-summary-helpers.large','warm']]
t=t[:start]+'jobs='+repr(jobs)+t[end:]
t=t.replace('assert len(jobs)==6','assert len(jobs)==5')
start=t.index('retire=[]');end=t.index('config=dict(',start)
t=t[:start]+'''retire=[]
for canonicalName,known in [
 ('2026-11-11-known-call-binding/initial/quiet/observations.tgz','known-call-binding-b586-20261111/observations.tgz'),
 ('2026-11-11-known-call-binding/refined/quiet/observations.tgz','known-call-binding-b587-20261111/observations.tgz'),
 ('2026-11-11-native-counting-loops/quiet/observations.tgz','native-counter-loops-b576-20261111/observations.tgz')]:
 canonical=Path('benchmarks/results')/canonicalName;data=canonical.read_bytes()
 assert data==subprocess.run(['git','show','HEAD:'+canonical.as_posix()],capture_output=True,check=True).stdout
 retire.append(dict(kind='exact',knownPath=known,bytes=len(data),sha256=hashlib.sha256(data).hexdigest(),canonicalStorage=canonical.as_posix(),reason='Duplicate retired R244 evidence committed in e2240c23. Original raw bytes preserved in Git; current690 reference and initialR245 core protected. Fresh UID/inode/all-alias proof before deleting only these duplicate files.'))
''' +t[end:]
start=t.index('protected={"captured-sequence');end=t.index('\nt=t[:start]',start)
t=t[:start]+'protected={"captured-sequence-b583-20261111/candidate/Lokad.Lython.dll":reference,"dictionary-values-b588-20261111/candidate/Lokad.Lython.dll":json.loads((parent/"diagnostic-identity.json").read_text(encoding="utf-8"))["images"]["candidate"]["Lokad.Lython.dll"]}'+t[end:]
position=t.index("ast.parse(t.replace('config=CONFIG'")
t=t[:position]+"t=t.replace('jobs=6,ownerSeconds=210','jobs=5,ownerSeconds=180').replace('RuntimeMaxSec=210','RuntimeMaxSec=180')\n"+t[position:]
position=t.index("(q/'remote-launch.py').write_text")
# Adapt the owned archive before payload creation, keeping exact job assertions.
needle="(q/n).write_text(t,encoding='utf-8',newline='\\n')"
replacement="t=t.replace('seconds=270','seconds=240').replace('ownerSeconds=210','ownerSeconds=180').replace(\"['elapsedSeconds']<210\",\"['elapsedSeconds']<180\")\n "+needle
assert needle in t
t=t.replace(needle,replacement)
t=t.replace(".replace('B576','B588')",".replace('B576','B589')")
ast.parse(t)
(parent/'prepare-quiet-refined-expanded.py').write_text(t,encoding='utf-8')
exec(compile(t,str(parent/'prepare-quiet-refined-expanded.py'),'exec'))
