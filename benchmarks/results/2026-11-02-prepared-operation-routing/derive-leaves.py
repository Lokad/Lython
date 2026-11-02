"""Offline derivation of the fixed captures, no new runtime measurement."""
import bisect,collections,json,pathlib,re
root=pathlib.Path(__file__).parent/'retrieved/perf'
for case in ['csv-warm','text-warm']:
    folder=root/case
    record=json.loads((folder/'record.json').read_text())
    ranges=[]
    for line in (folder/'perf.map').read_text().splitlines(keepends=True):
        parts=line.rstrip().split(' ',2)
        if len(parts)==3 and line.endswith('\n'):
            ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
    ranges.sort(); starts=[r[0] for r in ranges]; counts=collections.Counter()
    for line in (folder/'perf-leaves.txt').read_text().splitlines():
        m=re.match(r'\s*(\d+)/(\d+)\s+([0-9a-fx]+)\s*(.*)',line)
        if not m or m[2]!=record['guestTid']:continue
        address=int(m[3],16); index=bisect.bisect_right(starts,address)-1
        symbol=ranges[index][2] if index>=0 and address<ranges[index][0]+ranges[index][1] else m[4]
        counts[symbol]+=1
    result={'samples':counts.total(),'allLeafCounts':counts.most_common(),
        'preparedLeafCounts':[(s,c) for s,c in counts.most_common() if 'Prepared' in s],
        'limitation':'Self and inlined samples are not an exact removable forwarding fraction; no new capture.'}
    (folder/'all-cpu-leaves.json').write_text(json.dumps(result,indent=2))
    print(case,'samples',counts.total(),'prepared leaf samples',sum(c for s,c in result['preparedLeafCounts']))
