"""Persistent comparison worker; trusted catalog, externally supervised process."""

import contextlib
import gc
import hashlib
import io
import json
import os
import struct
import sys
import time

PROTOCOL_VERSION = 1
MAX_FRAME_BYTES = 4 * 1024 * 1024
MAX_CATALOG_BYTES = 64 * 1024 * 1024
MAX_BATCH_ITERATIONS = 1_000_000
MAX_BATCH_SECONDS = 60
MAX_OUTPUT_BYTES = 16 * 1024 * 1024


def digest(text):
    return hashlib.sha256(text.encode('utf-8', errors='strict')).hexdigest()


def strict_json(text, maximum_depth):
    def invalid_constant(value):
        raise ValueError('Non-JSON numeric constant: ' + value)

    def unique_object(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('Duplicate JSON property: ' + key)
            result[key] = value
        return result

    value = json.loads(text, parse_constant=invalid_constant, object_pairs_hook=unique_object)
    pending = [(value, 1)]
    while pending:
        node, depth = pending.pop()
        if type(node) in (dict, list):
            if depth > maximum_depth:
                raise ValueError('JSON exceeds the declared nesting depth')
            children = node.values() if type(node) is dict else node
            pending.extend((child, depth + 1) for child in children if type(child) in (dict, list))
    return value


def read_exact(stream, count):
    result = bytearray()
    while len(result) < count:
        part = stream.read(count - len(result))
        if not part:
            raise EOFError('Truncated comparison frame')
        result.extend(part)
    return bytes(result)


def read_frame(stream):
    first = stream.read(1)
    if not first:
        return None
    length = struct.unpack('>I', first + read_exact(stream, 3))[0]
    if not 0 < length <= MAX_FRAME_BYTES:
        raise ValueError('Invalid comparison frame length')
    frame = strict_json(read_exact(stream, length).decode('utf-8', errors='strict'), 32)
    if type(frame) is not dict:
        raise ValueError('A comparison frame must be a JSON object')
    return frame


def write_frame(stream, response):
    payload = json.dumps(response, ensure_ascii=False, separators=(',', ':')).encode('utf-8', errors='strict')
    if not 0 < len(payload) <= MAX_FRAME_BYTES:
        raise ValueError('Comparison response exceeds the frame limit')
    stream.write(struct.pack('>I', len(payload)))
    stream.write(payload)
    stream.flush()


class JobFailure(Exception):
    def __init__(self, status, reason, actual=None):
        super().__init__(reason)
        self.status = status
        self.actual = actual


class BoundedCapture(io.StringIO):
    def __init__(self):
        super().__init__()
        self.byte_count = 0

    def write(self, text):
        self.byte_count += len(text.encode('utf-8', errors='strict'))
        if self.byte_count > MAX_OUTPUT_BYTES:
            raise JobFailure('BudgetDenied', 'Reference capture exceeds its declared 16 MiB adapter cap')
        return super().write(text)


def load_manifest(path):
    with open(path, 'rb') as catalog_file:
        if not 0 < os.fstat(catalog_file.fileno()).st_size <= MAX_CATALOG_BYTES:
            raise ValueError('Invalid comparison catalog size')
        data = catalog_file.read(MAX_CATALOG_BYTES + 1)
    if not 0 < len(data) <= MAX_CATALOG_BYTES:
        raise ValueError('Invalid comparison catalog size')
    catalog = strict_json(data.decode('utf-8', errors='strict'), 64)
    if (type(catalog['schemaVersion']) is not int or catalog['schemaVersion'] != 1
            or type(catalog['catalogVersion']) is not int or catalog['catalogVersion'] != 1):
        raise ValueError('Unsupported comparison catalog version')
    rows = catalog['cases']
    if not 1 <= len(rows) <= 256:
        raise ValueError('Invalid comparison case count')
    cases = {}
    for case in rows:
        if (not 1 <= len(case['id'].encode('utf-16-le', errors='strict')) // 2 <= 128
                or type(case['size']) is not int or not 0 <= case['size'] <= 2_147_483_647
                or not 1 <= len(case['source'].encode('utf-16-le', errors='strict')) // 2 <= 1_000_000):
            raise ValueError('Catalog case exceeds declared bounds')
        if len(case['expectedOutput'].encode('utf-8', errors='strict')) > MAX_OUTPUT_BYTES:
            raise ValueError('Expected output exceeds declared bounds')
        fixture = strict_json(case['fixtureJson'], 64)
        if (type(fixture['size']) is not int or fixture['size'] != case['size']
                or (fixture['text'] is not None and type(fixture['text']) is not str)):
            raise ValueError('Invalid fixture size/text metadata')
        for source, checksum in (('source', 'sourceSha256'), ('fixtureJson', 'fixtureSha256'),
                                 ('expectedOutput', 'expectedOutputSha256')):
            if digest(case[source]) != case[checksum]:
                raise ValueError('Catalog digest mismatch: ' + case['id'])
        if case['id'] in cases:
            raise ValueError('Duplicate comparison case')
        cases[case['id']] = case
    return hashlib.sha256(data).hexdigest(), cases


def compile_case(case):
    try:
        return compile(case['source'], case['id'] + '.py', 'exec')
    except BaseException as failure:
        raise JobFailure('Failure', type(failure).__name__ + ': ' + str(failure)) from failure


def invoke(code, case):
    # Fresh namespace, bounded text capture/extraction and full cheap output
    # equality belong to every timed invocation. Preserve ordinary cyclic GC.
    stdout, stderr = BoundedCapture(), BoundedCapture()
    try:
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            exec(code, {'__name__': '__main__'})
    except JobFailure:
        raise
    except BaseException as failure:
        raise JobFailure('BudgetDenied' if isinstance(failure, MemoryError) else 'Failure',
                         type(failure).__name__ + ': ' + str(failure), stdout.getvalue()) from failure
    actual = stdout.getvalue()
    if actual != case['expectedOutput'] or stderr.getvalue():
        raise JobFailure('Mismatch', 'Complete result differs from the independent golden', actual)
    return actual


def response(case, request_id, status, completed, elapsed=None, actual=None, reason=None):
    return {'protocolVersion': PROTOCOL_VERSION, 'requestId': request_id, 'caseId': case['id'], 'status': status,
            'completedInvocations': completed, 'elapsedTicks': elapsed, 'clockFrequency': 1_000_000_000,
            'sourceSha256': case['sourceSha256'], 'fixtureSha256': case['fixtureSha256'],
            'expectedOutputSha256': case['expectedOutputSha256'],
            'actualOutputSha256': None if actual is None else digest(actual), 'reason': reason}


def serve(path, input_stream, output_stream):
    # Full provenance is untimed in persistent lanes. Keep these heavyweight
    # metadata imports out of the fresh-process entry.
    import platform
    import sysconfig
    if not sys.flags.isolated or not sys.flags.no_site or not gc.isenabled():
        raise ValueError('Launch with -I -S and ordinary enabled cyclic GC')
    catalog_hash, cases = load_manifest(path)
    compiled, verified = {}, set()
    with open(sys.executable, 'rb') as executable:
        executable_hash = hashlib.file_digest(executable, 'sha256').hexdigest()
    with open(__file__, 'rb') as adapter:
        adapter_hash = hashlib.file_digest(adapter, 'sha256').hexdigest()
    clock = time.get_clock_info('perf_counter')
    write_frame(output_stream, {
        'protocolVersion': PROTOCOL_VERSION, 'status': 'Ready', 'engine': 'CPython', 'catalogSha256': catalog_hash,
        'catalogVersion': 1, 'processId': os.getpid(), 'version': sys.version, 'executable': sys.executable,
        'executableSha256': executable_hash, 'adapterSha256': adapter_hash,
        'architecture': platform.machine(), 'platform': platform.platform(), 'flags': repr(sys.flags),
        'paths': sys.path, 'configArgs': sysconfig.get_config_var('CONFIG_ARGS'),
        'debug': bool(sysconfig.get_config_var('Py_DEBUG')), 'freeThreaded': bool(sysconfig.get_config_var('Py_GIL_DISABLED')),
        'gilEnabled': sys._is_gil_enabled() if hasattr(sys, '_is_gil_enabled') else True, 'gcEnabled': gc.isenabled(),
        'clockFrequency': 1_000_000_000, 'clockResolutionSeconds': clock.resolution, 'clockMonotonic': clock.monotonic,
        'clockImplementation': clock.implementation, 'maximumFrameBytes': MAX_FRAME_BYTES,
        'maximumBatchIterations': MAX_BATCH_ITERATIONS, 'maximumBatchSeconds': MAX_BATCH_SECONDS,
        'captureByteLimit': MAX_OUTPUT_BYTES})
    last_request_id = 0
    while (request := read_frame(input_stream)) is not None:
        request_id = request['requestId']
        if (type(request_id) is not int or not last_request_id < request_id <= 2_147_483_647
                or type(request['protocolVersion']) is not int or request['protocolVersion'] != PROTOCOL_VERSION):
            raise ValueError('Invalid protocol version or request sequence')
        last_request_id = request_id
        operation = request['operation']
        if operation == 'quit':
            write_frame(output_stream, {'protocolVersion': PROTOCOL_VERSION, 'requestId': request_id, 'status': 'Closed'})
            return
        if operation not in ('verify', 'batch'):
            raise ValueError('Unknown comparison operation')
        case = cases[request['caseId']]
        for name in ('sourceSha256', 'fixtureSha256', 'expectedOutputSha256'):
            if request[name] != case[name]:
                raise ValueError('Wrong comparison source/fixture/output digest')
        completed, actual, elapsed = 0, None, None
        try:
            if operation == 'verify':
                code = compile_case(case)
                for _ in range(2):
                    actual = invoke(code, case)
                    completed += 1
                compiled[case['id']] = code
                verified.add(case['id'])
                status = 'Equivalent'
            else:
                lane, iterations = request['lane'], request['iterations']
                if case['id'] not in verified or type(iterations) is not int or not 1 <= iterations <= MAX_BATCH_ITERATIONS:
                    raise ValueError('A batch needs a verified case and bounded positive count')
                if lane not in ('warm', 'compile-run', 'compile'):
                    raise ValueError('Unsupported comparison lane')
                last_code = None
                started = time.perf_counter_ns()
                for _ in range(iterations):
                    if time.perf_counter_ns() - started >= MAX_BATCH_SECONDS * 1_000_000_000:
                        raise JobFailure('Timeout', 'Batch exceeded its internal 60-second ceiling')
                    code = compiled[case['id']] if lane == 'warm' else compile_case(case)
                    if lane == 'compile':
                        last_code = code
                    else:
                        actual = invoke(code, case)
                    completed += 1
                elapsed = time.perf_counter_ns() - started
                if last_code is not None:
                    compiled[case['id']] = last_code
                status = 'Completed'
            write_frame(output_stream, response(case, request_id, status, completed, elapsed, actual))
        except JobFailure as failure:
            verified.discard(case['id'])
            write_frame(output_stream, response(case, request_id, failure.status, completed,
                                               actual=failure.actual, reason=str(failure)))


def serve_once(path, output_stream):
    if not sys.flags.isolated or not sys.flags.no_site or not gc.isenabled():
        raise ValueError('Launch with -I -S and ordinary enabled cyclic GC')
    catalog_hash, cases = load_manifest(path)
    if len(cases) != 1:
        raise ValueError('A fresh-process payload must contain exactly one case')
    case = next(iter(cases.values()))
    completed, actual, reason = 0, None, None
    try:
        code = compile_case(case)
        actual = invoke(code, case)
        status, completed = 'Equivalent', 1
    except JobFailure as failure:
        status, actual, reason = failure.status, failure.actual, str(failure)
    # Binary and full build/provenance hashes are checked by the parent outside
    # samples. This path deliberately avoids sysconfig/platform/argparse imports.
    identity = {
        'protocolVersion': PROTOCOL_VERSION, 'status': 'Ready', 'engine': 'CPython',
        'catalogSha256': catalog_hash, 'catalogVersion': 1, 'processId': os.getpid(),
        'version': sys.version, 'executable': sys.executable, 'isolated': bool(sys.flags.isolated),
        'noSite': bool(sys.flags.no_site), 'gcEnabled': gc.isenabled(),
        'gilEnabled': sys._is_gil_enabled() if hasattr(sys, '_is_gil_enabled') else True,
        'metadataModulesLoaded': [name for name in ('sysconfig', 'platform', 'argparse') if name in sys.modules],
        'clockFrequency': 1_000_000_000, 'maximumFrameBytes': MAX_FRAME_BYTES,
        'maximumBatchIterations': MAX_BATCH_ITERATIONS, 'maximumBatchSeconds': MAX_BATCH_SECONDS,
        'captureByteLimit': MAX_OUTPUT_BYTES}
    write_frame(output_stream, {'identity': identity,
        'response': response(case, 1, status, completed, actual=actual, reason=reason)})


def main():
    sys.stderr.reconfigure(encoding='utf-8', errors='backslashreplace')
    if len(sys.argv) == 4 and sys.argv[1:3] == ['--once', '--catalog']:
        try:
            serve_once(sys.argv[3], sys.stdout.buffer)
            return 0
        except BaseException as failure:
            print('Comparison once error: ' + type(failure).__name__ + ': ' + str(failure), file=sys.stderr)
            return 2
    import argparse
    import pathlib
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--catalog', type=pathlib.Path, required=True)
    arguments = parser.parse_args()
    try:
        serve(arguments.catalog, sys.stdin.buffer, sys.stdout.buffer)
        return 0
    except BaseException as failure:
        print('Comparison worker error: ' + type(failure).__name__ + ': ' + str(failure), file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
