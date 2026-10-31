import json, sys, textwrap
from pathlib import Path

cases = [
    ("capture-split", """
        import re
        p = re.compile(r'(?P<word>[a-z]+)([0-9]+)?')
        text = 'ab12 cd ef3 ' * 100
        pieces = p.split(text)
        return len(pieces) * 100000 + sum(len(x) for x in pieces if x is not None)
    """),
    ("template-sub", r"""
        import re
        p = re.compile(r'(?P<word>[a-z]+)([0-9]+)?')
        text = 'ab12 cd ef3 ' * 100
        return len(p.sub(r'<\g<word>:\2>', text))
    """),
    ("match-expand", r"""
        import re
        m = re.search(r'(?P<word>[a-z]+)([0-9]+)?', 'ab12')
        total = 0
        for i in range(100):
            total += len(m.expand(r'<\g<word>:\2>'))
        return total
    """),
]
result = []
for name, source in cases:
    source = textwrap.dedent(source).strip() + '\n'
    ns = {}
    exec('def run():\n' + textwrap.indent(source, '    '), ns)
    result.append(dict(Name=name, Source=source, Expected=str(ns['run']())))
Path(sys.argv[1]).write_text(json.dumps(result, indent=2), encoding='utf-8')
print(sys.version)
print([(row['Name'], row['Expected']) for row in result])
