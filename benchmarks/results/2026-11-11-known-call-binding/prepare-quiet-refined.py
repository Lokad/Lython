from pathlib import Path
import ast
p=Path(__file__).parent
t=(p/'prepare-quiet.py').read_text(encoding='utf-8')
t=t.replace("r=Path(__file__).parent;q=r/'b586';q.mkdir(exist_ok=False);base=r.parent", "parent=Path(__file__).parent;r=parent/'refined';q=r/'b587';q.mkdir(exist_ok=False);base=parent.parent")
t=t.replace('known-call-binding-b586','known-call-binding-b587').replace('lython-b586','lython-b587')
t=t.replace('5920broad focused actual11/10 checks plus final4internal/15public per target with overlap,14isolated CPython correctness goldens','5936focused actual11/10 checks and15isolated CPython correctness goldens')
t=t.replace('R244 uncommitted prepared known-call binding','R244 refined prepared known-call binding, omitting unused post-completion presence writes and duplicate small-loop entry check')
start=t.index('retire=[]');end=t.index('config=dict(',start)
t=t[:start]+'''retire=[]
for canonicalName,entry,known,expected in [
 ('2026-11-11-constructor-binding/preparation.tar.xz','candidate-core.dll','constructor-binding-b574-20261111/candidate/Lokad.Lython.dll','f5ae2355139a2588a3e6193c4c44332da7a2b0cf98bdcd6b1614878eebc479d2'),
 ('2026-11-11-ordinary-return-consumption/quiet/observations.tgz','current-core.dll','iteration-returns-b570-20261111/candidate/Lokad.Lython.dll','9ee7e700580399ff80d803f0a7535d2de5ff0f10d35a736370f457383be1b67f')]:
 canonical=Path('benchmarks/results')/canonicalName;data=canonical.read_bytes()
 assert data==subprocess.run(['git','show','HEAD:'+canonical.as_posix()],capture_output=True,check=True).stdout
 with tarfile.open(canonical)as archive:core=archive.extractfile(entry).read()
 assert hashlib.sha256(core).hexdigest()==expected
 retire.append(dict(kind='all-aliases',knownPath=known,bytes=len(core),sha256=expected,canonicalStorage=canonical.as_posix(),canonicalEntry=entry,canonicalArchiveSHA256=sha(canonical),reason='Obsolete15f/4aa historical image, no longer source or selected reference. Current690/reference and firstR244 prototype stay protected. Exact committed canonical archive/core verified; fresh path/UID/inode/all-alias proof before retirement.'))
''' +t[end:]
start=t.index('referencePath=');end=t.index('ast.parse(t.replace(',start)
t=t[:start]+'''referencePath="base/'captured-sequence-b583-20261111/candidate/Lokad.Lython.dll'"
t=t.replace("source=base/'constructor-binding-b574-20261111/candidate'","source=base/'captured-sequence-b583-20261111/candidate'")
start=t.index('protected={');end=t.index("with(base/'comparison-vm.lock')",start)
protected={"captured-sequence-b583-20261111/candidate/Lokad.Lython.dll":reference,"known-call-binding-b586-20261111/candidate/Lokad.Lython.dll":'00e08cfa247846f713d88f4e07c095bc7822d5e7234eb94be18a11c1db7628df'}
t=t[:start]+"protected={base/name:h for name,h in "+repr(protected)+".items()}\\n"+t[end:]
t=t.replace("os.link(root/name,root/role/'Lokad.Lython.dll')","os.link("+referencePath+" if role=='reference' else root/name,root/role/'Lokad.Lython.dll')")
t=t.replace("canonicalStorage='Pre-existing independently verified persistent local source.tar; exact path/bytes/SHA recorded in files'","canonicalStorage='Exact committed canonical benchmark archive entries verified locally; current selected roles and initial adverse prototype protected; each retirement has fresh path/UID/inode/all-alias proof'")
''' +t[end:]
ast.parse(t)
(p/'prepare-quiet-refined-expanded.py').write_text(t,encoding='utf-8')
exec(compile(t,str(p/'prepare-quiet-refined-expanded.py'),'exec'),{'__file__':str(p/'prepare-quiet-refined-expanded.py')})
