from pathlib import Path
import contextlib
import io
import json
import platform

root = Path(__file__).resolve().parents[3]
fixture = root / 'tests/Lokad.Lython.PublicApi.Tests/IntegerRenderingTests.cs'
rows = []
for line in fixture.read_text().splitlines():
    line = line.strip().removesuffix(',')
    if not line.startswith('{ "'):
        continue
    if line.startswith('{ "10**1000+7"'):
        expression, expected = '10**1000+7', '1' + '0' * 999 + '7'
    else:
        expression, expected = json.loads('[' + line[1:-1] + ']')
    source = 'value=' + expression + "\nprint(str(value),repr(value),f'{value}')\nprint(value)\nprint([value],(value,))"
    stdout = expected + ' ' + expected + ' ' + expected + '\n' + expected + '\n[' + expected + '] (' + expected + ',)\n'
    output = io.StringIO()
    with contextlib.redirect_stdout(output):
        exec(compile(source, '<integer-rendering-oracle>', 'exec'), {})
    assert output.getvalue() == stdout, expression
    rows.append({'expression': expression, 'expected': expected, 'stdout': stdout})
assert len(rows) == 14
receipt = {'python': platform.python_version(), 'flags': '-I -S -X utf8', 'cases': rows}
(Path(__file__).resolve().parent / 'cpython-oracle.json').write_text(json.dumps(receipt, indent=2) + '\n')
print('Fourteen CPython integer-rendering controls match the independent goldens.')
