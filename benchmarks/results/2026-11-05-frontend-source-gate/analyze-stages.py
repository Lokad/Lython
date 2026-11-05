from pathlib import Path
import bisect, collections, json, re

root=Path(__file__).resolve().parent
groups={
 'tokenization':('PythonTokenization','TokenizedSource'),
 'parser':('Frontend.Parser',),
 'syntax validity':('SyntaxValidityDiagnostics',),
 'annotation text/diagnostics':('PostponedAnnotationText','AnnotationDiagnostics','ModernTypeSyntaxFacts'),
 'lexical preparation':('LexicalScopePreparation',),
 'scope facts':('ScopeDirectiveFactsCollector',),
 'static binding/analysis':('StaticAbstractInterpreter','StaticBindingEngine','StaticAnalyzer','StaticAnalysisContext'),
 'static name/scope diagnostics':('StaticNameBindingDiagnostics','StaticScopeDirectiveDiagnostics'),
 'lowering':('LoweredScript',),
 'executable construction':('ExecutableScript','ExecutableStackLayout','ExecutableCodeObject'),
}
out=[]
for name in ['text','report']:
 p=root/'retrieved/perf'/name
 record=json.loads((p/'record.json').read_text(encoding='utf-8'))
 tids=set(record['guestTids']); ranges=[]
 for line in (p/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
  parts=line.rstrip().split(' ',2)
  if len(parts)==3 and line.endswith('\n'):ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
 ranges.sort(); starts=[r[0] for r in ranges]
 def symbol(ip,fallback):
  i=bisect.bisect_right(starts,ip)-1
  return ranges[i][2] if i>=0 and ip<ranges[i][0]+ranges[i][1] else fallback
 chains=[];tid=None;frames=[]
 for line in (p/'perf-stacks.txt').read_text(encoding='utf-8').splitlines()+['']:
  header=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line)
  frame=re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)',line)
  if header:tid=header[2];frames=[]
  elif frame and tid in tids:frames.append(symbol(int(frame[1],16),frame[2]))
  elif not line.strip() and tid is not None:
   if tid in tids:chains.append(frames)
   tid=None;frames=[]
 inclusive=collections.Counter(); leaves=collections.Counter();callers=collections.Counter();walks=collections.Counter()
 for c in chains:
  owners=[]
  for f in c:
   match=re.search(r'\[Lokad\.Lython\]\s+(Lokad\.Lython\.[^:\s]+)::',f)
   if match:owners.append(match[1])
  for group,keys in groups.items():
   if any(any(k in owner for k in keys) for owner in owners):inclusive[group]+=1
  if c:
   leaves[c[0]]+=1
   if 'ExpressionSyntaxTraversal' in c[0]:
    callers[' -> '.join(c[1:6])]+=1
   for f in set(f for f in c if '[Lokad.Lython]' in f):walks[f]+=1
 result=dict(case=name,samples=len(chains),empty=sum(not c for c in chains),oneFrame=sum(len(c)==1 for c in chains),
  inclusiveOverlapping=dict(inclusive),topInclusive=walks.most_common(25),expressionTraversalLeafCallers=callers.most_common(),
  note='Current source attribution, incomplete frame-pointer chains and unknown leaves retained. Overlapping stages are not removable work or predicted speedup. No comparison/heap/ownership-balance claim.')
 out.append(result)
 print(name,'samples',len(chains),'inclusive',dict(inclusive))
 print('Expression traversal callers:')
 for c,n in callers.most_common(6):print(n,c)
 print('Top inclusive:')
 for c,n in walks.most_common(14):print(n,c)
(root/'stage-attribution.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf-8')
