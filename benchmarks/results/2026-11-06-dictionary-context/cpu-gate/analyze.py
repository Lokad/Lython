from pathlib import Path
import bisect,collections,hashlib,json,re
root=Path(__file__).parent
for workload in ['text','csv']:
    folder=root/'retrieved/perf'/workload;r=json.loads((folder/'record.json').read_text(encoding='utf-8'));tids=set(r['guestTids'])
    ranges=[];malformed=[]
    for line in (folder/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
        p=line.rstrip().split(' ',2)
        if len(p)!=3 or not line.endswith('\n'):malformed.append(line);continue
        ranges.append((int(p[0],16),int(p[1],16),p[2]))
    ranges.sort();starts=[v[0] for v in ranges]
    def symbol(address,fallback):
        i=bisect.bisect_right(starts,address)-1
        return ranges[i][2] if i>=0 and address<ranges[i][0]+ranges[i][1] else fallback
    leaves=collections.Counter();inclusive=collections.Counter();stacks=[]
    for block in (folder/'perf-stacks.txt').read_text(encoding='utf-8').split('\n\n'):
        lines=block.splitlines()
        if not lines:continue
        m=re.match(r'(\d+)/(\d+)',lines[0])
        if not m or m[2] not in tids:continue
        names=[]
        for line in lines[1:]:
            p=line.split(None,1)
            if len(p)==2:names.append(symbol(int(p[0],16),p[1]))
        if names:stacks.append(names);leaves[names[0]]+=1;inclusive.update(set(names))
    listings=[]
    for listing in (folder/'prefix-jit.txt').read_text(encoding='utf-8').split('; Assembly listing for method ')[1:]:
        header=listing.splitlines()[0]
        method=re.search(r':(ExecuteInstructionPrefix|CompleteLocalResult|StoreExecutableLocal)\(',header)
        size=re.search(r'; Total bytes of code (\d+)',listing)
        if not method or not size or '(Tier1)' not in header:continue
        blocks=[]
        for m in re.finditer(r'(G_M\d+_IG\d+):\s*;; offset=0x([0-9A-Fa-f]+)',listing):blocks.append(dict(label=m[1],offset=int(m[2],16),start=m.start()))
        for i,b in enumerate(blocks):b['code']=listing[b['start']:(blocks[i+1]['start'] if i+1<len(blocks) else len(listing))].strip()
        listings.append(dict(method=method[1],bytes=int(size[1]),header=header,blocks=blocks))
    blockcounts=collections.Counter();unmapped=collections.Counter();methodcounts=collections.Counter();total=0
    for line in (folder/'perf-leaves.txt').read_text(encoding='utf-8').splitlines():
        m=re.match(r'\s*(\d+)/(\d+)\s+([0-9a-fx]+)',line)
        if not m or m[2] not in tids:continue
        total+=1;address=int(m[3],16);i=bisect.bisect_right(starts,address)-1
        if i<0:continue
        start,size,name=ranges[i]
        method=re.search(r'::(ExecuteInstructionPrefix|CompleteLocalResult|StoreExecutableLocal)\(',name)
        if address>=start+size or not method:continue
        method=method[1];methodcounts[method]+=1;matching=[(j,v) for j,v in enumerate(listings) if v['method']==method and v['bytes']==size]
        if len(matching)!=1:unmapped[method,size]+=1;continue
        j,listing=matching[0];k=bisect.bisect_right([b['offset'] for b in listing['blocks']],address-start)-1
        if k<0:unmapped[method,size]+=1;continue
        blockcounts[j,k]+=1
    blocks=[dict(method=listings[j]['method'],samples=count,percentAll=100*count/total,**listings[j]['blocks'][k]) for (j,k),count in blockcounts.most_common()]
    keys=['Mirror','TryInsert','CompleteLocalResult','StoreExecutableLocal','BeginPendingCall','CreateExecutableWorkState','PyDict','MemoryGovernor','TryResize','Reserve','SplitWhitespace','CsvRecordParser','PyIndexing','CheckExecution','PrepareQueryKey','PyStructuralGuard']
    def method_name(name):
        if '::' not in name:return name
        owner,_,method=name.partition('::')
        return owner.rsplit(' ',1)[-1]+'::'+method.split('(',1)[0]
    groups={key:dict(leaf=sum(c for n,c in leaves.items() if key in method_name(n)),inclusive=sum(any(key in method_name(n) for n in ss) for ss in stacks)) for key in keys}
    hotnative=[]
    for ss in stacks:
        if 'Monitor::' in ss[0] or '__tls_get_addr' in ss[0]:hotnative.append(ss[:7])
    result=dict(samples=total,guestTids=r['guestTids'],guestCpuSeconds=sum(x['seconds'] for x in r['threadCpuSeconds'] if x['tid'] in tids),
        groups=groups,leaves=leaves.most_common(),inclusive=inclusive.most_common(),methodSamples=dict(methodcounts),
        mappedSamples=sum(blockcounts.values()),unmapped=[dict(method=k[0],bytes=k[1],samples=v) for k,v in unmapped.items()],
        jitBlocks=blocks,hotNativeStacks=hotnative,malformedMapLines=malformed,
        limitations='Sampling/skid and inclusive overlap; unknown/native leaves are not assigned to memory or scopes without caller evidence. Not a source-pair speedup/removable fraction.')
    (root/(workload+'-analysis.json')).write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(workload,'samples',total,'mapped',result['mappedSamples'],'methods',dict(methodcounts),'unmapped',result['unmapped'])
    print('groups',groups)
    for b in blocks[:8]:print('\n',b['method'],b['label'],b['samples'],'\n'+'\n'.join(b['code'].splitlines()[:19]))
