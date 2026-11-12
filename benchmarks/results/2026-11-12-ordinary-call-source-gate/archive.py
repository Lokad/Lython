from pathlib import Path
import hashlib,json,shutil,tarfile
r=Path(__file__).resolve().parent;q=r/'b610';out=Path('benchmarks/results/2026-11-12-ordinary-call-source-gate');out.mkdir(exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'));identity=json.loads((q/'identity.json').read_text(encoding='utf-8'))
assert receipt['complete']and receipt['allWorkersJoined']and receipt['guestFixturesRemoved']and receipt['quietBeforeAndAfter']and receipt['archivedAndVerifiedUnderLease']
assert sha(q/'observations.tgz')==receipt['archiveSHA256']
with tarfile.open(q/'observations.tgz')as t:assert hashlib.sha256(t.extractfile('current-core.dll').read()).hexdigest()==identity['coreSHA256']['candidate']
p=q/'retrieved/profiles/guarded-search';leaves=json.loads((p/'guest-cpu-leaves.json').read_text(encoding='utf-8'));record=json.loads((p/'record.json').read_text(encoding='utf-8'))
assert leaves['samples']==1006 and record['exitCode']==0 and record['closed']['status']=='Closed'
for n in ['source-gate.md','Program.cs','inspect.csproj','code-facts.json','prepare-profile.py','archive.py']:shutil.copyfile(r/n,out/n)
for n in ['observations.tgz','archive-receipt.json','identity.json','source-identity.json','inventory.json','launch.log','terminal-status.json','observe-not-before.json']:shutil.copyfile(q/n,out/n)
shutil.copyfile(p/'guest-cpu-leaves.json',out/'guest-cpu-leaves.json')
with tarfile.open(out/'preparation.tar.xz','w:xz')as t:
 for p in sorted(q.iterdir()):
  if p.is_file()and p.name not in ['observations.tgz','payload.tgz']:t.add(p,arcname=p.name)
 t.add(r/'catalog.json',arcname='inspection/catalog.json')
 t.add(r.parent/'text-output-batches-r263/prepare-call-gate.py',arcname='inspection/prepare-call-gate.py')
groups={'binding':['::BindFunctionArgumentsInto(','::BindFunctionArguments('],
 'entryAndWork':['::CreateExecutableWorkState(','::BeginBoundCall(','::BeginFunctionCall(','::TryBeginExecutableCall('],
 'dispatch':['::ExecuteInstructionPrefix(']}
totals={k:sum(n for s,n in leaves['topLeafCpuSamples']if any(part in s for part in parts))for k,parts in groups.items()}
(out/'source-decision.json').write_text(json.dumps(dict(revision='983bab96 productionunchanged796c3218',samples=1006,unknown=35,guestCpuSeconds=leaves['cpuSeconds'],executionSeconds=receipt['capturedSeconds'],selectedTop24LeafSamples=totals,
 claim='Current ordinaryhelper binding/entry/work and shared dispatch material; useful/inlined workincluded, no removableCPU/speedup/allocation/causalclaim.',
 source='No duplicatecontext/local/operandarray. Guardedhelper hasif/not, no protectedregion/nativebody; native operations alreadycompleteinborrowedprefix. CurrentCSV/format helpers ordinary; capturedhelpernative.',
 next='Complete ordinary call/body transport source design: name actual bound/work preparation removed across conditional/validation/format helpers before selecting code. Preserve existing measured native paths. No extra opcode or arena presentedas whole redesign; compare prior slower shared-prefix/arena lessons. If coherent removalrequires morethanremainingcostsupports, record concretebarrier and choose constructionstage.',
 acceptance='Native/default/keyword/receiver/descriptor/protocol/cell/error/evaluationorder/realasync suspension, cancellation and terminal admission beforeeffects, retainedaliases and joined cleanup. Focusedactualtargets then bounded completeaffected/control sourcepair. Temporary debt explicit, no fixed%veto.',
 cleanup='Terminalcached, usersjoined and temporarymaps archived/SHAverified before retirement; RAM removed. OnlycommittedB609raw/catalog/records exactduplicates retired under freshUID/path/inode/allaliases/canonicalGitSHA proof; b2cbprotected; no new VMmeasurementpending.',
 qualification='Full correctedsource43166350709 pending independently; publicationheld.'),indent=2)+'\n',encoding='utf-8')
(out/'README.md').write_text('''# Ordinary call/body source gate

2026-11-12, production983bab96/unchanged796c3218, frozenb2cbfdb6 and fixedfec79
worker/optionaladapter0.4. One six-second guarded-search Run profile on quiet
F4as_v7 Ubuntu24.04.4/.NET11RC3, ordinaryGC/tiering plus recorded map/JIT diagnostics.
Full stdout/files, allowance, actualloaded image, quiet-before/after, verified
archive, joinedusers and RAMcleanup pass. Executionclocks5.464s, selected guest
CPU5.09s;1006leaves,35unknown(3.48%), no malformed map lines. No elapsed comparison.

Top24 leaf groups: binding80(7.95%), entry/work91(9.05%), shared dispatch206(20.48%).
These include useful/inlined work, not removable taxes or speedup forecasts. Full
maps/stacks/code, unknowns, threadCPU and output/cleanup evidence survive. The
guarded helper is a simpleif/not, not try/except; its native operations already
complete without a context. Source inspection excludes the proposed duplicate
frame/array removal. Ordinary binding/work preparation and dispatch remain real.

Next: design a coherent ordinary call/body transport deletion across conditional,
validation and formatting helpers, preserving existing native gains and all
Python/cancellation/ownership obligations. Do not select a narrow opcode patch or
revive unchanged slower arenas/shared-prefix designs from leaf shares alone.
Stop at a concrete design/removal proof or an explicit obstacle; bounded justified
refinement remains allowed. This source gate does not complete performance work.

[Source facts](code-facts.json), [assessment](source-gate.md),
[decision](source-decision.json), [raw data/code](observations.tgz).
Full corrected-source platform/stress/package qualification remains independent;
publication is postponed. No live VM users or sole RAM evidence remain.
''',encoding='utf-8')
files={p.relative_to(out).as_posix():sha(p)for p in out.rglob('*')if p.is_file()};(out/'files-sha256.json').write_text(json.dumps(files,indent=2)+'\n',encoding='utf-8')
with Path('.gitattributes').open('a',encoding='utf-8')as f:f.write('\nbenchmarks/results/2026-11-12-ordinary-call-source-gate/** -text whitespace=cr-at-eol\n')
print('Currentordinarycall sourcefacts/1006samples/allunknowns/rawmaps/code/ownership/cleanup archived; no performanceclaim.')
