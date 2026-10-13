from pathlib import Path
import json,tarfile,hashlib,statistics,math
report=Path(__file__).resolve().parent
root=report/'reproduce/v2'
m=json.loads((root/'manifest.json').read_bytes());p=json.loads((root/'prepared.json').read_bytes());s=json.loads((report/'raw/v2/summary.json').read_bytes());terminal=json.loads((root/'terminal-proof.json').read_bytes())
assert s['policy']==m['policy']=='r10-bound-error-micro-v2' and s['elapsedSeconds']<30 and s['allChildrenExited'] and not s['runtimeOverrides']
assert terminal['leaseFree'] and not terminal['remainingPids'] and terminal['processors']==4
for unit in terminal['units'].values():assert unit['MainPID']=='0' and unit['ActiveState']=='inactive' and unit['Result']=='success' and unit['ExecMainStatus']=='0'
assert p['identities']['before']['Probe.dll']==p['identities']['after']['Probe.dll']
for name,digest in p['identities']['before'].items():
 if name!='Lokad.Lython.dll':assert p['identities']['after'][name]==digest
assert p['identities']['before']['Lokad.Lython.dll']==m['beforeDllSha256']
for name,expected in m['files'].items():
 path=root/name
 if name=='subject.tar':continue # reproduce declared Git archive independently
 assert hashlib.sha256(path.read_bytes()).hexdigest()==expected,name
rows={}
for row in s['rows']:
 assert row['eligible'] and not row['exclusion'] and not row['timedOut'] and row['exitCode']==0 and row['elapsedIncludingCleanupSeconds']<9
 assert statistics.median(row['idleCpuPercent'])<=3 and max(row['idleCpuPercent'])<=5 and not row['stealTicks'] and not any(row['noiseDelta'].values())
 path=report/'raw/v2'/row['label'];out=(path/'stdout.json').read_bytes();assert hashlib.sha256(out).hexdigest()==row['stdoutSha256'];assert hashlib.sha256((path/'stderr.txt').read_bytes()).hexdigest()==row['stderrSha256']
 d=json.loads(out);version,kind=row['label'].split('-');assert d['subjectDllSha256'].lower()==p['identities'][version]['Lokad.Lython.dll']
 assert d['policy']==m['policy'] and d['capBytes']==48000 and d['peakBytes']<=48000 and d['hostGenerationsBefore']==[0,0,131072]
 assert d['preparationCollections']==[2,2,2] and d['retainedPayloadBytes']==32000 and d['caughtDenialsPerJob']==128
 assert d['subjectInformationalVersion'].endswith('+'+(m['beforeRevision'] if version=='before' else m['revision']))
 assert d['originalSourceSha256'].lower()==hashlib.sha256((root/'pressure-bound.py').read_bytes()).hexdigest()
 expected={k.removeprefix('csv-summary-1024/'):v.upper() for k,v in m['fixtureHashes'].items() if k.startswith('csv-summary-1024/')};assert d['fixtureHashes']==expected
 for metric in ['requestLatencyMilliseconds','heartbeatLatenessMilliseconds']:
  values=d[metric]['rawMilliseconds'];ordered=sorted(values);assert len(values)==d['samples'] and all(math.isfinite(v) and v>=0 for v in values)
  for quantile in [.5,.95,.99]:assert d[metric][f'p{round(quantile*100)}']==ordered[math.ceil(len(values)*quantile)-1]
  assert d[metric]['max']==max(values)
 rows[row['label']]=d
result={'candidate':m['revision'],'production':m['production'],'before':m['beforeRevision'],'campaignSeconds':s['elapsedSeconds'],'eligibleSamples':4,'pairs':{}}
for version in ['before','after']:
 a=rows[version+'-active'];c=rows[version+'-control'];result['pairs'][version]={'completed':a['completed'],'observationSeconds':a['observationSeconds'],'meanJobMilliseconds':1000*a['observationSeconds']/a['completed'],'gcCollections':a['gcCollections'],'gen2PerJob':a['gcCollections'][2]/a['completed'],'cpuSeconds':a['cpuSeconds'],'cpuMillisecondsPerJob':1000*a['cpuSeconds']/a['completed'],'allocatedBytesPerJob':a['allocatedBytes']/a['completed'],'peakBytes':a['peakBytes'],'samples':a['samples'],'requestP99Ms':a['requestLatencyMilliseconds']['p99'],'addedRequestP99Ms':a['requestLatencyMilliseconds']['p99']-c['requestLatencyMilliseconds']['p99'],'addedHeartbeatMaxMs':a['heartbeatLatenessMilliseconds']['max']-c['heartbeatLatenessMilliseconds']['max']}
result['status']='one short matched before/after diagnostic; no percentile/SLO/CPython qualification; invalid v1 preserved separately'
assert result==json.loads((report/'validation.json').read_bytes())
print(json.dumps({'eligibleSamples':4,'campaignSeconds':result['campaignSeconds'],'verified':True}))

v1=json.loads((report/'raw/v1/summary.json').read_bytes())
assert len(v1['rows'])==4 and all(not r['eligible'] and r['exitCode']==-6 for r in v1['rows'])
for row in v1['rows']:
    folder=report/'raw/v1'/row['label']
    assert hashlib.sha256((folder/'stdout.json').read_bytes()).hexdigest()==row['stdoutSha256']
    assert hashlib.sha256((folder/'stderr.txt').read_bytes()).hexdigest()==row['stderrSha256']
    assert 'Insufficient request observations' in (folder/'stderr.txt').read_text()
print(json.dumps({'invalidOriginalsPreserved':4}))
