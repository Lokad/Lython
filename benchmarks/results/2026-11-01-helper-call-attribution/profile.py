import datetime, json, os, pathlib, select, shutil, subprocess, time

root = pathlib.Path(__file__).resolve().parent
dotnet = pathlib.Path.home() / 'lython-bench-toolchains/dotnet/11.0.100-rc.3.26522.148/dotnet'
baseline = root.parent / 'string-windows-b64-20261101/baseline/Lokad.Lython.Benchmarks.dll'
env = os.environ.copy()
env['DOTNET_ROOT'] = str(dotnet.parent)
env['DOTNET_PerfMapEnabled'] = '1'
ticks = os.sysconf('SC_CLK_TCK')

def threads(pid):
    result = {}
    for task in pathlib.Path(f'/proc/{pid}/task').iterdir():
        try:
            text = (task / 'stat').read_text()
            fields = text[text.rfind(')') + 2:].split()
            result[task.name] = {'name': (task / 'comm').read_text().strip(), 'cpu': (int(fields[11]) + int(fields[12])) / ticks}
        except OSError:
            pass
    return result

stderr = (root/'driver.stderr').open('w')
process = subprocess.Popen([str(dotnet), str(root/'jobs/bin/Release/net11.0/complete-jobs.dll'), str(baseline), str(root/'catalog.json'), str(root/'hash-witness.json')], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=stderr, env=env)

def message():
    if not select.select([process.stdout], [], [], 15)[0]: raise TimeoutError('Driver response')
    line = process.stdout.readline()
    if not line: raise EOFError('Driver exited')
    return json.loads(line)

def command(value):
    process.stdin.write((value+'\n').encode()); process.stdin.flush()

total = 0.0
profiler = None
try:
    for label in ['original', 'expanded', 'original-control']:
        ready = message()
        assert ready['phase'] == 'ready' and ready['label'] == label, ready
        assert ready['files']['Lokad.Lython.dll'].lower() == '526b0a185fd4e3d9a960ea2734f4175c6f1cd58ba7e0d829955156254de8ce99', ready
        folder = root/'perf'/label
        folder.mkdir(parents=True, exist_ok=False)
        for name in ['control', 'ack']: os.mkfifo(folder/name)
        control = os.open(folder/'control', os.O_RDWR | os.O_NONBLOCK)
        ack = os.open(folder/'ack', os.O_RDWR | os.O_NONBLOCK)
        log = (folder/'collector.log').open('w')
        def perf_command(value, wait=True):
            os.write(control, (value+'\n').encode())
            if wait:
                if not select.select([ack], [], [], 4)[0]: raise TimeoutError('perf acknowledgment')
                assert os.read(ack,64).rstrip(b'\0\r\n') == b'ack'
        profiler = subprocess.Popen(['sudo','-n','perf','record','-e','cpu-clock','-F','199','--call-graph','fp','--pid',str(process.pid),'--delay=-1','--control',f'fifo:{folder}/control,{folder}/ack','-o',str(folder/'perf.data')], stdout=log, stderr=subprocess.STDOUT, env=env)
        try:
            before = threads(process.pid)
            perf_command('enable')
            started = datetime.datetime.now(datetime.timezone.utc).isoformat()
            clock = time.monotonic()
            command('go')
            complete = message()
            perf_command('disable')
            seconds = time.monotonic() - clock
            stopped = datetime.datetime.now(datetime.timezone.utc).isoformat()
            after = threads(process.pid)
            total += seconds
            perf_command('stop', wait=False)
            profiler.wait(timeout=4)
            assert profiler.returncode == 0
            record = {'ready':ready, 'response':complete, 'startedUtc':started, 'stoppedUtc':stopped, 'captureWallSeconds':seconds, 'performanceQualified':False, 'threadCpuSeconds':[{'tid':tid, 'name':v['name'], 'seconds':v['cpu']-before.get(tid, {'cpu':0})['cpu']} for tid,v in after.items()]}
            guest = [x for x in record['threadCpuSeconds'] if x['name'].startswith('Lython executio') and x['seconds'] > 0]
            assert len(guest) == 1, record
            record['guestTid'] = guest[0]['tid']
            (folder/'record.json').write_text(json.dumps(record,indent=2)+'\n')
            shutil.copyfile(f'/tmp/perf-{process.pid}.map', folder/'perf.map')
            assert complete['phase'] == 'complete' and complete['jobs'] == 384 and complete['fullGoldensPassed'] and complete['executionBalancesReturned'], complete
            assert seconds <= 6 and total <= 15, (seconds,total)
            print(json.dumps({'label':label, 'seconds':seconds,'threads':record['threadCpuSeconds']}), flush=True)
            command('next')
        finally:
            if profiler.poll() is None:
                perf_command('stop',wait=False)
                try:profiler.wait(timeout=3)
                except subprocess.TimeoutExpired:profiler.kill();profiler.wait()
            log.close();os.close(control);os.close(ack)
    closed = message()
    assert closed['phase'] == 'closed' and closed['allBalancesZero'] and closed['ownedFixturesRemoved'], closed
    process.wait(timeout=3)
    assert process.returncode == 0
    (root/'closed.json').write_text(json.dumps({'closed':closed,'exitCode':process.returncode,'totalCaptureSeconds':total},indent=2)+'\n')
finally:
    if process.poll() is None:
        process.kill();process.wait()
    stderr.close()
