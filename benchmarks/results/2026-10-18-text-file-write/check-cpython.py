from pathlib import Path
import contextlib
import io
import json
import platform
import tempfile

root = Path(__file__).resolve().parents[3]
fixture = root / 'tests/Lokad.Lython.PublicApi.Tests/TextFileWriteCallTests.cs'
results = []
for line in fixture.read_text(encoding='utf-8').splitlines():
    line = line.strip().removesuffix(',')
    if not line.startswith('{ "'):
        continue
    name, source, stdout, content = json.loads('[' + line[1:-1] + ']')
    with tempfile.TemporaryDirectory(prefix='lython-text-write-') as temp:
        paths = {p: Path(temp) / p[1:] for p in ['/out', '/other']}
        for old, path in paths.items():
            source = source.replace(repr(old), repr(str(path)))
        namespace = {}
        output = io.StringIO()
        try:
            with contextlib.redirect_stdout(output):
                exec(compile(source, '<text-write-oracle>', 'exec'), namespace)
        finally:
            for value in list(namespace.values()):
                if isinstance(value, io.IOBase):
                    value.close()
        assert output.getvalue() == stdout, (name, output.getvalue(), stdout)
        assert paths['/out'].read_text(encoding='utf-8') == content
        if name == 'receiver-before-argument':
            assert paths['/other'].read_bytes() == b''
        results.append({'name': name, 'stdout': output.getvalue(), 'fileUtf8': paths['/out'].read_bytes().hex()})
assert len(results) == 5
receipt = {'python': platform.python_version(), 'flags': '-I -S -X utf8', 'cases': results}
(Path(__file__).resolve().parent / 'cpython-oracle.json').write_text(json.dumps(receipt, indent=2) + '\n')
print('Five isolated CPython controls match the independent golden outputs.')
