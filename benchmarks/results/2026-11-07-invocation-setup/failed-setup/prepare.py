from pathlib import Path
import hashlib,json,shutil,tarfile
r=Path(__file__).parent;home=r.parent;old=home/'report-cost-r52'
assert not(r/'payload.tgz').exists()
focused=json.loads((home/'lexical-nesting-r55/focused.json').read_text(encoding='utf-8'))
for p,h in focused['productionSources'].items():assert hashlib.sha256(Path(p).read_bytes()).hexdigest()==h
catalog=json.loads((home/'wildcard-utf8-r51/b409/representative.json').read_text(encoding='utf-8'))
catalog['cases']=[next(c for c in catalog['cases']if c['id']=='scripts.config-edit.small')]
(r/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
identity=dict(sourceRevision='7df9b465 production, documentationa138c769; exact focused source/core inventory',files={'Lokad.Lython.dll':focused['cores']['net11.0']},auxiliaryFiles=json.loads((old/'identity.json').read_text(encoding='utf-8'))['auxiliaryFiles'],productionSources=focused['productionSources'],qualification='Focused actual.NET11/10 and matching pathological subprocess gates pass. Full CI42397553014 independent/pending at preparation. No producer/package promotion.',purpose='R56 complete small configuration invocation setup CPU attribution, not a source-pair speedup or normal-runtime timing; inspect all threads before selecting a redesign.')
(r/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8',newline='\n')
shutil.copy2(home/'lexical-nesting-r55/cores/net11.0/Lokad.Lython.dll',r/'current-core.dll')
name='invocation-setup-b413-20261107';unit='lython-b413-invocation-setup-20261107'
def adapt(s):return s.replace('report-cost-b410-20261107',name).replace('lython-b410-report-cost-20261107',unit).replace('report-cost-b410-launch.py','invocation-setup-b413-launch.py').replace('lython-b410-fixtures','lython-b413-fixtures').replace('report-warm','config-warm')
s=adapt((old/'ops-profile.py').read_text(encoding='utf-8')).replace('Initial current synchronous text CPU capture','R56 current complete small configuration invocation CPU capture').replace('scripts.report-render.large','scripts.config-edit.small')
s=s.replace('TryExecuteFormattedInstruction AppendInterpolatedStringPart AppendInterpolatedStringValue TryAppendNumericValue CreateExecutableWorkState','CreateBuiltinVariables CreateModuleVariables InitializeExecutableExecution CreateSuccessfulResult CloseResources')
s=s.replace('count = 1  # Deadline bounded; each request remains a complete checked job.','count = 32  # Each invocation remains a complete checked small configuration job.')
s=s.replace('512 if lane == "compile" else 1024','512 if lane == "compile" else 4096').replace('batches.append(request("batch",1))','batches.append(request("batch",count))')
assert 'batches.append(request("batch",count))'in s
(r/'ops-profile.py').write_text(s,encoding='utf-8',newline='\n');compile(s,'ops-profile','exec')
for name in ['run.sh','archive.py','retrieve.py']:
 s=adapt((old/name).read_text(encoding='utf-8'));(r/name).write_text(s,encoding='utf-8',newline='\n')
shutil.copy2(old/'analyze-cpu-guest.py',r/'analyze-cpu-guest.py')
s=adapt((old/'analyze-chains.py').read_text(encoding='utf-8'));(r/'analyze-chains.py').write_text(s,encoding='utf-8',newline='\n')
contract='''R56/B413 bounded source/cost gate, no runtime prototype. One maintained config-edit.small (181B/16paths), compiled once/warm full execution, efficient mediated RAM-file I/O/full stdout/file goldens/prior-result cleanup. Compile/startup/staging/reset/final cached teardown excluded; CPU profile cannot isolate wall I/O latency. Source shows run-local class owners and constructor/exception descriptor caches: do not blindly share mutable builtin graphs. Hypothesis: avoiding unused definition construction could remove a material initialization phase; profile must distinguish it from I/O/cleanup/actual guest work. Existing R55 exact source/core, unchanged normal0.4 worker/auxiliaries; R55 full CI independent.6guest-second warmup(max64*4096jobs),2ssettle,6swall199Hz CPU/FP,32full jobs per sampling request rather than transport-dominated singles. All threads/raw stacks/maps/unknowns/inlining limits retained. Diagnostic PerfMap3/five JIT listings declared; no normal-runtime speedup claim. Minimum256MiB persistent headroom; raw records/logs persistent, owned separate128MiB tmpfs guest fixtures only. Core/aux/actualReady identities, pre/post full goldens, quiet/Closed/exit/join/emptyfixture/archive-under-lease/localverification/unmount gates required.120sowner+30sstop+30smargin; observe180s, no early service probing/restart on timeout. Stop after selecting one complete removable phase or recording insufficient attribution; no broad sweep, prototype, predicted percentage or package publication.'''
(r/'contract.md').write_text(contract+'\n',encoding='utf-8',newline='\n');shutil.copy2('PLAN.md',r/'pre-measurement-plan.md')
s=adapt((old/'launch.py').read_text(encoding='utf-8'))
a=s.index('previous=json.loads');b=s.index('payload=',a);s=s[:a]+s[b:]
oldline=" (root/'candidate').symlink_to(source,target_is_directory=True)"
assert oldline in s
s=s.replace(oldline,""" target=root/'candidate';target.mkdir()
 for p in source.iterdir():
  if p.is_file()and p.name!='Lokad.Lython.dll':os.link(p.resolve(),target/p.name)
 (root/'current-core.dll').rename(target/'Lokad.Lython.dll')""")
(r/'launch.py').write_text(s,encoding='utf-8',newline='\n');compile(s,'launch','exec')
for name in ['archive.py','retrieve.py']:compile((r/name).read_text(encoding='utf-8'),name,'exec')
with tarfile.open(r/'payload.tgz','w:gz')as a:
 for name in ['identity.json','catalog.json','current-core.dll','contract.md','pre-measurement-plan.md','ops-profile.py','analyze-cpu-guest.py','archive.py','run.sh']:a.add(r/name,arcname=name)
print(json.dumps(dict(core=identity['files']['Lokad.Lython.dll'],payloadBytes=(r/'payload.tgz').stat().st_size,case=catalog['cases'][0]['id'],ownerSeconds=120,observeAfterSeconds=180)))
