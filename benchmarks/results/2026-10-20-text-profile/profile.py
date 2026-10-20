import base64
import hashlib
import json
import os
import pathlib
import queue
import shutil
import struct
import subprocess
import sys
import tempfile
import threading
import time

root = pathlib.Path.cwd().resolve()
notes = pathlib.Path(__file__).parent.resolve()
catalog = notes / 'catalog.json'
capture = notes / 'collection'
capture.mkdir()
case = next(c for c in json.loads(catalog.read_text(encoding='utf-8-sig'))['cases']
    if c['id'] == 'scripts.text-summary-helpers.large')
dotnet, tracer = sys.argv[1:3]
benchmark = root / 'benchmarks/Lokad.Lython.Benchmarks/bin/Release/net10.0/Lokad.Lython.Benchmarks.dll'
started = time.monotonic()
deadline = started + 24
receipt = {'revision': '522e2f53347fbf089311ad5400aa53b84634e863', 'runtimeRevision': 'f0cd695fd883915f0989a6fb88844c35f3499d57',
    'caseId': case['id'], 'lane': 'compile-run', 'iterations': 384, 'traceSeconds': 8,
    'sourceSha256': case['sourceSha256'], 'fixtureSha256': case['fixtureSha256'],
    'expectedOutputSha256': case['expectedOutputSha256'], 'expectedFilesSha256': case['expectedFilesSha256'],
    'catalogSha256': hashlib.sha256(catalog.read_bytes()).hexdigest(),
    'startedUtc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'traceCommand': None}
worker = trace = None
reader = None
worker_error = trace_output = None
fixture = pathlib.Path(tempfile.mkdtemp(prefix='lython-comparison-files-')).resolve()
temp_root = pathlib.Path(tempfile.gettempdir()).resolve()
assert fixture.parent == temp_root and fixture.name.startswith('lython-comparison-files-') and not fixture.is_symlink()
receipt['fixtureDirectory'] = str(fixture)
frames = queue.Queue()

def remaining():
    seconds = deadline - time.monotonic()
    if seconds <= 0:
        raise TimeoutError('Owned profile deadline expired')
    return seconds

def pump():
    try:
        def exact(size):
            data = bytearray()
            while len(data) < size:
                part = worker.stdout.read(size - len(data))
                if not part:
                    raise EOFError('Worker frame ended')
                data.extend(part)
            return data
        while True:
            size = struct.unpack('>I', exact(4))[0]
            if not 0 < size <= 4 * 1024 * 1024:
                raise ValueError('Invalid existing protocol frame size')
            frames.put(json.loads(exact(size)))
    except BaseException as error:
        frames.put(error)

def response():
    frame = frames.get(timeout=remaining())
    if isinstance(frame, BaseException):
        raise frame
    return frame

def send(value):
    data = json.dumps(value, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
    worker.stdin.write(struct.pack('>I', len(data)) + data)
    worker.stdin.flush()

def request(number, operation, **extra):
    value = {key: case[key] for key in ('sourceSha256', 'fixtureSha256', 'expectedOutputSha256')}
    value.update(protocolVersion=1, requestId=number, operation=operation, caseId=case['id'], **extra)
    send(value)
    result = response()
    expected_status = 'Equivalent' if operation == 'verify' else 'Completed'
    assert result['status'] == expected_status and result['requestId'] == number, result
    for key in ('sourceSha256', 'fixtureSha256'):
        assert result[key] == case[key], (key, result)
    assert result['actualOutputSha256'] == case['expectedOutputSha256'], result
    assert result['actualFilesSha256'] == case['expectedFilesSha256'], result
    return result

try:
    worker_error = (capture / 'worker.stderr.txt').open('wb')
    worker = subprocess.Popen([dotnet, str(benchmark), '--compare', 'worker', '--catalog', str(catalog)],
        cwd=fixture, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=worker_error,
        creationflags=subprocess.CREATE_NO_WINDOW)
    reader = threading.Thread(target=pump, name='owned-profile-frame-reader')
    reader.start()
    ready = response()
    assert ready['status'] == 'Ready' and ready['processId'] == worker.pid
    assert ready['catalogSha256'] == receipt['catalogSha256']
    assert pathlib.Path(ready['fileFixtureDirectory']).resolve() == fixture
    expected_core = '3af5653d480e92a53d6209360ce3a3dcc87ecb3a88b101b889e4ec167d53ffed'
    assert any(lib['sha256'].lower() == expected_core for lib in ready['libraries'])
    receipt['identity'] = ready
    receipt['verification'] = request(1, 'verify')
    assert receipt['verification']['completedInvocations'] == 2
    trace_path = capture / 'text.nettrace'
    command = [tracer, 'collect', '--process-id', str(worker.pid), '--profile', 'dotnet-sampled-thread-time',
        '--duration', '00:00:00:08', '--buffersize', '64', '--format', 'Speedscope', '--output', str(trace_path)]
    receipt['traceCommand'] = command
    trace_output = (capture / 'trace.stdout.txt').open('wb')
    trace = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=trace_output, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW)
    receipt['tracePid'] = trace.pid
    # Redirected dotnet-trace omits its interactive recording message. Keep
    # alignment approximate and qualify capture from the resulting stacks.
    time.sleep(0.5)
    assert trace.poll() is None, 'Profiler must still be live when work starts'
    receipt['profilerStartupDelaySeconds'] = 0.5
    receipt['batchStartSecondsAfterLaunch'] = time.monotonic() - started
    receipt['batch'] = request(2, 'batch', lane='compile-run', iterations=384)
    assert receipt['batch']['completedInvocations'] == 384
    receipt['batchEndSecondsAfterLaunch'] = time.monotonic() - started
    # Keep the worker alive until rundown/format conversion finishes.
    assert trace.wait(timeout=remaining()) == 0
    send({'protocolVersion': 1, 'requestId': 3, 'operation': 'quit'})
    receipt['close'] = response()
    assert receipt['close']['status'] == 'Closed'
    worker.stdin.close()
    assert worker.wait(timeout=remaining()) == 0
    receipt['status'] = 'Completed'
except BaseException as error:
    receipt['status'] = 'Inconclusive'
    receipt['error'] = repr(error)
    raise
finally:
    for process in (trace, worker):
        if process is not None and process.poll() is None:
            process.kill()
            process.wait(timeout=1)
    if worker is not None:
        if not worker.stdin.closed:
            worker.stdin.close()
        worker.stdout.close()
    if reader is not None:
        reader.join(timeout=1)
        assert not reader.is_alive(), 'Owned reader must finish'
    if worker_error is not None:
        worker_error.close()
    if trace_output is not None:
        trace_output.close()
    receipt['workerExitCode'] = None if worker is None else worker.returncode
    receipt['traceExitCode'] = None if trace is None else trace.returncode
    receipt['readerFinished'] = reader is None or not reader.is_alive()
    assert fixture.parent == temp_root and fixture.name.startswith('lython-comparison-files-') and not fixture.is_symlink()
    shutil.rmtree(fixture)
    receipt['fixtureRemoved'] = not fixture.exists()
    receipt['elapsedSecondsIncludingCleanup'] = time.monotonic() - started
    receipt['files'] = {p.name: {'bytes': p.stat().st_size, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
        for p in capture.glob('text.*') if p.is_file()}
    (capture / 'profile-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
print('One owned profile completed: 384 fresh compile/run jobs, full goldens checked, worker/profiler exited, fixture removed')
