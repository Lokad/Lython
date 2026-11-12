from pathlib import Path
import ast,hashlib,json,tarfile
r=Path(__file__).resolve().parent;q=r/'b614';p=q/'collect.py';s=p.read_text(encoding='utf-8')
a=s.index("purpose='");b=s.index("',\n                  warmupSeconds",a)
s=s[:a]+"purpose='R266compiler-only negatedpredicate normalization versusfullyqualified983bab96/796c. GuardedbothAPIs/native/capturedcontrols;12swarmups/350msrounds. Allruntime sourcebytes baselineidentical. Currentcompileguardedallocation+1.02%, allcurrentdesktopadversity andoriginalrawvariant+1.10%/capturedCPUdebt preserved. No causalrecovery/CPUforecast. Sameworker/RAMIO/fullvalidation/priorresultcleanup"+s[b:]
ast.parse(s);p.write_text(s,encoding='utf-8',newline='\n')
with tarfile.open(q/'payload.tgz','w:gz')as t:
 for name in ['representative.json','source-identity.json','identity.json','collect.py','archive-owned.py','run.sh','current-core.dll']:t.add(q/name,arcname=name)
with tarfile.open(q/'payload.tgz')as t:expanded=sum(((m.size+4095)//4096)*4096 for m in t.getmembers())
config=json.loads((q/'config.json').read_text(encoding='utf-8'));assert config['requiredFreeBytes']==expanded+252*1024*1024
config.update(payloadBytes=(q/'payload.tgz').stat().st_size,payloadSHA256=hashlib.sha256((q/'payload.tgz').read_bytes()).hexdigest())
(q/'config.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
print('Current/initial source claims distinguished before launch; payload/hash rebuilt, requiredfloor unchanged.')
