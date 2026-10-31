"""Offline CPU IP -> current JIT block mapping; no runtime instrumentation."""
import bisect, collections, json, re, sys
from pathlib import Path

folder = Path(sys.argv[1])
record = json.loads((folder/'record.json').read_text())
active = next(x for x in record['threadCpuSeconds'] if x['name'].startswith('Lython executio'))['tid']
listing = (folder/'jit-disasm.txt').read_text()
methods = re.split(r'^; Assembly listing for method ', listing, flags=re.M)[1:]
matched = []
for method in methods:
    header = method.splitlines()[0]
    if ':ExecuteUntilCall()' not in header: continue
    size = int(re.search(r'; Total bytes of code (\d+)', method)[1])
    maps = []
    for line in (folder/'perf.map').read_text().splitlines():
        parts = line.split(' ',2)
        if len(parts)!=3: continue
        start, length, name = parts
        if 'ExecutableFrameInterpreter::ExecuteUntilCall()' in name and int(length,16)==size:
            maps.append((int(start,16),name))
    if len(maps)!=1:
        matched.append(dict(header=header,codeSize=size,ambiguousMaps=maps));continue
    start, name = maps[0]
    blocks=[]; instructions=[]; offset=0
    for line in method.splitlines():
        label=re.match(r'(G_M\d+_IG\d+):.*offset=0x([0-9a-fA-F]+)',line)
        if label:
            offset=int(label[2],16)
            blocks.append(dict(label=label[1],offset=offset,instructions=[]))
        ins=re.match(r'\s+([0-9A-F]+)\s+(.+)',line)
        if ins and len(ins[1])%2==0:
            item=dict(offset=offset,bytes=ins[1],text=ins[2])
            instructions.append(item)
            if blocks:blocks[-1]['instructions'].append(item)
            offset+=len(ins[1])//2
    counts=collections.Counter()
    for line in (folder/'perf-leaves.txt').read_text().splitlines():
        hit=re.match(r'\s*(\d+)/(\d+)\s+([0-9a-fx]+)\s*(.*)',line)
        if hit and hit[2]==active:
            address=int(hit[3],16)
            if start<=address<start+size:counts[address-start]+=1
    block_offsets=[b['offset'] for b in blocks]
    for off,count in counts.items():
        block=blocks[bisect.bisect_right(block_offsets,off)-1]
        block['samples']=block.get('samples',0)+count
        block.setdefault('sampleOffsets',{})[hex(off)]=count
    matched.append(dict(header=header,name=name,codeSize=size,samples=counts.total(),
        blocks=sorted([b for b in blocks if b.get('samples')],key=lambda b:-b['samples'])))
(folder/'dispatch-offsets.json').write_text(json.dumps(matched,indent=2)+'\n')
print(folder.name,[(x.get('codeSize'),x.get('samples'),len(x.get('ambiguousMaps',[]))) for x in matched])
for method in matched:
    for block in method.get('blocks',[])[:5]:
        print(block['label'],hex(block['offset']),block['samples'],block['sampleOffsets'])
        for ins in block['instructions'][:10]: print(' ',hex(ins['offset']),ins['text'])
