from pathlib import Path
import bisect,collections,json,re
r=Path(__file__).parent;output=[]
for p in sorted((r/'retrieved/profiles').iterdir()):
 if not (p/'perf-stacks.txt').exists():continue # Failed search warmup has no captured samples; preserved separately.
 rec=json.loads((p/'record.json').read_text(encoding='utf8'));threads={t['tid']:t for t in rec['threadCpuSeconds']};ranges=[]
 for line in (p/'perf.map').read_text(encoding='utf8').splitlines():
  row=line.split(' ',2)
  if len(row)==3:ranges.append((int(row[0],16),int(row[1],16),row[2]))
 ranges.sort();starts=[v[0]for v in ranges]
 def symbol(address,fallback):
  i=bisect.bisect_right(starts,address)-1
  return ranges[i][2]if i>=0 and address<ranges[i][0]+ranges[i][1]else fallback
 def group(tid):
  name=threads.get(tid,{}).get('name','<unknown>')
  if name.startswith('Lython executio'):return'guest'
  if name.startswith('.NET Tiered'):return'tiering'
  if name in {'.NET BGC','.NET Finalizer'}or 'GC'in name:return'collection/finalizer'
  return'caller/other'
 counts=collections.defaultdict(collections.Counter);leafs=collections.defaultdict(collections.Counter);phase_leafs=collections.defaultdict(lambda:collections.defaultdict(collections.Counter));frames=[];tid=None
 phases=[('call binding',['::BindFunctionArguments','FunctionBindingPlan::','::BindInto','::BindPositionalFunctionArguments','::RentFunctionArgumentStorage']),('invocation frame',['::EnterInvocationFrame','PyFunctionBinding::','::BeginBoundCall']),('CSV parser',['CsvRecordParser::']),('CSV records',['CsvDictReaderObject::','CsvDictReaderObject+','RecordKeys::','RecordStorage::']),('CSV source',['CsvRecordSource::','CsvRecordSource+']),('fixture reset',['ComparisonFileHost::Reset']),('validation',['ComparisonFileHost::ReadOutputs']),('protocol',['ComparisonProtocol::','ComparisonProtocol+']),('builtin initialization',['ExecutionContext::CreateBuiltinVariables']),('native guard',['::GuardNativeReturn','::GuardNativeBody']),('native evaluation',['::EvaluateNativeReturn']),('native call preparation',['::TryExecuteNativeCall']),('operand/result publication',['ExecutableValueStorage::','ExecutableValueStack::','::CompleteLocalResult','::PublishInstructionResult']),('VM prefix',['::ExecuteInstructionPrefix']),('memory admission',['MemoryGovernor::','LythonMemoryScope::','LythonMemoryBudget::']),('host/filesystem',['ComparisonFileHost::','ComparisonFileHost+']),('JSON',['JsonModule::','JsonModule+','JsonEncodeIterator::','::JsonLoad','::JsonDump','::DeserializeJson','::SerializeJson']),('text reader',['ChunkedTextFileReadState::']),('context initialization',['ExecutionContext::.ctor']),('text kernels',['PyStringOps::','PyString::','::ExecuteCasedSplit']),('dictionary/subscript',['PyDict::','PyDict+','::InvokeDictionaryGet','::SetSubscriptValue']),('scalar operators',['::EvaluateBinaryOperator','::EvaluateAugmentedAssignment','::EvaluateAdd','::AddExactIntegers','::TryCompareScalarOperands']),('managed cancellation/stack',['ExecutionState::CheckExecution','StackRuler::','::EnterInterpreterFrame'])]
 def finish():
  if not frames or tid is None:return
  g=group(tid);leafs[g][frames[0]]+=1
  # One innermost observed category per sample, not additive inclusive costs.
  hits=[(i,phase)for i,f in enumerate(frames)for phase,keys in phases if any(k in f for k in keys)]
  phase=min(hits)[1]if hits else'unassigned';counts[g][phase]+=1;phase_leafs[g][phase][frames[0]]+=1
 for line in (p/'perf-stacks.txt').read_text(encoding='utf8').splitlines():
  header=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line)
  if header:finish();tid=header[2];frames=[];continue
  m=re.match(r'\s+([0-9a-f]+)\s+(.*)',line)
  if m:frames.append(symbol(int(m[1],16),m[2]))
 finish()
 row=dict(case=rec['workload'],groups={g:dict(samples=c.total(),unknownLeaf=leafs[g]['[unknown]'],observedPhase=c.most_common(),topLeaf=leafs[g].most_common(16),phaseLeaf={phase:leaves.most_common(20)for phase,leaves in phase_leafs[g].items()})for g,c in counts.items()},threadCpuSeconds=rec['threadCpuSeconds'],correction='Collector selects actual named Lython execution workers; all threads and raw stacks retained. Offline attribution uses actual names and separates caller/tiering/collection. Two current-qualified CSV/text captures; operand classifier recognizes actual ExecutableValueStorage name; offline classification only, no elapsed comparison.',limits='Whole-batch sampling is wider than execution clocks and includes reset/protocol/validation. Phase shares overlap parent work only through assignment to innermost observed category; inlining/truncation/unknown IPs limit attribution. Native guard/evaluation includes useful work, not a removable tax.')
 output.append(row)
 print(row['case'])
 for g,d in row['groups'].items():
  print(g,'samples',d['samples'],'unknown',d['unknownLeaf'],'phases',d['observedPhase'][:9])
(r/'attribution.json').write_text(json.dumps(output,indent=2)+'\n',encoding='utf8')
