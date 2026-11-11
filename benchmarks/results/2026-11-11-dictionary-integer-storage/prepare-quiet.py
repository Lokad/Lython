from pathlib import Path
import ast
r=Path(__file__).parent
template=(r.parent/'known-call-binding-r244/prepare-quiet-refined-expanded.py').read_text(encoding='utf-8')
template=template.replace("parent=Path(__file__).parent;r=parent/'refined';q=r/'b587';q.mkdir(exist_ok=False);base=parent.parent", "r=Path(__file__).parent;q=r/'b588';q.mkdir(exist_ok=False);base=r.parent")
template=template.replace('known-call-binding-b587','dictionary-values-b588').replace('lython-b587-known-call-binding','lython-b588-dictionary-values').replace("'b587'","'b588'").replace('lython-b587-fixtures','lython-b588-fixtures')
start=template.index("identity.update(sourceRevision=");end=template.index(',coreSHA256=',start)
template=template[:start]+"identity.update(sourceRevision='R245 private native dictionary integer storage versus fully qualified6904093e. Exact source/core pair, existing scripts/worker/adapter; 3566focused actual11/10 and4isolated CPython correctness goldens pass. Entry capacity grows8bytes, includes funding for eventual immutable boxes; copies/shared layouts preserve identity. Desktop allocation/elapsed evidence preserved separately; no CPU gain or neutrality claim before quiet confirmation'"+template[end:]
template=template.replace('Qualified69043058223560GREEN cached; R244focused only, no full prototype qualification yet.','Qualified69043058223560GREEN cached; R245focused only, no full prototype qualification yet.')
start=template.index('jobs=');end=template.index('\nt=replace',start)
jobs=[['scripts.text-summary-helpers.nonlocal-counter.large','warm'],['scripts.text-summary-helpers.nonlocal-counter.large','warm-async'],['scripts.text-summary-helpers.large','warm'],['scripts.text-summary-helpers.large','warm-async'],['scripts.csv-validated-records.large','warm'],['scripts.report-render.large','warm-async']]
template=template[:start]+'jobs='+repr(jobs)+template[end:]
template=template.replace('assert len(jobs)==5','assert len(jobs)==6')
template=template.replace('R244 bounded known-call binding versus qualified690, CSV/search both APIs and unchanged textsync control. Desktop conflict/adversity retained.', 'R245 native dictionary values versus qualified690, captured/ordinary text both APIs and CSV control. Both forms, all desktop adversity preserved.')
start=template.index('retire=[]');end=template.index('config=dict(',start)
template=template[:start]+'''retire=[]
for form,known in [('initial','known-call-binding-b586-20261111/candidate/Lokad.Lython.dll'),('refined','known-call-binding-b587-20261111/candidate/Lokad.Lython.dll')]:
 canonical=Path('benchmarks/results/2026-11-11-known-call-binding')/form/'candidate-net11.0.dll.gz';data=canonical.read_bytes()
 assert data==subprocess.run(['git','show','HEAD:'+canonical.as_posix()],capture_output=True,check=True).stdout
 core=gzip.decompress(data);expected=hashlib.sha256(core).hexdigest()
 retire.append(dict(kind='all-aliases',knownPath=known,bytes=len(core),sha256=expected,canonicalStorage=canonical.as_posix(),canonicalArchiveSHA256=sha(canonical),reason='Withdrawn R244 source/core/evidence committed in e2240c23. Current690 reference stays protected. Fresh path/UID/inode/all-alias proof before retiring only these obsolete images.'))
''' +template[end:]
start=template.index('protected={"captured-sequence');end=template.index('\nt=t[:start]',start)
template=template[:start]+'protected={"captured-sequence-b583-20261111/candidate/Lokad.Lython.dll":reference}'+template[end:]
template=template.replace('current selected roles and initial adverse prototype protected','current selected reference protected; withdrawn prototype originals preserved in committed canonical Git')
template=template.replace(".replace('B576','B586')",".replace('B576','B588')")
needle="ast.parse(t.replace('config=CONFIG'"
position=template.index(needle)
template=template[:position]+"t=t.replace('jobs=5,ownerSeconds=210','jobs=6,ownerSeconds=210')\n"+template[position:]
ast.parse(template)
(r/'prepare-quiet-expanded.py').write_text(template,encoding='utf-8')
exec(compile(template,str(r/'prepare-quiet-expanded.py'),'exec'))
for name in ['retrieve.py','summarize.py']:
 p=r/'b588'/name
 p.write_text(p.read_text(encoding='utf-8').replace("['jobs']==5","['jobs']==6").replace("len(raw['jobs'])==5","len(raw['jobs'])==6").replace('jobs=5,elapsedSeconds','jobs=6,elapsedSeconds').replace('five-job','six-job'),encoding='utf-8',newline='\n')
