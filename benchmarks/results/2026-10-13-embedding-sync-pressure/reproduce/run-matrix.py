import datetime, fcntl, hashlib, json, os, pathlib, signal, statistics, subprocess, time

root = pathlib.Path('/home/lokad/lython-bench-checks/embedding-sync-pressure-f67419a8-20261013')
lock = (root.parent / 'comparison-vm.lock').open('a')
fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
manifest = json.loads((root / 'manifest.json').read_text())
prepared = json.loads((root / 'prepared.json').read_text())
results = root / 'matrix'
results.mkdir(exist_ok=False)
dotnet = pathlib.Path('/home/lokad/lython-bench-toolchains/dotnet/10.0.401/dotnet')
binary_root = root / 'subject/ImpactProbe/bin/Release/net10.0'
started = time.monotonic()
deadline = started + manifest['campaignDeadlineSeconds']
runtime_keys = ('DOTNET_ReadyToRun', 'COMPlus_ReadyToRun', 'CORECLR_ENABLE_PROFILING', 'DOTNET_EnableDiagnostics')
overrides = {key: value for key, value in os.environ.items()
             if key in runtime_keys or key.lower().startswith(('dotnet_gc', 'complus_gc', 'dotnet_tiered', 'complus_tiered', 'dotnet_jit', 'complus_jit'))}
if overrides: raise RuntimeError('Runtime overrides fail admission: '+','.join(overrides))
environment = dict(os.environ, DOTNET_ROOT=str(dotnet.parent), DOTNET_NOLOGO='1', DOTNET_CLI_TELEMETRY_OPTOUT='1')
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def identities(): return {name: digest(binary_root / name) for name in prepared['identities']}
assert identities() == prepared['identities']
assert digest(dotnet) == prepared['dotnetSha256']
for name, expected in manifest['fixtureHashes'].items(): assert digest(root / 'fixtures' / name) == expected
for name, expected in manifest['files'].items(): assert digest(root / name) == expected
def cpu():
    values = list(map(int, pathlib.Path('/proc/stat').read_text().splitlines()[0].split()[1:9]))
    return sum(values), values[3] + values[4], values[7]
def vm():
    return {line.split()[0]: int(line.split()[1]) for line in pathlib.Path('/proc/vmstat').read_text().splitlines()
            if line.split()[0] in ('pgmajfault', 'pswpin', 'pswpout')}
rows = []
for session in range(3):
    order = manifest['cases'] if session != 1 else list(reversed(manifest['cases']))
    for name in order:
        stage = results / f's{session+1}-{name}'
        stage.mkdir()
        row = {'session': session+1, 'name': name, 'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
               'eligible': False, 'exclusion': None}
        if deadline-time.monotonic() < 30:
            row['exclusion'] = 'original campaign deadline reserves cleanup'
        else:
            idle = []
            for _ in range(2):
                before = cpu(); time.sleep(.25); after = cpu()
                total = after[0] - before[0]
                idle.append(100 * (total - (after[1] - before[1])) / total)
            row['idleCpuPercent'] = idle
            if statistics.median(idle) > 3 or max(idle) > 5:
                row['exclusion'] = 'idle CPU gate'
            else:
                before_cpu, before_vm = cpu(), vm()
                began = time.monotonic()
                process = subprocess.Popen([str(dotnet), str(binary_root / 'Probe.dll'), name,
                                            str(root / 'fixtures'), str(root / 'subject/benchmarks/scripts')],
                                           stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=environment, start_new_session=True)
                timeout = False
                try: out, err = process.communicate(timeout=28)
                except subprocess.TimeoutExpired:
                    timeout = True; os.killpg(process.pid, signal.SIGKILL); out, err = process.communicate(timeout=1)
                elapsed = time.monotonic() - began
                after_cpu, after_vm = cpu(), vm()
                (stage / 'stdout.json').write_bytes(out); (stage / 'stderr.txt').write_bytes(err)
                row.update(pid=process.pid, exitCode=process.returncode, timedOut=timeout,
                           elapsedIncludingCleanupSeconds=elapsed, stdoutSha256=digest(stage / 'stdout.json'),
                           stderrSha256=digest(stage / 'stderr.txt'), stealTicks=after_cpu[2]-before_cpu[2],
                           noiseDelta={key: after_vm[key]-before_vm[key] for key in before_vm})
                if timeout or process.returncode or elapsed >= 30:
                    row['exclusion'] = 'owned process/output gate'
                else:
                    data = json.loads(out)
                    assert data['policy'] == manifest['policy'] and data['name'] == name
                    assert data['subjectDllSha256'].lower() == prepared['identities']['Lokad.Lython.dll']
                    assert data['subjectInformationalVersion'].endswith('+' + manifest['revision'])
                    assert data['samples'] >= 1000 and data['peakBytes'] <= data['capBytes']
                    assert data['capBytes'] == (28000 if data['pressure'] else 262144)
                    assert data['warmupYieldedJobs'] == data['warmupDelayedHostCalls'] == 0
                    job = name.removesuffix('-control').removesuffix('-sync')
                    fixture_job = 'csv-summary' if data['pressure'] else job
                    expected = {path.removeprefix(fixture_job+'-1024/'): value.upper() for path, value in manifest['fixtureHashes'].items() if path.startswith(fixture_job+'-1024/')}
                    assert data['fixtureHashes'] == expected
                    source = root / 'subject/benchmarks/scripts' / (job+'.py')
                    assert data['originalSourceSha256'].lower() == digest(source)
                    if data['control']:
                        assert data['completed'] == data['yieldedJobs'] == data['delayedHostCalls'] == 0
                    else:
                        assert data['completed'] > 0 and data['yieldedJobs'] == data['delayedHostCalls'] == 0
                    if data['hostGenerationsBefore'] != [0, 0, data['modeledHostObjects']]:
                        row['exclusion'] = 'host graph age gate'
                    elif row['stealTicks'] or any(row['noiseDelta'].values()):
                        row['exclusion'] = 'steal or paging noise'
                    else: row['eligible'] = True
                    row['result'] = {key: value for key, value in data.items() if key not in ('fixtureHashes', 'requestLatencyMilliseconds', 'heartbeatLatenessMilliseconds')}
                    row['request'] = {key: value for key, value in data['requestLatencyMilliseconds'].items() if key != 'rawMilliseconds'}
                    row['heartbeat'] = {key: value for key, value in data['heartbeatLatenessMilliseconds'].items() if key != 'rawMilliseconds'}
        rows.append(row)
        (stage / 'receipt.json').write_text(json.dumps(row, indent=2))
        (results / 'progress.json').write_text(json.dumps(rows, indent=2))
        print(json.dumps({key: row[key] for key in ('session', 'name', 'eligible', 'exclusion')}), flush=True)
assert identities() == prepared['identities']
assert digest(dotnet) == prepared['dotnetSha256']
for name, expected in manifest['files'].items(): assert digest(root / name) == expected
summary = {'policy': manifest['policy'], 'source': manifest['revision'], 'productionTree': manifest['productionTree'],
           'elapsedCampaignSeconds': time.monotonic()-started, 'runtimeOverrides': overrides, 'rows': rows,
           'allChildrenExited': all(row.get('exitCode') is not None for row in rows if 'pid' in row)}
(results / 'summary.json').write_text(json.dumps(summary, indent=2))
print(json.dumps({'entries': len(rows), 'eligible': sum(row['eligible'] for row in rows), 'elapsedSeconds': summary['elapsedCampaignSeconds']}), flush=True)
