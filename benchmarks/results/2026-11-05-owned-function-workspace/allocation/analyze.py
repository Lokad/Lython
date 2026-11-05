from pathlib import Path
import hashlib,json,statistics,tarfile
root=Path(__file__).resolve().parent
receipt=json.loads((root/'archive-receipt.json').read_text())
assert hashlib.sha256((root/'observations.tgz').read_bytes()).hexdigest()==receipt['archiveSHA256']
with tarfile.open(root/'observations.tgz') as archive:archive.extractall(root/'retrieved',filter='data')
result=json.loads((root/'retrieved/result.json').read_text())
job=result['observations'][0]
summary={}
for role in ('before','candidate','before-control'):
    rows=[s for s in job['samples'] if s['image']==role]
    elapsed=[s['elapsedMilliseconds']/s['jobs'] for s in rows]
    allocation=[s['allocatedBytes']/s['jobs'] for s in rows]
    quartiles=statistics.quantiles(elapsed,n=4,method='inclusive')
    summary[role]=dict(medianMilliseconds=statistics.median(elapsed),medianProcessAllocatedBytes=statistics.median(allocation),iqrPercent=100*(quartiles[2]-quartiles[0])/statistics.median(elapsed))
for role in ('before','before-control'):
    print(role,'elapsedDelta%',100*(summary['candidate']['medianMilliseconds']/summary[role]['medianMilliseconds']-1),'allocationDelta%',100*(summary['candidate']['medianProcessAllocatedBytes']/summary[role]['medianProcessAllocatedBytes']-1))
(root/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps(summary))
