from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile
r=Path(__file__).resolve().parent;q=r/'b611';out=Path('benchmarks/results/2026-11-12-positional-default-binding');out.mkdir(exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();meta=json.loads((r/'diagnostic-identity.json').read_text(encoding='utf-8'))
assert all(sha(Path(n))==h for n,h in meta['sourceHashes'].items())
for phase in ['warm-sync','warm-async','compile']:shutil.copytree(r/phase,out/phase)
for name in ['diagnostic-identity.json','sources-candidate.tar.xz','focused-checks.json','focused.py','freeze-and-measure.py','prepare-quiet.py','recover-space.py','archive-withdraw.py']:shutil.copyfile(r/name,out/name)
shutil.copytree(r/'tests',out/'tests')
for p in r.glob('*.log'):shutil.copyfile(p,out/p.name)
(out/'implementation.patch').write_bytes(subprocess.check_output(['git','diff','--','src/Lokad.Lython']))
with tarfile.open(out/'development.tar.xz','w:xz')as t:
 t.add(r/'candidate-net10.dll',arcname='candidate-net10.dll')
 for p in (r/'tool').glob('*'):
  if p.is_file():t.add(p,arcname='tool/'+p.name)
 for p in r.glob('*.before'):t.add(p,arcname='originals/'+p.name)
 t.add(r/'catalog.json',arcname='catalog.json')
quiet=out/'b611';quiet.mkdir()
receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'));identity=json.loads((q/'identity.json').read_text(encoding='utf-8'))
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
(out/'decision.json').write_text(json.dumps(dict(status='Withdraw this implementation; no permanent design veto',baseline='983bab96/productionunchanged796c3218 fullyqualifiedGREEN43166350709cached',focused='628checksactual11/10pass, fivecomplete scriptsbothAPIs/separatecompile goldens/files/balances/cleanup pass, allocationessentiallyflat',
 quiet='B611sixjobs170.297s allgates. GuardedRun elapsed-1.09..+.11%, CPU-6.89..-1.41%; GuardedAsync-5.65..+2.28%, CPU-8.54..+2.33%. NativeRun+1.45..+3.13%, CPU-3.03..+1.86%; CSVAsync-2.29..+.65%, CPU0..+3.23%; reportRun-1.90..+1.95%, CPU-2.04..+2.04%; capturedRun+1.43..+7.05%, CPU-3.41..+6.12%. Allcontrols/IQR/raw retained; rangesnotconfidenceintervals.',
 reason='Shareddefaultfill and fewer projectedargumentrecords do not remove whole ordinary work/binding stage; source adds30netlines/asyncbranching and broadens eligibility. Modest/uncertainprimary improvement and adversenative/capturedelapsed do not justify accepting extra debt for thisform. No fixedpercentageveto or claim allCPUresultsregress. No unchangedconfirmation selected because source removalitself insufficient; distinct coherentdesign remainsopen.',
 desktop='Preserve nativeRun+11..18%, guardedAsync+7..13%, CSVAsync+2..3%, reportRun+9..24%, allunchangedcontrols; no rejectionfromunstabledesktopalone.',
 cleanup='Terminalcached, usersjoined/RAMremoved. First inventory refusedbeforemutation259276800+8638464<270753792B; exactcommittedprofileduplicates suppliedheadroomunderfreshlease/UID/path/inode/allaliases/GitSHAproof, floorsunchanged. Superseded09adallaliases andB610raw/catalog/maps/stack/code duplicates retired; currentb2protected. Free261525504B afterarchive.',
 ongoing='Restore3prototypefilesfromverifiedbackups, nonincrementalbuild thenfocusedchecks, no unchangedfullqualification/CPTreplay. Nextsourcegate completes ordinaryconditional helperbody onexistingnativeplan ratherthanpackagingdifferentbindingaroundoldwork. Preservepriorarena/sharedprefixlessons, Python/cancellation/admission/asyncsemantics. Overallperformanceobjective active; regexindependent/publicationheld.'),indent=2)+'\n',encoding='utf-8')
(out/'README.md').write_text('''# Positional/default binding: withdrawn prototype

2026-11-12, three-file source pair against qualified983bab96/unchanged796c3218.
Direct positional fill accepts keyword-only signatures and shares default filling;
native borrows still require complete parameters, actual pending defaults retain
funded inputs.628focused actual11/10 checks and five complete scripts/bothAPIs/
separatecompile pass all goldens/files/balances/cleanup. Allocation effectively flat.

Quiet B611sixjobs170.297s allgates/join/RAM/archive pass. GuardedRun elapsed
−1.09..+0.11%, widerCPU−6.89..−1.41%; guardedAsync−5.65..+2.28%, CPU−8.54..+2.33%.
NativeRun elapsed+1.45..3.13%, capturedRun+1.43..7.05%; their CPU overlaps zero.
CSV/report controls uncertain. Four independent process-median pairings are not
confidence intervals. All desktop adversity and failed inventory remain unchanged.

Withdraw this form: it shares a loop/removes record projection but keeps ordinary
binding/workspace/dispatch and adds control flow. Modest/uncertain primary benefit
does not justify accepting additional native/captured elapsed debt. This is not a
fixed%veto, a claim everyCPUresult worsens, or permanent rejection of shared binding.
No unchanged retiming or final platform/package qualification selected. Native
helper/body work remains the next coherent source question; overall goal active.

Same F4as_v7 Ubuntu24.04.4/.NET11RC3/commonfec79 worker/adapter0.4, ordinaryGC/tiering,
5swarmup/7rotated150msrounds and two processes/image. Warm includes mediatedRAMI/O,
fulloutputvalidation/priorresultcleanup; startup/compile/reset/protocol/finalcache
excluded. WiderCPU includes reset/protocol; desktop includes immediate disposal.
No newCPython/RSS/tail/largefile claim. Currentb2 runtime remains fully qualified.
''',encoding='utf-8')
files={p.relative_to(out).as_posix():sha(p)for p in out.rglob('*')if p.is_file()};(out/'files-sha256.json').write_text(json.dumps(files,indent=2)+'\n',encoding='utf-8')
with Path('.gitattributes').open('a',encoding='utf-8')as f:f.write('\nbenchmarks/results/2026-11-12-positional-default-binding/** -text whitespace=cr-at-eol\nbenchmarks/results/2026-11-12-positional-default-binding/**/*.patch -text -whitespace\n')
for n in meta['changedProductionFiles']:
 p=Path(n);backup=r/(p.name+'.before');assert backup.exists();p.write_bytes(backup.read_bytes())
print('Alloriginalsource/data/failures/cores archived; threeprototype files restored. Require fresh nonincremental build/focused checks.')
