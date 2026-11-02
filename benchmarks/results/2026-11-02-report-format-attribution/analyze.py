"""Offline report-format attribution from the single saved capture; no execution."""
from pathlib import Path
import bisect,collections,gzip,json,re,statistics,sys
root=Path(sys.argv[1]).resolve(); folder=root/'retrieved/perf/report-render'
record=json.loads((folder/'record.json').read_text(encoding='utf-8'))
ranges=[];malformed=[]
for line in (folder/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
 parts=line.rstrip().split(' ',2)
 if len(parts)!=3 or not line.endswith('\n'):malformed.append(line);continue
 ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
ranges.sort();starts=[r[0] for r in ranges];prefix=[];end=0
for start,size,_ in ranges:end=max(end,start+size);prefix.append(end)
ambiguous={}
def symbol(address,fallback):
 hi=bisect.bisect_right(starts,address);lo=bisect.bisect_right(prefix,address)
 names={r[2] for r in ranges[lo:hi] if address<r[0]+r[1]}
 if len(names)>1:ambiguous[hex(address)]=sorted(names);return fallback
 return next(iter(names)) if names else fallback
raw=gzip.decompress((folder/'saved-chains.txt.gz').read_bytes()).decode('utf-8')
allchains=[];tid=None;frames=[]
for line in raw.splitlines()+['']:
 header=re.fullmatch(r'\s*(\d+)/(\d+)\s*',line);frame=re.fullmatch(r'\s+([0-9a-f]+)\s+(.*)',line)
 if header:tid=header[2];frames=[]
 elif frame and tid is not None:frames.append(symbol(int(frame[1],16),frame[2]))
 elif not line.strip() and tid is not None:allchains.append({'tid':tid,'frames':frames});tid=None;frames=[]
chains=[r['frames'] for r in allchains if r['tid']==record['guestTid']]
formats=['::FormatInterpolatedStringPart','::FormatInterpolatedStringValue','::FormatValue','::ApplyInterpolatedFormatPadding','::TryFormatNumericValue','::ParseInterpolatedFormatSpecifier']
def core(name):
 match=re.search(r'\[([^]]+)\]\s+([^[]+)$',name.partition('::')[0])
 return bool(match and match[1]=='Lokad.Lython' and match[2].startswith('Lokad.Lython.') and match[2].count('<')==match[2].count('>'))
assert core('string [Lokad.Lython] Lokad.Lython.Runtime.LythonRuntime::FormatValue(object)')
assert not core('string [Other] External.Host::Run(class [Lokad.Lython] Lokad.Lython.LythonEngine)')
assert not core('void [System.Private.CoreLib] System.Collections.Generic.List`1<class [Lokad.Lython] Lokad.Lython.Item>::Clear()')
formatter=[c for c in chains if any(core(f) and any(n in f for n in formats) for f in c)]
specific=[c for c in formatter if any('::AsString(' in f or 'GetString(' in f or '::Decode' in f for f in c)]
count=collections.Counter(f for c in formatter for f in set(c) if core(f) and any(n in f for n in formats))
leaves=collections.Counter(c[0] if c else '<empty>' for c in chains)
result={'guestTid':record['guestTid'],'guestSamples':len(chains),'formatRootedSamples':len(formatter),'formatRootedFraction':len(formatter)/len(chains),'formatWithStringDecodeSamples':len(specific),'formatMethodsInclusiveNonAdditive':count.most_common(),'leaves':leaves.most_common(30),'malformedMapRecords':len(malformed),'ambiguousManagedAddresses':ambiguous,'threadCpu':record['threadCpuSeconds'],'captureWallSeconds':record['captureWallSeconds'],'completedJobs':record['completedJobs'],'performanceQualified':False,'decisionGateFraction':.2,'limits':'Shared numeric/string formatting and inlining prevent a string-specific or removable CPU fraction. Unknown leaves and active JIT remain explicit.'}
(root/'cost-assessment.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8',newline='\n')
(root/'resolved-chains.json.gz').write_bytes(gzip.compress(json.dumps(allchains).encode('utf-8')))
phase=json.loads((root/'retrieved/phases.json').read_text(encoding='utf-8'));obs=phase['rows'][0]['observations'];summary={}
for kind in sorted({r['kind'] for r in obs}):
 rows=[r for r in obs if r['kind']==kind]
 data={'observations':len(rows),'totalMedianUs':statistics.median(r['totalBeforeVerificationUs'] for r in rows),'nestedHostMedianUs':statistics.median(r['host']['elapsedUs'] for r in rows)}
 if kind=='public fresh':data['nestedCompileMedianUs']=statistics.median(r['compilation']['Microseconds'] for r in rows)
 if kind=='reflected synchronous phases':data['phaseMediansUs']={k:statistics.median(r['phases'][k]['Microseconds'] for r in rows) for k in rows[0]['phases']}
 summary[kind]=data
(root/'phase-summary.json').write_text(json.dumps({'performanceQualified':False,'directObservationsNonAdditive':summary,'limits':phase['limitations']},indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({k:result[k] for k in ['guestSamples','formatRootedSamples','formatRootedFraction','malformedMapRecords','ambiguousManagedAddresses']}))
