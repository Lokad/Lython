import datetime,hashlib,json,os,pathlib,signal,statistics,subprocess,time
root=pathlib.Path('/home/lokad/lython-bench-checks/embedding-impact-20261012')
dotnet='/home/lokad/lython-bench-toolchains/dotnet/10.0.401/dotnet'
expected=json.loads((root/'prepared.json').read_text())['identities']
producers=json.loads((root/'producers.json').read_text(encoding='utf-8-sig'))
results=root/'matrix'
results.mkdir(exist_ok=False)
start=time.monotonic()
deadline=start+240
cases=[('current','control'),('control','control'),('current','csv-summary'),('control','csv-summary'),('current','text-summary'),('current','json-summary'),('current','file-manifest')]
runtime_keys=('DOTNET_TieredCompilation','DOTNET_TieredPGO','DOTNET_GCServer','DOTNET_ReadyToRun','COMPlus_TieredCompilation','COMPlus_gcServer','COMPlus_gcConcurrent','CORECLR_ENABLE_PROFILING')
environment={key:os.environ[key] for key in runtime_keys if key in os.environ}
if environment: raise RuntimeError('Unexpected runtime overrides: '+str(list(environment)))
def cpu():
    values=list(map(int,pathlib.Path('/proc/stat').read_text().splitlines()[0].split()[1:9]))
    return sum(values),values[3]+values[4],values[7]
def vm():
    return {line.split()[0]:int(line.split()[1]) for line in pathlib.Path('/proc/vmstat').read_text().splitlines() if line.split()[0] in ('pgmajfault','pswpin','pswpout')}
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
rows=[]
for session in range(3):
    order=cases if session%2==0 else list(reversed(cases))
    for variant,name in order:
        stage=results/f's{session+1}-{variant}-{name}'
        stage.mkdir()
        row={'session':session+1,'variant':variant,'name':name,'startedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
        if deadline-time.monotonic()<30:
            row.update(eligible=False,exclusion='original campaign deadline reserves cleanup')
            rows.append(row); continue
        idle=[]
        for _ in range(2):
            before=cpu();time.sleep(.25);after=cpu()
            total=after[0]-before[0]
            idle.append(100*(total-(after[1]-before[1]))/total)
        row['idleCpuPercent']=idle
        if statistics.median(idle)>3 or max(idle)>5:
            row.update(eligible=False,exclusion='idle CPU gate')
            rows.append(row); (stage/'receipt.json').write_text(json.dumps(row,indent=2)); continue
        directory=root/variant/'ImpactProbe/bin/Release/net10.0'
        for filename,value in expected[variant].items():
            if digest(directory/filename)!=value: raise RuntimeError('Frozen producer changed')
        before_cpu=cpu(); before_vm=vm()
        began=time.monotonic()
        p=subprocess.Popen([dotnet,str(directory/'Probe.dll'),name,str(root/'fixtures'),str(root/'scripts')],stdout=subprocess.PIPE,stderr=subprocess.PIPE,start_new_session=True)
        timeout=False
        try: out,err=p.communicate(timeout=28)
        except subprocess.TimeoutExpired:
            timeout=True; os.killpg(p.pid,signal.SIGKILL); out,err=p.communicate(timeout=1)
        elapsed=time.monotonic()-began
        after_cpu=cpu();after_vm=vm()
        (stage/'stdout.json').write_bytes(out); (stage/'stderr.txt').write_bytes(err)
        row.update(pid=p.pid,exitCode=p.returncode,timedOut=timeout,elapsedIncludingCleanupSeconds=elapsed,stdoutSha256=digest(stage/'stdout.json'),stderrSha256=digest(stage/'stderr.txt'),noiseDelta={k:after_vm[k]-before_vm[k] for k in before_vm},stealTicks=after_cpu[2]-before_cpu[2])
        data=None
        if p.returncode!=0 or timeout or elapsed>=30: row.update(eligible=False,exclusion='owned process/output gate')
        else:
            data=json.loads(out)
            if data['subjectDllSha256'].lower()!=expected[variant]['Lokad.Lython.dll'] or not data['subjectInformationalVersion'].endswith('+'+producers[variant]): raise RuntimeError('Loaded subject differs')
            if data['name']!=name or data['samples']<1000: raise RuntimeError('Sample/identity mismatch')
            if data['hostGenerationsBefore']!=[0,0,data['modeledHostObjects']]: row.update(eligible=False,exclusion='host graph was not fully old before measurement')
            elif row['stealTicks'] or any(row['noiseDelta'].values()): row.update(eligible=False,exclusion='steal or paging noise')
            else: row.update(eligible=True,exclusion=None)
            row['result']={k:v for k,v in data.items() if k not in ('fixtureHashes','requestLatencyMilliseconds','heartbeatLatenessMilliseconds')}
            row['request']={k:v for k,v in data['requestLatencyMilliseconds'].items() if k!='rawMilliseconds'}
            row['heartbeat']={k:v for k,v in data['heartbeatLatenessMilliseconds'].items() if k!='rawMilliseconds'}
        rows.append(row)
        (stage/'receipt.json').write_text(json.dumps(row,indent=2))
        (results/'progress.json').write_text(json.dumps(rows,indent=2))
        print(json.dumps({k:row[k] for k in ('session','variant','name','eligible','exclusion')}),flush=True)
for variant in expected:
    for filename,value in expected[variant].items():
        if digest(root/variant/'ImpactProbe/bin/Release/net10.0'/filename)!=value: raise RuntimeError('Producer changed during collection')
summary={'policy':'embedding-v1','elapsedCampaignSeconds':time.monotonic()-start,'producers':producers,'environmentOverrides':environment,'rows':rows}
(results/'summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps({'completed':len(rows),'eligible':sum(r['eligible'] for r in rows),'elapsed':summary['elapsedCampaignSeconds']}),flush=True)
