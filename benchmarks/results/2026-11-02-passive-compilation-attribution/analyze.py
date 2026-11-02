import bisect,collections,gzip,json,re,sys
from pathlib import Path
r=Path(__file__).resolve().parent;f=r/'retrieved/perf/text-compile'
ranges=[];bad=[]
for line in (f/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
 p=line.rstrip().split(' ',2)
 if len(p)!=3 or not line.endswith('\n'):bad.append(line);continue
 ranges.append((int(p[0],16),int(p[1],16),p[2]))
ranges.sort();starts=[x[0] for x in ranges];prefix=[];last=0
for start,size,name in ranges:last=max(last,start+size);prefix.append(last)
ambiguous=collections.Counter();unmapped=collections.Counter()
def symbol(address,fallback):
 i=bisect.bisect_right(starts,address)-1;names=set()
 while i>=0 and prefix[i]>address:
  if address<ranges[i][0]+ranges[i][1]:names.add(ranges[i][2])
  i-=1
 if len(names)==1:return next(iter(names))
 if names:ambiguous[hex(address)]+=1;return '<ambiguous map address>'
 if fallback=='[unknown]':unmapped[hex(address)]+=1
 return fallback

def core(name):
 prefix=name.partition('::')[0];m=re.search(r'\[([^]]+)\]\s+([^[]+)$',prefix)
 return bool(m and m[1]=='Lokad.Lython' and m[2].startswith('Lokad.Lython.') and m[2].count('<')==m[2].count('>'))
assert core('class Lokad.Lython.LythonCompiledScript [Lokad.Lython] Lokad.Lython.LythonEngine::CompileCore(string)')
assert not core('class [Lokad.Lython]Lokad.Lython.Item [Other] External.Host::Run()')
assert not core('void [Lokad.Lython.Benchmarks] Worker::Run(class [Lokad.Lython]Lokad.Lython.Item)')
chains=[];tid=None;frames=[]
for line in gzip.decompress((f/'saved-chains.txt.gz').read_bytes()).decode('utf-8').splitlines()+['']:
 h=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line);m=re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)',line)
 if h:
  if tid is not None:chains.append((tid,frames))
  tid=h[2];frames=[]
 elif m:frames.append(symbol(int(m[1],16),m[2]))
 elif not line.strip() and tid is not None:chains.append((tid,frames));tid=None;frames=[]
perThread=collections.Counter();rootedThread=collections.Counter();leaf=collections.Counter();coreInclusive=collections.Counter();firstCore=collections.Counter();hashOrigins=collections.Counter();rooted=[];syntaxHashChains=[];unknownOrigins=collections.Counter();stageCounts=collections.Counter()
for tid,chain in chains:
 perThread[tid]+=1
 if not any(core(n) and 'Lokad.Lython.LythonEngine::CompileCore(' in n for n in chain):continue
 rooted.append((tid,chain));rootedThread[tid]+=1
 leaf[chain[0] if chain else '<empty>']+=1
 managed=[n for n in chain if core(n)]
 firstCore[managed[0] if managed else '<missing>']+=1
 for n in set(managed):coreInclusive[n]+=1
 if chain and chain[0]=='[unknown]':unknownOrigins[managed[0] if managed else '<missing>']+=1
 if any(core(n) and ('ExpressionSyntax::GetHashCode(' in n or 'LythonSourceSpan::GetHashCode(' in n) for n in chain):syntaxHashChains.append((tid,chain))
 stage=next((key for key in ['SyntaxValidityDiagnostics::Analyze(', 'ImplicitSuperSyntaxFacts::Prepare(', 'AnnotationDiagnostics::Analyze(', 'StaticAbstractInterpreter::Analyze(', 'Parser::Parse(', 'ExecutableScript::Compile(', 'LoweredScript::Lower('] if any(core(n) and key in n for n in chain)), 'other compiler work')
 stageCounts[stage]+=1
 if chain and any(key in chain[0] for key in ['GetHashCode','Equals','String.GetNonRandomizedHashCode']):hashOrigins[tuple(chain[:8])]+=1
result=dict(purpose='Passive CPU stack inventory, no timing or exact causal/removable CPU fraction',allSamples=len(chains),samplesPerThread=perThread,
 compileCoreRootedSamples=len(rooted),rootedPerThread=rootedThread,rootedLeaf=leaf.most_common(),firstCore=firstCore.most_common(),inclusiveCore=coreInclusive.most_common(),
 syntaxHashInclusiveDistinctSamples=len(syntaxHashChains),syntaxHashThroughCacheDistinctSamples=sum(any("AbstractState::TryGetCachedAbstractValue(" in n or "AbstractState::SetCachedAbstractValue(" in n for n in chain) for tid,chain in syntaxHashChains),unknownFirstCore=unknownOrigins.most_common(),stageSamples=stageCounts,hashLeafOrigins=[dict(frames=list(k),samples=n) for k,n in hashOrigins.most_common()],ambiguousAddresses=ambiguous,unknownAddresses=unmapped,malformedMapLines=bad)
(r/'native-inventory.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:result[k] for k in ['allSamples','samplesPerThread','compileCoreRootedSamples','rootedPerThread']}))
print('TOP LEAF',json.dumps(result['rootedLeaf'][:12]));print('TOP CORE',json.dumps(result['inclusiveCore'][:12]))
