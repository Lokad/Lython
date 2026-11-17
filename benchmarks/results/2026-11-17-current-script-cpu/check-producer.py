from pathlib import Path
import hashlib,json,subprocess
r=Path(__file__).resolve().parent;repo=Path('D:/code/Lython');sibling=Path('D:/code/utf8regex')
old=json.loads((repo/'benchmarks/results/2026-11-17-function-container-construction/producer-source-check.json').read_text(encoding='utf-8'))
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=sibling,text=True).strip();files=[]
for item in old['files']:
 data=subprocess.check_output(['git','show',head+':'+item['path']],cwd=sibling)
 digest=hashlib.sha256(data).hexdigest();assert digest==item['committedSHA256'],item['path']
 files.append(dict(path=item['path'],committedSHA256=digest,unchanged=True))
v=dict(head=head,readOnlyCommittedBlobs=True,producerWorktreeNotReadOrModified=True,files=files,changesSince8403='Core Match/capture-slot and optional shared-working signatures consolidated; escaped character-class endpoints corrected. PythonRe metadata/cursor/type source unchanged.',missingCapabilities=old['missingCapabilities'],normalAdapterPin='0.4.0',normalAdapterSourceCompatibleWithProducerHead=False,development105QualificationReused=True,newQualification=False,packageReleaseStatusNotInferred=True,coreDevelopmentIndependent=True)
(r/'producer-source-check.json').write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8',newline='\n')
print('Producer head',head[:8],': all four PythonRe capability files unchanged; precise blockers unchanged, no qualification replay.')
