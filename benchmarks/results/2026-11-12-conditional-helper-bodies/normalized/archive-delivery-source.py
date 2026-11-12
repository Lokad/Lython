from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b614'
root=Path('benchmarks/results/2026-11-12-conditional-helper-bodies');out=root/'normalized';out.mkdir(exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
assert all(sha(Path(n))==h for n,h in meta['sourceHashes'].items())
for phase in ['warm-sync','warm-async','compile']:shutil.copytree(r/phase,out/phase)
for name in ['diagnostic-identity.json','sources-candidate.tar.xz','focused-checks.json','python-golden.json','code-facts.json','focused.py','freeze-and-measure.py','prepare-quiet.py','finalize-metadata.py','archive-delivery-source.py']:shutil.copyfile(r/name,out/name)
shutil.copyfile(r.parent/'python-goldens.json',out/'original-python-goldens.json')
shutil.copytree(r/'tests',out/'tests')
for p in r.glob('*.log'):shutil.copyfile(p,out/p.name)
(out/'implementation.patch').write_bytes(subprocess.check_output(['git','diff','--','src/Lokad.Lython','tests']))
with tarfile.open(out/'development.tar.xz','w:xz')as t:
 t.add(r/'candidate-net10.dll',arcname='candidate-net10.dll')
 for folder in [r,r/'tool']:
  for p in folder.iterdir():
   if p.is_file()and(p.suffix in ['.py','.cs','.csproj','.stdout','.stderr']or p.name.endswith('.before')):t.add(p,arcname=('tool/'if folder.name=='tool'else'local/')+p.name)
 for name in ['tests/Lokad.Lython.PublicApi.Tests/NativeReturnFunctionTests.cs','tests/Lokad.Lython.Tests/NativeUnaryReentryTests.cs']:t.add(name,arcname=name)
quiet=out/'b614';quiet.mkdir();receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'));identity=json.loads((q/'identity.json').read_text(encoding='utf-8'))
assert receipt['complete']and receipt['jobs']==4 and receipt['allWorkersJoined']and receipt['guestFixturesRemoved']and receipt['quietBeforeAndAfter']and receipt['archivedAndVerifiedUnderLease']
assert sha(q/'observations.tgz')==receipt['archiveSHA256']
with tarfile.open(q/'observations.tgz')as t:assert hashlib.sha256(t.extractfile('current-core.dll').read()).hexdigest()==identity['coreSHA256']['candidate']==meta['candidateCoreSHA256']
raw=json.loads((q/'retrieved/confirmation/confirmation.json').read_text(encoding='utf-8'))
for job in raw['jobs']:
 assert job['workersJoinedAndFixturesRemoved']
 for role,v in job['identities'].items():assert next(x['sha256'].lower()for x in v['libraries']if Path(x['path']).name=='Lokad.Lython.dll')==identity['coreSHA256'][role]
for name in ['observations.tgz','archive-receipt.json','identity.json','source-identity.json','summary.json','terminal-status.json','inventory.json','launch.log','observe-not-before.json']:shutil.copyfile(q/name,quiet/name)
with tarfile.open(quiet/'preparation.tar.xz','w:xz')as t:
 for p in sorted(q.iterdir()):
  if p.is_file()and p.name not in ['observations.tgz','payload.tgz','current-core.dll']:t.add(p,arcname=p.name)
(out/'README.md').write_text('''# Compiler-only negated predicates — retained, delivery pending

2026-11-12. The retained form changes one compiler file. A chain of `not` used
immediately by a branch becomes the existing conditional plan with its roots
swapped by parity. No runtime unary operation, shape, guard or evaluator is added;
all runtime source bytes equal the qualified reference. The complete eligible
helper omits ordinary binding/workspace/body dispatch. Other unary expressions
and custom truth protocols retain supported ordinary execution before effects.
The raw three-file prototype and its confirmation remain separate and immutable.

996 focused checks pass on actual .NET 11/10 (153 internal/345 public per target).
Three independent CPython 3.13.2 goldens cover unary values/protocols and negated
branches. The admission callback test rebinds a global before a later read and
awaits real host I/O once, with balances and cleanup intact. The existing effect
frontier supplies that safety; no new mechanism. Five whole scripts pass both
APIs and separate compilation, stdout/files/ownership/cleanup. Warm allocation is
flat; guarded compilation allocation rises 1.02% for prepared nodes.

B614 completes four jobs in 254.822 seconds. Twelve-second warmups, seven rotating
350 ms batches, two independent processes per image. Qualified b2cb reference
versus a552 candidate, same fec79 worker/host/adapter0.4 on F4as_v7/Ubuntu24.04.4/
.NET11RC3 with ordinary GC/tiering. All identities, outputs, balances, quietness,
joined users, RAM cleanup and independently verified archival gates pass.

| Complete script / API | Elapsed change | Wider process CPU change | Unchanged-image process spread, reference / candidate |
| --- | --- | --- | --- |
| Guarded search / Run | -28.45..-22.30% | -23.51..-18.27% | 3.95% / 4.12% |
| Guarded search / RunAsync | -25.05..-14.58% | -20.54..-9.90% | 5.37% / 7.53% |
| Existing native search / Run | +0.08..+2.50% | +0.46..+1.54% | 1.08% / 1.30% |
| Captured text / Run | -2.01..+9.84% | -3.19..+9.05% | 2.67% / 9.11% |

Ranges compare all four independent-process median pairs; they are not confidence
intervals. Warm clocks include public invocation, mediated RAM I/O, full output
validation and prior-result cleanup; exclude startup/compile/fixture reset/protocol
and final cache disposal. Wider CPU includes reset/protocol. Local diagnostics
include immediate disposal and retain every adverse result, notably guardedAsync
+6..20%, capturedRun+1.92..6.49% and nativeAsync -22..+1.72%, with moving controls.

Retain the compiler-only simplification: the affected complete script improves
with supporting CPU and runtime machinery disappears versus the raw prototype.
Accept the small native-search control debt and extra compilation storage.
Captured-text regression remains unresolved, not disproved by unchanged runtime
source or by some neutral pairings. Its candidate control drift does not support
a precise causal claim. Track it in the next meaningful combined checkpoint;
do not start a repeated unchanged-image campaign. No new CPython, RSS, pending-I/O,
tail latency or final adapter qualification claim follows. Full exact-source
Windows/Ubuntu, both targets, original stress and installed consumers are pending.

Only exact committed raw-prototype duplicates were retired under fresh idle,
UID/path/inode/all-alias and canonical Git/SHA proofs; the reference remains
protected and OS/evidence floors unchanged. Terminal state is cached. Publication
and tagging remain held.
''',encoding='utf-8')
files={p.relative_to(out).as_posix():sha(p)for p in out.rglob('*')if p.is_file()}
(out/'files-sha256.json').write_text(json.dumps(files,indent=2)+'\n',encoding='utf-8')
print('Normalized source, 996 checks, independent goldens, all adversity and verified B614 archived; full delivery pending.')
