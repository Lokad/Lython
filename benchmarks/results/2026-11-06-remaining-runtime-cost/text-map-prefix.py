from pathlib import Path
import bisect,collections,json,re
root=Path(__file__).resolve().parent;folder=root/'retrieved/perf/text'
record=json.loads((folder/'record.json').read_text(encoding='utf-8'))
text=(folder/'prefix-jit.txt').read_text(encoding='utf-8')
listings=[]
for listing in text.split('; Assembly listing for method ')[1:]:
    if '(Tier1)' not in listing.splitlines()[0]:continue
    size=int(re.search(r'; Total bytes of code (\d+)',listing)[1])
    blocks=[]
    for m in re.finditer(r'(G_M\d+_IG\d+):\s*;; offset=0x([0-9A-Fa-f]+)',listing):
        blocks.append(dict(label=m[1],offset=int(m[2],16),start=m.start()))
    for i,b in enumerate(blocks):
        end=blocks[i+1]['start'] if i+1<len(blocks) else len(listing)
        b['code']=listing[b['start']:end].strip()
    listings.append(dict(bytes=size,blocks=blocks))
ranges=[]
for line in (folder/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
    p=line.rstrip().split(' ',2)
    if len(p)==3 and line.endswith('\n'):
        try:ranges.append((int(p[0],16),int(p[1],16),p[2]))
        except ValueError:pass
ranges.sort();starts=[v[0] for v in ranges]
counts=collections.Counter();offsets=collections.Counter();unmapped=collections.Counter();total=0;prefix=0
for line in (folder/'perf-leaves.txt').read_text(encoding='utf-8').splitlines():
    m=re.match(r'\s*(\d+)/(\d+)\s+([0-9a-fx]+)',line)
    if not m or m[2] not in record['guestTids']:continue
    total+=1;address=int(m[3],16);i=bisect.bisect_right(starts,address)-1
    if i<0:continue
    start,size,name=ranges[i]
    if address>=start+size or '::ExecuteInstructionPrefix(' not in name:continue
    prefix+=1;offset=address-start
    candidates=[v for v in listings if v['bytes']==size]
    if len(candidates)!=1:unmapped[name,size]+=1;continue
    blocks=candidates[0]['blocks'];j=bisect.bisect_right([b['offset'] for b in blocks],offset)-1
    if j<0:unmapped[name,size]+=1;continue
    b=blocks[j];counts[b['label']]+=1;offsets[offset]+=1
results=[]
for label,count in counts.most_common():
    block=next(b for v in listings for b in v['blocks'] if b['label']==label)
    results.append(dict(samples=count,percentAll=100*count/total,percentPrefix=100*count/prefix,**block))
summary=dict(totalSamples=total,prefixSamples=prefix,uniqueExactSizeTier1Listings=[v['bytes'] for v in listings],
    unmapped=[dict(name=k[0],bytes=k[1],samples=v) for k,v in unmapped.items()],blocks=results,
    leafOffsets=offsets.most_common(),limitations='Sampling skid/basic-block attribution; methods can include useful inlined operations; not removable CPU or source-pair timing.')
(root/'text-prefix-blocks.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Total',total,'prefix',prefix,'mapped',sum(counts.values()),'unmapped',dict(unmapped))
for r in results[:13]:
    print('\n',r['label'],r['samples'],f"{r['percentAll']:.2f}%all",'offset',hex(r['offset']))
    print('\n'.join(r['code'].splitlines()[:34]))
