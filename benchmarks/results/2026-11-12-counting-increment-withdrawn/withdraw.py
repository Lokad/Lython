from pathlib import Path
import json,hashlib,shutil,subprocess,tarfile
r=Path('.git/agent-notes/review-20261031/native-counting-r257');d=Path('benchmarks/results/2026-11-12-counting-increment-withdrawn');d.mkdir()
for n in ['before.json','diagnostic-identity.json','sources-candidate.tar.xz','internal11.log','public11.log','internal10.log','public10.log','freeze-and-measure.py','summarize.py','catalog.json']:shutil.copyfile(r/n,d/n)
(d/'implementation.patch').write_bytes(subprocess.check_output(['git','diff','--','src/Lokad.Lython/Runtime/PyDict.Accumulation.cs','src/Lokad.Lython/Runtime/LythonRuntime.Executable.DictionaryAccumulation.cs','tests/Lokad.Lython.Tests/DictionaryIntegerValueTests.cs']))
with tarfile.open(d/'candidate-core.tar.xz','w:xz')as a:a.add(r/'candidate/Lokad.Lython.dll',arcname='Lokad.Lython.dll')
(d/'tool').mkdir()
for n in ['complete-jobs.csproj','Program.cs']:shutil.copyfile(r/'tool'/n,d/'tool'/n)
for phase in ['warm-sync','warm-async']:
 (d/phase).mkdir()
 for p in(r/phase).iterdir():
  if p.is_file():shutil.copyfile(p,d/phase/p.name)
readme='''# Primitive counting increment — withdrawn source gate

2026-11-12, prototype over82ed92d4, not retained. Two production files pass a
bounded constant as long through the existing dictionary accumulation service,
removing repeated BigInteger struct transport/width/cast. The compiler already
proves this constant's range; the ordinary caller validates dynamic increments.
Existing values, promotion and effects remain unchanged in focused checks.

90internal/197public Release tests pass per actual .NET11/10 (574total). Three
complete text/report scripts, both APIs,64warmups/5rotated rounds8jobs preserve
full stdout/files, allowance and cleanup. Allocation flat. Ordinary text elapsed
-9.55..-9.07%Run(control0.52%),-4.12..-2.94%Async(control1.23%) is diagnostic.
Captured Run controls10.36%, capturedAsync+12.18..+25.47%with10.59%control;
reportRun+5.13..+14.75%with9.15%control, reportAsync-2.90..+12.03%with13.33%control.
No quiet VM collection, CPU claim, full qualification or publication.

Withdraw for the contract tradeoff: a previously checked internal integer argument
becomes a plain long with an implicit signed-seven-byte precondition. Enforcing it
inside the service recreates the removed check; a new wrapper type would add
machinery. This tiny change does not constitute the intended coherent redesign.
The original constant was already unboxed before the loop; remaining work is struct
transport/width/conversion, not a per-word heap box. No allocation saving is claimed.
Timing uncertainty is not a failed design or permanent ban; retain all observations.

Exact82ed production is restored from saved physical source, tests from Git;
nonincremental rebuild and focused restoration follow separately. No dormant new
signature or range contract remains. Continue with the complete ordinary helper
binding/workstate/pending-call lifecycle, whose guarded-script CPython gap remains.
'''
(d/'README.md').write_text(readme,encoding='utf-8')
for n,h in json.loads((r/'before.json').read_text(encoding='utf-8'))['files'].items():
 assert hashlib.sha256((r/'originals'/n).read_bytes()).hexdigest()==h
 shutil.copyfile(r/'originals'/n,n)
p=Path('tests/Lokad.Lython.Tests/DictionaryIntegerValueTests.cs');p.write_bytes(subprocess.check_output(['git','show','HEAD:'+p.as_posix()]))
meta=json.loads(Path('.git/agent-notes/review-20261031/receiver-operations-r256/refined/diagnostic-identity.json').read_text(encoding='utf-8'))
for n,h in meta['sourceHashes'].items():assert hashlib.sha256(Path(n).read_bytes()).hexdigest()==h,n
(r/'restoration-source-proof.json').write_text(json.dumps(dict(exactProductionRestored=True,revision='82ed92d422a947726fdc802108cd0ffebbaa5472',sourceFiles=len(meta['sourceHashes']),candidateArchived=True,forcedBuildPending=True),indent=2)+'\n',encoding='utf-8')
shutil.copyfile(r/'restoration-source-proof.json',d/'restoration-source-proof.json')
p=Path('.gitattributes');s=p.read_text(encoding='utf-8');s+='\nbenchmarks/results/2026-11-12-counting-increment-withdrawn/** -text whitespace=cr-at-eol\nbenchmarks/results/2026-11-12-counting-increment-withdrawn/**/*.patch -text -whitespace\n';p.write_text(s,encoding='utf-8')
p=Path('benchmarks/EXPERIMENTS.md');s=p.read_text(encoding='utf-8');a=s.index('[Affected CPython');s=s[:a]+'''[Primitive counting increment](results/2026-11-12-counting-increment-withdrawn/README.md)
is withdrawn.574focused checks and three complete scripts bothAPIs pass, allocation
flat, diagnostic elapsed mixed with adversity preserved. Replacing a checked
BigInteger argument with a plain long creates an implicit range precondition;
restoring enforcement recreates the check, a new wrapper adds machinery. The
constant was already unboxed outside the loop. This small contract tradeoff does
not justify retention or a quiet campaign. Exact82ed production restored; continue
with the complete ordinary helper lifecycle rather than another local threshold.

'''+s[a:];p.write_text(s,encoding='utf-8')
print('Prototype, binaries,574checks and all diagnostic adversity archived; exact82ed production restored.')
