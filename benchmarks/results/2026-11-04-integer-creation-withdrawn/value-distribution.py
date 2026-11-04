"""Untimed CPython value-distribution diagnostic; not Lython allocation profiling."""
import ast
import base64
from collections import Counter
from contextlib import redirect_stdout
import io
import json
import os
from contextlib import contextmanager
from pathlib import Path
import tempfile

root = Path(__file__).resolve().parent
catalog = json.loads((root.parent / 'coupled-cpython-b195/representative.json').read_text(encoding='utf-8'))
original_loads, original_load = json.loads, json.load

@contextmanager
def changed_directory(folder):
    previous = os.getcwd()
    os.chdir(folder)
    try:
        yield
    finally:
        os.chdir(previous)
results = []

class Probe(ast.NodeTransformer):
    def visit_BinOp(self, node):
        self.generic_visit(node)
        return ast.copy_location(ast.Call(ast.Name('_observe', ast.Load()),
            [ast.Constant('arithmetic'), node], []), node)

    def visit_AugAssign(self, node):
        self.generic_visit(node)
        # Preserve non-name augmented-target evaluation semantics unchanged.
        if not isinstance(node.target, ast.Name):
            return node
        expression = ast.BinOp(ast.Name(node.target.id, ast.Load()), node.op, node.value)
        observed = ast.Call(ast.Name('_observe', ast.Load()),
            [ast.Constant('arithmetic'), expression], [])
        return ast.copy_location(ast.Assign([node.target], observed), node)

    def visit_Call(self, node):
        self.generic_visit(node)
        if isinstance(node.func, ast.Name) and node.func.id == 'int':
            return ast.copy_location(ast.Call(ast.Name('_observe', ast.Load()),
                [ast.Constant('int-call'), node], []), node)
        return node

for case in catalog['cases']:
    counts = Counter()
    def observe(category, value):
        if type(value) is int:
            counts[category + ':total'] += 1
            if -5 <= value <= 256:
                counts[category + ':small'] += 1
        return value
    def parse_int(text):
        return observe('json-default-int', int(text))
    def loads(*args, **kwargs):
        kwargs.setdefault('parse_int', parse_int)
        return original_loads(*args, **kwargs)
    def load(*args, **kwargs):
        kwargs.setdefault('parse_int', parse_int)
        return original_load(*args, **kwargs)
    json.loads, json.load = loads, load
    fixture = original_loads(case['fixtureJson'])
    tree = ast.fix_missing_locations(Probe().visit(ast.parse(case['source'])))
    code = compile(tree, '<distribution-only>', 'exec')
    previous = os.getcwd()
    try:
        with tempfile.TemporaryDirectory(dir=root) as directory:
            folder = Path(directory)
            for name, encoded in fixture['inputFiles'].items():
                destination = folder / name
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(base64.b64decode(encoded))
            with changed_directory(folder):
                # Default Windows text encoding differs from the actual UTF-8 host.
                def utf8_open(*args, **kwargs):
                    if len(args) < 2 or 'b' not in args[1]:
                        kwargs.setdefault('encoding', 'utf-8')
                    return open(*args, **kwargs)
                output = io.StringIO()
                with redirect_stdout(output):
                    exec(code, {'_observe': observe, 'open': utf8_open})
                assert output.getvalue() == case['expectedOutput'], case['id']
                for name, encoded in fixture['outputFiles'].items():
                    assert (folder/name).read_bytes() == base64.b64decode(encoded), (case['id'], name)
    finally:
        os.chdir(previous)
        json.loads, json.load = original_loads, original_load
    results.append({'id':case['id'], 'counts':dict(counts)})

report = {'status':'untimed distribution only', 'conditions':
    'CPython 3.13 AST probes after arithmetic and int(); default JSON parse_int callback. '
    'Original fixtures, stdout and output-file bytes verified. Not a Lython allocation/CPU trace; '
    'int(existing) is absent from these catalog sources. No range calls in these five scripts; the one config subscript += is not instrumented.',
    'cases': results}
(root/'value-distribution.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
print(json.dumps(report, indent=2))
