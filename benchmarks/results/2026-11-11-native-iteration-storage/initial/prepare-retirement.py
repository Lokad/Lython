from pathlib import Path
import hashlib,json,shutil
r=Path(__file__).parent;n=r/'retirement';n.mkdir();old=r/'refined'
meta=json.loads((old/'diagnostic-identity.json').read_text(encoding='utf-8'))
for name,h in meta['sourceHashes'].items():assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==h,name
q=old/'b593';receipt=json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'))
assert receipt['complete']and receipt['jobs']==7 and hashlib.sha256((q/'observations.tgz').read_bytes()).hexdigest()==receipt['archiveSHA256']
assert (q/'terminal-status.json').exists()and (q/'summary.json').exists()
for phase in ['warm-sync','warm-async','compile']:
 (n/phase).mkdir();shutil.copyfile(old/phase/'hash-witness.json',n/phase/'hash-witness.json')
for role in ['before','candidate']:shutil.copytree(r/'images/before',n/'images'/role)
shutil.copytree(r/'originals',n/'originals')
for name in ['before-net10.dll','catalog.json','python-goldens.json','run-focused.py','freeze-and-measure.py','summarize.py']:shutil.copyfile(old/name,n/name)
before=json.loads((old/'before.json').read_text(encoding='utf-8'));before['hypothesis']+=' Final retirement correction: only live cased reference descriptors need clearing; plain list/tuple integer progress resets at acquisition. Remove unconditional metadata writes introduced for ordinary VM values. B593196.604s terminal: ordinaryRun−3.96..−5.01% with supportingCPU, captured/search variable, reportAsync widerCPU+4.26..+6.38%; preserve exact evidence, no attribution of entire regression to clearing.'
(n/'before.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8')
print('Both measured forms and B593 source/raw archive frozen. Retirement correction keeps same qualified baseline and controls; no current VM users.')
