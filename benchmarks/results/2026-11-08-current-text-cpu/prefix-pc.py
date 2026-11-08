from pathlib import Path
import bisect,collections,contextlib,io,json,re,runpy
r=Path(__file__).parent
with contextlib.redirect_stdout(io.StringIO()):d=runpy.run_path(str(r/'attribute.py'))
sections=[]
for section in (r/'retrieved/profiles/text-warm/call-jit.txt').read_text(encoding='utf8').split('; Assembly listing for method ')[1:]:
    header=section.splitlines()[0]
    if ':ExecuteInstructionPrefix(' not in header:continue
    size=re.search(r'; Total bytes of code (\d+)',section)
    if not size:continue
    blocks=[]
    for m in re.finditer(r'^(G_M\d+_IG\d+):\s*;; offset=0x([0-9A-Fa-f]+).*$',section,re.M):
        blocks.append((int(m[2],16),m[1],m.end()))
    blocks.sort()
    sections.append(dict(header=header,size=int(size[1]),blocks=blocks,text=section))
out=dict(source='b1e17b64/core93e926fe',sections=[dict(header=s['header'],size=s['size']) for s in sections],ranges=[],blocks=[],unmatched=0)
counts=collections.Counter();details={}
for sample in d['samples']:
    if not sample or '::ExecuteInstructionPrefix(' not in sample[0][1]:continue
    address,name=sample[0];index=bisect.bisect_right(d['starts'],address)-1
    start,size,mapped=d['ranges'][index];assert start<=address<start+size
    match=[s for s in sections if s['size']==size and ('(Tier1)' in s['header'])==('[OptimizedTier1]' in mapped)]
    row=dict(start=hex(start),size=size,name=mapped)
    if row not in out['ranges']:out['ranges'].append(row)
    if len(match)!=1:out['unmatched']+=1;continue
    section=match[0];blocks=section['blocks'];offset=address-start
    j=bisect.bisect_right([b[0] for b in blocks],offset)-1;assert j>=0
    block=blocks[j];key=(section['size'],block[1],block[0]);counts[key]+=1
    end=blocks[j+1][2]-len(blocks[j+1][1])-40 if j+1<len(blocks) else len(section['text'])
    # Retain the exact next label boundary, not a guessed line/byte conversion.
    marker=re.search(r'^G_M\d+_IG\d+:',section['text'][block[2]:],re.M)
    end=block[2]+marker.start() if marker else len(section['text'])
    details[key]=section['text'][block[2]:end].strip()
for key,count in counts.most_common():out['blocks'].append(dict(size=key[0],label=key[1],offset=hex(key[2]),leafSamples=count,assembly=details[key]))
out['caveat']='Only exact native-size/tier matches; samples may land after an inlined operation or within useful work. Blocks are not pure dispatch phases; no normal elapsed or speedup inference. Original maps/raw/JIT retained.'
(r/'prefix-pc.json').write_text(json.dumps(out,indent=2)+'\n',encoding='utf8')
print('prefix',sum(counts.values()),'matched,',out['unmatched'],'unmatched; disassembly sizes',[(s['size'],s['header'][-30:]) for s in sections])
for b in out['blocks'][:7]:print(b['leafSamples'],b['label'],b['offset'],b['assembly'][:1800])
