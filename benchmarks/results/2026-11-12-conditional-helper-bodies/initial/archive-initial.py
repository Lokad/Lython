from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b612';root=Path('benchmarks/results/2026-11-12-conditional-helper-bodies');root.mkdir(exist_ok=False);out=root/'initial';out.mkdir()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
assert all(sha(Path(n))==h for n,h in meta['sourceHashes'].items())
for phase in ['warm-sync','warm-async','compile']:shutil.copytree(r/phase,out/phase)
for name in ['diagnostic-identity.json','sources-candidate.tar.xz','focused-checks.json','python-goldens.json','code-facts.json','focused.py','freeze-and-measure.py','prepare-quiet.py','archive-initial.py']:shutil.copyfile(r/name,out/name)
shutil.copytree(r/'tests',out/'tests')
for p in r.glob('*.log'):shutil.copyfile(p,out/p.name)
(out/'implementation.patch').write_bytes(subprocess.check_output(['git','diff','--','src/Lokad.Lython','tests']))
with tarfile.open(out/'development.tar.xz','w:xz')as t:
 t.add(r/'candidate-net10.dll',arcname='candidate-net10.dll')
 for folder in [r,r/'tool']:
  for p in folder.iterdir():
   if p.is_file()and(p.suffix in ['.py','.cs','.csproj','.stdout','.stderr']or p.name.endswith('.before')):t.add(p,arcname=('tool/'if folder.name=='tool'else'local/')+p.name)
 for name in ['tests/Lokad.Lython.PublicApi.Tests/NativeReturnFunctionTests.cs','tests/Lokad.Lython.Tests/NativeUnaryReentryTests.cs']:t.add(name,arcname=name)
quiet=out/'b612';quiet.mkdir();receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'));identity=json.loads((q/'identity.json').read_text(encoding='utf-8'))
assert receipt['complete']and receipt['jobs']==6 and receipt['allWorkersJoined']and receipt['guestFixturesRemoved']and receipt['quietBeforeAndAfter']and receipt['archivedAndVerifiedUnderLease']
assert sha(q/'observations.tgz')==receipt['archiveSHA256']
with tarfile.open(q/'observations.tgz')as t:assert hashlib.sha256(t.extractfile('current-core.dll').read()).hexdigest()==identity['coreSHA256']['candidate']
raw=json.loads((q/'retrieved/confirmation/confirmation.json').read_text(encoding='utf-8'))
for job in raw['jobs']:
 assert job['workersJoinedAndFixturesRemoved']
 for role,v in job['identities'].items():assert next(x['sha256'].lower()for x in v['libraries']if Path(x['path']).name=='Lokad.Lython.dll')==identity['coreSHA256'][role]
for name in ['observations.tgz','archive-receipt.json','identity.json','source-identity.json','summary.json','terminal-status.json','inventory.json','launch.log','observe-not-before.json']:shutil.copyfile(q/name,quiet/name)
with tarfile.open(quiet/'preparation.tar.xz','w:xz')as t:
 for p in sorted(q.iterdir()):
  if p.is_file()and p.name not in ['observations.tgz','payload.tgz','current-core.dll']:t.add(p,arcname=p.name)
(root/'README.md').write_text('''# Conditional helper bodies — measured prototype

2026-11-12. **No runtime promotion or final qualification yet.** Source pair versus
fullyqualified983bab96/unchanged796c3218, frozenb2cb. Three production files add
unary eligibility/shape to the existing native plan, reusing normal unary kernels.
The ordinary guarded helper now has a prepared body and omits owned binding/work/
dispatch. Existing native bodies keep their route. No new engine/owner/cache/policy.

994focused actual11/10 checks pass, including two independent CPython3.13.2 goldens
and a post-admission global rebinding with actual host I/O/cleanup. Existing effect
frontier already protects later mutable reads; no extra protection mechanism.
An initial test used the void convenience overload, corrected before execution;
first reentry fixture had unresolvedlookup, then explicitglobal targets the real
effect barrier. All source/logs remain. Five complete scripts/bothAPIs/separate
compile pass stdout/files/allowance/cleanup. Warm allocation essentially flat;
guarded compilation allocation+1.10% for prepared nodes. No allocation/CPU conflation.

B612sixjobs170.169s, allgates/join/RAM/archive pass. GuardedRun elapsed−21.50..−11.26%,
CPU−17.74..−6.30%; guardedAsync−23.72..−19.27%, CPU−20.67..−18.36%. CapturedRun
elapsed−0.69..+13.04%, CPU+4.04..+13.33% is material additional possible debt;
candidate process spread8.16%, IQR0.92%. GuardedRun candidate spread12.48% and
native-search reference13.02% also signal uncertainty. CSVAsync elapsed+1.10..2.48%,
CPU0..+3.33%; reportRun−4.38..−1.32%, CPU−4..−2.04%. Native search elapsed
−10.16..+2.12%, CPU−9.59..−3.53%. Fourprocessmedianpairings are notconfidenceintervals.

One justified two-case longer-warmup/larger-batch confirmation will assess guarded
Run and capturedRun control debt before retention. No source change or permanent
one-attempt policy. Preserve initial desktop adversity: guardedAsync−0.75..+4.81%,
nativeAsync+3..6%, CSVAsync+1.69..5.63%, reportCompile+2.93..4.77%, allcontrols.

Same quietF4as_v7/Ubuntu24.04.4/.NET11RC3/commonfec79worker/adapter0.4, ordinaryGC/
tiering,5swarmup/7rotating150msrounds/twoindependentprocesses perimage. Warm includes
mediatedRAMI/O, fullvalidation/priorresultcleanup; excludesstartup/compile/reset/
protocol/finalcache. WiderCPU includes reset/protocol. Desktopincludesimmediate
disposal. No newCPython/RSS/tail/largefile or finaladapterqualification claim.

Only committed withdrawnR265core aliases/raw/catalog/records and exact historical
B606records retired after freshidle/UID/path/inode/allaliases/canonicalGitSHA proof;
currentb2 protected, OS/evidence floorsunchanged. Terminalcached, usersjoined and
fixtures removed; rawarchive independentlySHAverified. Publication stays held.
''',encoding='utf-8')
files={p.relative_to(out).as_posix():sha(p)for p in out.rglob('*')if p.is_file()};(out/'files-sha256.json').write_text(json.dumps(files,indent=2)+'\n',encoding='utf-8')
with Path('.gitattributes').open('a',encoding='utf-8')as f:f.write('\nbenchmarks/results/2026-11-12-conditional-helper-bodies/** -text whitespace=cr-at-eol\nbenchmarks/results/2026-11-12-conditional-helper-bodies/**/*.patch -text -whitespace\n')
print('InitialR266 sources/994checks/independentgoldens/alladversity/B612 archived; no promotion yet.')
