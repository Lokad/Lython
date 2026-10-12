from pathlib import Path
import hashlib,json,math,statistics,tarfile
stage=Path('.git/agent-notes/embedding-sync-pressure-20261013'); root=stage/'evidence'
assert hashlib.sha256((stage/'evidence.tar').read_bytes()).hexdigest()=='831888337e2c600ba793f1c313d2bf3fd1254303c4e2f66048f6a43379e5b5b3'
assert not root.exists(); root.mkdir()
with tarfile.open(stage/'evidence.tar') as archive: archive.extractall(root,filter='data')
manifest=json.loads((root/'manifest.json').read_bytes()); prepared=json.loads((root/'prepared.json').read_bytes()); summary=json.loads((root/'matrix/summary.json').read_bytes())
assert manifest['revision']=='f67419a86ee0304de9d0f05c0e68ef15c860e508'
assert summary['source']==manifest['revision'] and summary['productionTree']==manifest['productionTree']
assert len(summary['rows'])==30 and summary['allChildrenExited'] and summary['elapsedCampaignSeconds']<=300 and not summary['runtimeOverrides']
for name,expected in manifest['files'].items():
    path=root/name if (root/name).exists() else stage/'payload'/name
    assert hashlib.sha256(path.read_bytes()).hexdigest()==expected,name
assert hashlib.sha256((root/'run-matrix.py').read_bytes()).hexdigest()==json.loads((root/'runner-identity.json').read_bytes())['sha256']
terminal=json.loads((root/'terminal-proof.json').read_bytes()); assert terminal['leaseFree'] and not terminal['remainingPids'] and terminal['processors']==4
for unit in terminal['units'].values(): assert unit['MainPID']=='0' and unit['ActiveState']=='inactive' and unit['SubState']=='dead' and unit['Result']=='success' and unit['ExecMainStatus']=='0'
data={}
for row in summary['rows']:
    if 'pid' not in row: continue
    path=root/'matrix'/f"s{row['session']}-{row['name']}"
    assert hashlib.sha256((path/'stdout.json').read_bytes()).hexdigest()==row['stdoutSha256']
    assert hashlib.sha256((path/'stderr.txt').read_bytes()).hexdigest()==row['stderrSha256']
    if not row['eligible']: continue
    assert not row['exclusion'] and not row['timedOut'] and row['exitCode']==0 and row['elapsedIncludingCleanupSeconds']<30
    assert statistics.median(row['idleCpuPercent'])<=3 and max(row['idleCpuPercent'])<=5 and not row['stealTicks'] and not any(row['noiseDelta'].values())
    value=json.loads((path/'stdout.json').read_bytes())
    assert value['policy']==manifest['policy'] and value['name']==row['name'] and not value['asynchronous']
    assert value['subjectDllSha256'].lower()==prepared['identities']['Lokad.Lython.dll'] and value['subjectInformationalVersion'].endswith('+'+manifest['revision'])
    assert value['processorCount']==4 and value['samples']>=1000 and value['hostGenerationsBefore']==[0,0,131072]
    assert value['preparationCollections']==[2,2,2] and value['modeledHostBytes']==128*1024*1024
    assert value['peakBytes']<=value['capBytes'] and value['capBytes']==(28000 if value['pressure'] else 262144)
    assert value['warmupYieldedJobs']==value['warmupDelayedHostCalls']==value['yieldedJobs']==value['delayedHostCalls']==0
    assert value['completed']==0 if value['control'] else value['completed']>0
    job=row['name'].removesuffix('-control').removesuffix('-sync'); fixture_job='csv-summary' if value['pressure'] else job
    expected={name.removeprefix(fixture_job+'-1024/'):digest.upper() for name,digest in manifest['fixtureHashes'].items() if name.startswith(fixture_job+'-1024/')}
    assert value['fixtureHashes']==expected
    if value['pressure']:
        assert value['retainedPayloadBytes']==16000 and value['caughtDenialsPerJob']==128
        assert value['sourceSha256'].lower()==hashlib.sha256((root/'retained-pressure.py').read_bytes()).hexdigest()
    for metric in ['requestLatencyMilliseconds','heartbeatLatenessMilliseconds']:
        values=value[metric]['rawMilliseconds']; ordered=sorted(values)
        assert len(values)==value['samples'] and all(math.isfinite(x) and x>=0 for x in values)
        for p in [.5,.95,.99]: assert value[metric][f'p{round(p*100)}']==ordered[math.ceil(p*len(values))-1]
        assert value[metric]['max']==max(values)
    data[row['session'],row['name']]=value
pairs=[]; table=[]
for job in ['csv-summary','text-summary','json-summary','file-manifest','retained-pressure']:
    for session in range(1,4):
        name=job+'-sync'; active=data.get((session,name)); control=data.get((session,name+'-control'))
        if active is None or control is None:
            pairs.append({'job':job,'session':session,'eligible':False,'reason':'paired admitted samples required'}); continue
        p99=active['requestLatencyMilliseconds']['p99']-control['requestLatencyMilliseconds']['p99']
        heartbeat=active['heartbeatLatenessMilliseconds']['max']-control['heartbeatLatenessMilliseconds']['max']
        pairs.append({'job':job,'session':session,'eligible':True,'addedP99Milliseconds':p99,'addedLargestHeartbeatMilliseconds':heartbeat,'p99TargetMet':p99<=5,'heartbeatTargetMet':heartbeat<=25,'noGen2':active['gcCollections'][2]==0,'gen2PerCompleteJob':active['gcCollections'][2]/active['completed']})
    rows=[data[session,job+'-sync'] for session in range(1,4)]
    matches=[pair for pair in pairs if pair['job']==job]
    def bounds(values): return [min(values),max(values)]
    table.append({'job':job,'requestP99Ms':bounds([row['requestLatencyMilliseconds']['p99'] for row in rows]),'addedP99Ms':bounds([pair['addedP99Milliseconds'] for pair in matches]),'addedMaxHeartbeatMs':bounds([pair['addedLargestHeartbeatMilliseconds'] for pair in matches]),'collections':[bounds([row['gcCollections'][g] for row in rows]) for g in range(3)],'jobs':bounds([row['completed'] for row in rows]),'allocatedBytes':bounds([row['allocatedBytes'] for row in rows]),'cpuCoreFraction':bounds([row['cpuCoreFraction'] for row in rows])})
result={'source':manifest['revision'],'production':manifest['productionTree'],'policy':manifest['policy'],'eligibleRows':len(data),'independentRows':30,'campaignSeconds':summary['elapsedCampaignSeconds'],'longestChildSeconds':max(row['elapsedIncludingCleanupSeconds'] for row in summary['rows'] if 'pid' in row),'latencyTargetsMet':all(pair.get('p99TargetMet') and pair.get('heartbeatTargetMet') for pair in pairs),'pairs':pairs,'table':table,'qualification':'finite Linux closed-loop sync and bare-caught-denial model only; no Windows/general SLO/GC independence/CPython ratio claim'}
(root/'validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({key:result[key] for key in ['eligibleRows','campaignSeconds','longestChildSeconds','latencyTargetsMet','table']}))
