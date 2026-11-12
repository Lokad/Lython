from pathlib import Path
import hashlib,json,shutil,tarfile
r=Path(__file__).resolve().parent;q=r/'b609';d=Path('benchmarks/results/2026-11-12-output-cpython-checkpoint');d.mkdir(exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'));identity=json.loads((q/'identity.json').read_text(encoding='utf-8'))
assert receipt['complete']and receipt['jobs']==12 and receipt['allWorkersJoined']and receipt['guestFixturesRemoved']and receipt['quietBeforeAndAfter']and receipt['archivedAndVerifiedUnderLease']
assert sha(q/'observations.tgz')==receipt['archiveSHA256']
with tarfile.open(q/'observations.tgz')as t:assert hashlib.sha256(t.extractfile('current-core.dll').read()).hexdigest()==identity['coreSHA256']['candidate']
raw=json.loads((q/'retrieved/confirmation/confirmation.json').read_text(encoding='utf-8'))
for job in raw['jobs']:
 assert job['workersJoinedAndFixturesRemoved']
 for role,v in job['identities'].items():
  if not role.startswith('CPython'):assert next(x['sha256'].lower()for x in v['libraries']if Path(x['path']).name=='Lokad.Lython.dll')==identity['coreSHA256'][role]
shutil.copyfile('docs/BENCHMARKS.md',d/'prior-BENCHMARKS.md')
for p in q.iterdir():
 if p.is_file()and p.name not in ['payload.tgz','representative.json']:shutil.copyfile(p,d/p.name)
for name in ['prepare-checkpoint.py','preserve-checkpoint.py']:shutil.copyfile(r/name,d/name)
(d/'README.md').write_text('''# Complete output and text CPython checkpoint

2026-11-12, retained983bab966f13d4575b514eb20dd237a12159bc65, frozen coreb2cbfdb6
with precommit assembly metadata; all568production source bytes identify the
measured image. Test-only correction796c3218 leaves production unchanged.
Fixed fec79afd worker/normal optional adapter0.4, stock CPython3.13.16, quiet
Azure F4as_v7/Ubuntu24.04.4/actual.NET11RC3, ordinary GC/tiering.

Twelve jobs complete325.440s: UTF8/UTF16 Markdown reports, captured-helper text
and ordinary text control; separate compile/warm/compile-to-result. Both engines
run identical source per case. Two independent processes per engine,5s warmup,
seven rotating150ms rounds. All output/file/allowance/identity/quiet/join/RAM/
persistent-archive gates pass, localSHA verified. Terminal cached; no users left.

Warm Lython/CPython ratios: UTF8report1.26–1.33x, UTF16report1.44–1.51x,
capturedtext1.62–1.67x, ordinarytext0.95–1.11x. These warm observations are variable,
not confidence intervals or equivalence. Compilation is1.07–1.09x for reports,
0.83–0.84x captured,1.07x ordinary. Compile-to-result1.26–1.29x UTF8,
1.38–1.40x UTF16,1.53–1.56x captured,0.99–1.03x ordinary. All adverse controls,
IQRs and widerCPU survive in summary/raw evidence. Different collections cannot
attribute a gain to one runtime change or remove prior different-base debts.

Warm/result include public invocation, mediated RAM-file I/O, complete stdout/file
validation and prior-result cleanup. Exclude startup, compile in warm, fixture
reset/staging, protocol and final cache disposal. Compile includes construction/
disposal under shared allowance. Wider processCPU includes reset/protocol. No RSS,
pending-I/O/embedding-tail/large-file throughput claim. All old guide rows remain
in prior-BENCHMARKS; unrefreshed workloads retain their older revision labels.

Full qualification of983bab96 first exposed two obsolete flush-copy assertions
on Ubuntu; correction now tests replacement-batch refusal before allocation or
publication using a smaller input. Original failures/10checks peractualtarget
are preserved separately. Corrected fullCI43166350709 pending at this collection;
no full-delivery claim follows from these measurements. Publication/tagging held.

Only exact committed B608raw/catalog/records were retired under fresh idlelease,
UID/path/inode/allaliases/canonicalGitSHA proof. Both selected core generations
protected; OS/evidence floors unchanged. Free260861952B after finalarchive.
''',encoding='utf-8')
p=Path('docs/BENCHMARKS.md');s=p.read_text(encoding='utf-8');data=json.loads((q/'summary.json').read_text(encoding='utf-8'));rows={(x['case'],x['lane']):x for x in data['timing']}
mapping={'Text summary/helpers':'scripts.text-summary-helpers.large','Text / captured helper + counter':'scripts.text-summary-helpers.nonlocal-counter.large','JSON-to-Markdown report':'scripts.report-render.large'}
seen={k:0 for k in mapping};output=[]
def row(label,case,lane,hasInput):
 x=rows[case,lane];fmt=lambda v:f'{min(v):.3f}–{max(v):.3f} ms';parts=[label]
 if hasInput:parts+=['3,072 lines /35,840 B'if case.startswith('scripts.text')else'3,072 records /189,441 B']
 parts += [fmt(x['timeRangeMilliseconds']),fmt(x['pythonTimeRangeMilliseconds']),f"{min(x['ratioRange']):.2f}–{max(x['ratioRange']):.2f}×",'`983bab96`',f"{x['status']}; L{x['controlPercent']:.2f}%, P{x['pythonControlPercent']:.2f}% / {max(x['iqrPercent'].values()):.2f}%"]
 return '| '+' | '.join(parts)+' |'
for line in s.splitlines():
 label=next((k for k in mapping if line.startswith('| '+k+' |')),None)
 if label:
  i=seen[label];seen[label]+=1;lane=['compile-run','warm','compile'][i];output.append(row(label,mapping[label],lane,i==0))
  if label=='JSON-to-Markdown report':output.append(row('JSON-to-Markdown / UTF-16 output','scripts.report-render.utf16.large',lane,i==0))
 else:output.append(line)
assert all(v==3 for v in seen.values());s='\n'.join(output)+'\n'
a=s.index('**Measured sources:**');b=s.index('Earlier rows remain historical:',a)
s=s[:a]+'''**Measured sources:** `983bab96` for UTF8/UTF16 reports and both text shapes,
collected2026-11-12, frozen core `b2cbfdb6`, exact568production source hashes.
Full corrected-source qualification is pending; the fixture-only followup leaves
production unchanged. `82ed92d4` remains the two helper-search rows; `2ec9d67f`
remains CSV validation/JSON-lines. Both older runtimes passed full Windows/Ubuntu
actual.NET11/10, original stress and installed Release consumers. All run against
stock CPython3.13.16 on the same quiet VM. Sources are labelled per row.
''' +s[b:]
a=s.index('The later [cell-assignment source pair]');b=s.index('The captured-helper variant',a)
s=s[:a]+'''The [current four-script checkpoint](../benchmarks/results/2026-11-12-output-cpython-checkpoint/README.md)
includes separate compilation, warm execution and compile-to-result, with all
output/ownership/quiet/join/archive gates passing. Reports and captured text retain
material warm gaps; ordinary text is variable. Older measurements and replaced
guide rows remain linked from the evidence. Separate collections cannot attribute
a gain to an individual change.

''' +s[b:]
a=s.index('five-second warmups per role/job');b=s.index('These observations do not qualify',a)
s=s[:a]+'''five-second warmups per role/job and seven rotated150ms rounds. Four current
scripts use b2cbfdb6/983bab96; search keeps0106a711/82ed92d4, CSV validation and
JSON-lines keepc9829194/2ec9d67f. All reuse common workerfec79afd and adapter0.4.
Other rows retain recorded images.
''' +s[b:]
p.write_text(s,encoding='utf-8')
p=Path('benchmarks/EXPERIMENTS.md');s=p.read_text(encoding='utf-8');idx=s.index('[Final-byte text storage/batching]')
s=s[:idx]+'''[Current output/text CPython checkpoint](results/2026-11-12-output-cpython-checkpoint/README.md)
completes12jobs325.440s, allgates/join/RAM/archive pass. Retained983bab96 warm
UTF8report1.26–1.33x, UTF16report1.44–1.51x, capturedtext1.62–1.67x; ordinarytext
0.95–1.11x variable. Compile/compile-to-result separate; no singlechange
attribution or equivalence. Original flush-copy CI failures and fixture-only
correction survive. Full correctedhead qualification pending, publication held.

'''+s[idx:];p.write_text(s,encoding='utf-8')
files={p.relative_to(d).as_posix():sha(p)for p in d.rglob('*')if p.is_file()};(d/'files-sha256.json').write_text(json.dumps(files,indent=2)+'\n',encoding='utf-8')
with Path('.gitattributes').open('a',encoding='utf-8')as f:f.write('\nbenchmarks/results/2026-11-12-output-cpython-checkpoint/** -text whitespace=cr-at-eol\n')
print('Four current scripts/separatephases, exactevidence/adversecontrols and previousguide archived; finaldelivery stillpending.')
