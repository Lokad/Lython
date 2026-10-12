"""Check complete benchmark scripts against independent output/failure goldens.

Outputs are preserved under --output-dir, which must be new. This is a
correctness driver, not a timing harness. Each Python child has a 10 s timeout.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output-dir', type=Path, required=True)
args = parser.parse_args()
args.output_dir.mkdir(parents=True, exist_ok=False)
scripts = Path(__file__).resolve().parents[1] / 'benchmarks' / 'scripts'
checks = []
for n in (0, 16, 1024):
    cases = {
        'csv-summary': (
            {'input.csv': ('kind,amount,note\n' + 'red,3,"café,\nquoted"\nblue,2,😀\nred,-1,skip\nblue,5,ok\n' * n).encode()},
            {'report.csv': ('kind,total\n' + (f'blue,{7*n}\nred,{3*n}\n' if n else '')).encode()}, f'{3*n}\n'),
        'text-summary': (
            {'input.txt': ('Alpha café 😀\nalpha BETA café\n\n' * n).encode()},
            {'report.txt': (f'alpha={2*n}\nbeta={n}\ncafé={2*n}\n😀={n}\n' if n else '').encode()}, f'{6*n}\n'),
        'json-summary': (
            {'input.jsonl': ('{"kind":"red","amount":3}\n{"kind":"blue","amount":2}\n{"kind":"red","amount":-1}\n{"kind":"blue","amount":5}\n\n' * n).encode()},
            {'report.json': ((f'{{"blue":{7*n},"red":{3*n}}}' if n else '{}') + '\n').encode()}, f'{3*n}\n'),
        'file-manifest': (
            {'inbox/a.bin': bytes((0, 127, 128, 255)) * n, 'inbox/é.txt': 'café 😀\n'.encode()},
            {'outbox/a.bin': bytes((0, 127, 128, 255)) * n, 'outbox/é.txt': 'café 😀\n'.encode(),
             'manifest.json': (json.dumps([
                 {'checksum': 510*n, 'name': 'a.bin', 'size': 4*n},
                 {'checksum': 1383, 'name': 'é.txt', 'size': 11}], sort_keys=True, separators=(',', ':')) + '\n').encode()}, '2\n')
    }
    for name, (inputs, outputs, stdout) in cases.items():
        directory = args.output_dir / f'{name}-{n}'
        directory.mkdir()
        for path, payload in inputs.items():
            target = directory / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(payload)
        script = scripts / (name + '.py')
        result = subprocess.run([sys.executable, '-I', '-X', 'utf8', str(script)], cwd=directory,
                                capture_output=True, timeout=10, check=False)
        (directory / 'stdout.bin').write_bytes(result.stdout)
        (directory / 'stderr.bin').write_bytes(result.stderr)
        actual = {path: (directory / path).read_bytes() for path in outputs}
        # Standard output is text at the embedding boundary; normalize its
        # platform line endings. Published file bytes remain exact.
        passed = result.returncode == 0 and not result.stderr and result.stdout.replace(b'\r\n', b'\n') == stdout.encode() and actual == outputs
        checks.append({'script': name, 'repetitions': n, 'passed': passed,
                       'sourceSha256': hashlib.sha256(script.read_bytes()).hexdigest(),
                       'inputSha256': {p: hashlib.sha256(b).hexdigest() for p, b in inputs.items()},
                       'outputSha256': {p: hashlib.sha256(b).hexdigest() for p, b in actual.items()}})
# Negative whole-job controls. Generic mediated-host failures in Lython have a
# documented RuntimeError contract; these record CPython's concrete filesystem
# errors independently rather than pretending the two host models are identical.
failure_wrapper = """import json, pathlib, runpy, sys
try:
    runpy.run_path(sys.argv[1], run_name='__main__')
except Exception as error:
    pathlib.Path('failure.json').write_text(json.dumps({'type': type(error).__name__, 'message': str(error)}), encoding='utf8')
    sys.exit(1)
"""
for name, inputs, expected in (
    ('csv-summary', {'input.csv': b'kind,amount,note\nred,nope,invalid\n'}, 'ValueError'),
    ('json-summary', {'input.jsonl': b'{"kind":"red","amount":invalid}\n'}, 'JSONDecodeError'),
    ('text-summary', {}, 'FileNotFoundError'),
    ('file-manifest', {}, 'FileNotFoundError'),
):
    directory = args.output_dir / (name + '-negative')
    directory.mkdir()
    for path, payload in inputs.items():
        (directory / path).write_bytes(payload)
    script = scripts / (name + '.py')
    result = subprocess.run([sys.executable, '-I', '-X', 'utf8', '-c', failure_wrapper, str(script)],
                            cwd=directory, capture_output=True, timeout=10, check=False)
    failure_path = directory / 'failure.json'
    failure = json.loads(failure_path.read_text(encoding='utf8')) if failure_path.exists() else {}
    published = sorted(str(path.relative_to(directory)) for path in directory.rglob('*')
                       if path.is_file() and str(path.relative_to(directory)) not in inputs and path.name != 'failure.json')
    (directory / 'stdout.bin').write_bytes(result.stdout)
    (directory / 'stderr.bin').write_bytes(result.stderr)
    passed = result.returncode == 1 and failure.get('type') == expected and not result.stdout and not result.stderr and not published
    checks.append({'script': name, 'variant': 'negative', 'passed': passed,
                   'expectedException': expected, 'failure': failure, 'publishedFiles': published,
                   'sourceSha256': hashlib.sha256(script.read_bytes()).hexdigest(),
                   'inputSha256': {path: hashlib.sha256(payload).hexdigest() for path, payload in inputs.items()}})
receipt = {'python': sys.version, 'executable': sys.executable, 'checks': checks}
(args.output_dir / 'receipt.json').write_text(json.dumps(receipt, indent=2), encoding='utf8')
failed = sum(not check['passed'] for check in checks)
print(f'{len(checks)} complete scripts checked; {failed} failed')
sys.exit(1 if failed else 0)
