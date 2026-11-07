from pathlib import Path
import bisect,collections,json,re

root=Path(__file__).resolve().parent;folder=root/'retrieved/perf/text-warm'
record=json.loads((folder/'record.json').read_text(encoding='utf-8'))
listings=[]
for listing in (folder/'prefix-jit.txt').read_text(encoding='utf-8').split('; Assembly listing for method ')[1:]:
    header=listing.splitlines()[0]
    if 'ExecuteInstructionPrefix(' not in header or '(Tier1)' not in header:continue
    size=int(re.search(r'; Total bytes of code (\d+)',listing)[1]);blocks=[]
    for match in re.finditer(r'(G_M\d+_IG\d+):\s*;; offset=0x([0-9A-Fa-f]+)',listing):
        blocks.append(dict(label=match[1],offset=int(match[2],16),start=match.start()))
    for index,block in enumerate(blocks):
        end=blocks[index+1]['start'] if index+1<len(blocks) else len(listing)
        block['code']=listing[block['start']:end].strip()
    listings.append(dict(header=header,bytes=size,blocks=blocks))
ranges=[];malformed=[]
for line in (folder/'perf.map').read_text(encoding='utf-8').splitlines(keepends=True):
    parts=line.rstrip().split(' ',2)
    try:
        assert len(parts)==3 and line.endswith('\n')
        ranges.append((int(parts[0],16),int(parts[1],16),parts[2]))
    except (ValueError,AssertionError):malformed.append(line)
ranges.sort();starts=[value[0] for value in ranges]
counts=collections.Counter();offsets=collections.Counter();unmapped=collections.Counter();total=prefix=0
for line in (folder/'perf-leaves.txt').read_text(encoding='utf-8').splitlines():
    match=re.match(r'\s*(\d+)/(\d+)\s+([0-9a-fx]+)',line)
    if not match or match[2] not in record['guestTids']:continue
    total+=1;address=int(match[3],16);index=bisect.bisect_right(starts,address)-1
    if index<0:continue
    start,size,name=ranges[index]
    if address>=start+size or '::ExecuteInstructionPrefix(' not in name:continue
    prefix+=1;offset=address-start
    candidates=[(index,value) for index,value in enumerate(listings) if value['bytes']==size]
    if len(candidates)!=1:unmapped[name,size]+=1;continue
    listing_index,listing=candidates[0]
    blocks=listing['blocks'];index=bisect.bisect_right([b['offset'] for b in blocks],offset)-1
    if index<0:unmapped[name,size]+=1;continue
    block=blocks[index];counts[listing_index,index]+=1;offsets[listing_index,offset]+=1
rows=[]
for (listing_index,block_index),count in counts.most_common():
    block=listings[listing_index]['blocks'][block_index]
    rows.append(dict(samples=count,percentAll=100*count/total,percentPrefix=100*count/prefix,
                     listing=listing_index,codeBytes=listings[listing_index]['bytes'],**block))
summary=dict(totalSamples=total,prefixSamples=prefix,uniqueExactSizeTier1Listings=[v['bytes'] for v in listings],
             malformedMapLines=malformed,unmapped=[dict(name=k[0],bytes=k[1],samples=v) for k,v in unmapped.items()],
             blocks=rows,leafOffsets=[dict(listing=k[0],offset=k[1],samples=v) for k,v in offsets.most_common()],
             limitations='Current-process unique Tier1 map-size/range mapping. Sampling skid and necessary inlined work remain; no removable CPU or source-pair timing claim.')
(root/'prefix-blocks.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Total',total,'prefix',prefix,'mapped',sum(counts.values()),'unmapped',dict(unmapped),'malformed',len(malformed))
for row in rows[:20]:
    print('\n',row['label'],row['samples'],f"{row['percentAll']:.2f}%all",'offset',hex(row['offset']))
    print('\n'.join(row['code'].splitlines()[:28]))
