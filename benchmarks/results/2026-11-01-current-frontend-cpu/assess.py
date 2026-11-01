from pathlib import Path
import bisect,collections,gzip,hashlib,json,re

root=Path(__file__).resolve().parent
folder=root/'retrieved/perf/csv-compile'
record=json.loads((folder/'record.json').read_text())
entries=[]
for line in (folder/'perf.map').read_text().splitlines(keepends=True):
    parts=line.rstrip().split(' ',2)
    if len(parts)==3 and line.endswith('\n'):entries.append((int(parts[0],16),int(parts[1],16),parts[2]))
entries.sort();starts=[e[0] for e in entries]
def symbol(address,fallback):
    i=bisect.bisect_right(starts,address)-1
    return entries[i][2] if i>=0 and address<entries[i][0]+entries[i][1] else fallback
raw=gzip.open(folder/'saved-chains.txt.gz','rt').read()
chains=[];current=None;frames=[]
for line in raw.splitlines()+['']:
    header=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line)
    stack_frame=re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)',line)
    if header:current=header[2];frames=[]
    elif stack_frame and current==record['guestTid']:
        frames.append(symbol(int(stack_frame[1],16),stack_frame[2]))
    elif not line.strip() and current is not None:
        if current==record['guestTid']:chains.append(frames)
        current=None;frames=[]
groups={
    'parsing':('Frontend.Parser::','Frontend.Parser+'),
    'tokenization':('PythonTokenization',),
    'static analysis':('StaticAnalyzer::','StaticAbstractInterpreter','StaticBindingEngine::','StaticControlFlowAnalyzer::'),
    'executable construction':('ExecutableScript::Compile','ExecutableScript+'),
    'lowering':('LoweredScript::Lower','LoweredScript+'),
    'scope preparation':('ImplicitSuperSyntaxFacts::','ScopeDirectiveFacts::'),
    'syntax validation':('SyntaxValidityDiagnostics::',),
    'postponed annotations':('PostponedAnnotationText::',),
    'mutation analysis':('CollectMutatedReceiverNames','AssignmentMayMutateSequenceFacts'),
    'syntax traversal':('ExpressionSyntaxTraversal','StatementSyntaxTraversal'),
    'abstract cache':('AbstractState::TryGetCachedAbstractValue','AbstractState::SetCachedAbstractValue'),
}
counts={k:sum(any(any(needle in frame for needle in needles) for frame in chain) for chain in chains)
        for k,needles in groups.items()}
all_frames=collections.Counter(frame for chain in chains for frame in set(chain) if '[Lokad.Lython]' in frame)
assert record['exitCode']==0 and record['verificationBefore']['status']=='Equivalent' and record['verificationAfter']['status']=='Equivalent'
result=dict(source='44e29e05 + B44 source patch/new files; same frontend as a0eae244',
    question='Current compiler passes after Python tokenization, not warm runtime attribution',
    captureSeconds=record['response']['elapsedTicks']/record['response']['clockFrequency'],
    count=record['count'],compilerTid=record['guestTid'],compilerThreadEvidence=record['compilerThreadEvidence'],
    samples=len(chains),emptyChains=sum(not c for c in chains),inclusiveGroups=counts,
    allManagedInclusiveFrames=all_frames.most_common(),allThreadCpu=record['threadCpuSeconds'],
    note='Inclusive groups overlap; inlined/missing frames prevent treating counts as exact or removable fractions.',
    performanceQualified=False,rawSHA256=hashlib.sha256((root/'raw-capture.tar.gz').read_bytes()).hexdigest())
(root/'assessment.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k not in ('allManagedInclusiveFrames','allThreadCpu')},indent=2))
print(json.dumps(result['allManagedInclusiveFrames'][:14],indent=2))
