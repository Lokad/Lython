from pathlib import Path
import collections,hashlib,json,tarfile
r=Path(__file__).parent;r.mkdir(exist_ok=True);root=Path('D:/code/Lython');prior=r.parent/'record-pipeline-r484'
source=(prior/'analyze.py').read_text(encoding='utf-8');stop=source.index(' leaves=collections.Counter')
ns={'__file__':str(prior/'analyze.py')};exec(compile(source[:stop],str(prior/'analyze.py'),'exec'),ns)
samples=ns['samples'];old=json.loads((root/'benchmarks/results/2026-11-17-json-record-costs/attribution.json').read_text(encoding='utf-8'))['rows'][0]
nearest=collections.Counter(next((n for n in s if 'Lokad.Lython.'in n),'[no Lython frame]')for s in samples if s)
assert len(samples)==old['sampleCount']==986 and dict(nearest)==dict(old['nearestLythonFrames'])
def has(stack,parts):return any(any(p in frame for p in parts)for frame in stack)
locks=[(i,s)for i,s in enumerate(samples)if s and 'System.Threading.Monitor::'in s[0]and ('::Enter('in s[0]or '::Exit('in s[0])]
reserve=[(i,s)for i,s in enumerate(samples)if has(s,['MemoryGovernor::Reserve('])]
helpers=[(i,s)for i,s in enumerate(samples)if has(s,['PyExecutableFunction::StartNativeCall','PyExecutableFunction::StartInline'])]
prefix=[(i,s)for i,s in enumerate(samples)if s and '::ExecuteInstructionPrefix('in next((n for n in s if'Lokad.Lython.'in n),'')]
files=['src/Lokad.Lython/Runtime/MemoryGovernor.cs','src/Lokad.Lython/Public/LythonMemoryBudget.cs']
with tarfile.open(root/'benchmarks/results/2026-11-17-utf8-widths/refined/candidate-sources.tar.xz')as archive:
 for name in files:assert archive.extractfile(name).read().decode('utf-8-sig').replace('\r\n','\n')==(root/name).read_text(encoding='utf-8-sig')
v=dict(evidence='Existing R484/B901 a95 qualified capture, no new VM capture/prototype/timing; source comparison confirms governor/default allowance unchanged in00adcc8a.',sampleCount=len(samples),unknownLeaves=old['unmappedLeafSamples'],nearestInstructionPrefix=len(prefix),instructionPrefixWithVisibleHelperActivation=sum(has(s,['PyExecutableFunction::StartNativeCall','PyExecutableFunction::StartInline'])for i,s in prefix),helperActivationInclusive=len(helpers),reserveInclusive=len(reserve),monitorLeaf=len(locks),monitorLeafWithReserve=sum(has(s,['MemoryGovernor::Reserve('])for i,s in locks),monitorLeafWithoutReserve=[dict(sample=i,frames=s[:7])for i,s in locks if not has(s,['MemoryGovernor::Reserve('])],lockLeafMethods=collections.Counter(s[0]for i,s in locks),sourceSHA256={name:hashlib.sha256((root/name).read_bytes()).hexdigest()for name in files},reserveSampleIndices=[i for i,s in reserve],monitorSampleIndices=[i for i,s in locks],limitations='Sampling/inlining/tailcalls prevent exact call frequency or removable-time attribution. Prefix samples include module work; not a demonstrated function-frame share. No causal speedup or capacity-policy promotion.')
(r/'source-gate.json').write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({k:v[k]for k in ['sampleCount','unknownLeaves','nearestInstructionPrefix','instructionPrefixWithVisibleHelperActivation','helperActivationInclusive','reserveInclusive','monitorLeaf','monitorLeafWithReserve']},indent=2))
