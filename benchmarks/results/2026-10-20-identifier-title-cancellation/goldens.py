import json
import platform
from pathlib import Path

cases = [
    ('empty', ''), ('underscore', '_'), ('keyword', 'for'),
    ('identifier-digit', 'a1'), ('leading-digit', '1a'),
    ('title', 'Hello World'), ('lower-first', 'hello World'),
    ('upper', 'HELLO'), ('punctuation', 'Hello-World'),
    ('combining', 'a\u0301'), ('leading-combining', '\u0301a'),
    ('title-combining', 'A\u0301B'), ('titlecase', '\u01c5a'),
    ('ideograph', '\u56db'), ('arabic', '\u0661'),
    ('astral-lower', '\U00010428'), ('astral-title', '\U00010400\U00010428'),
    ('other-id-start', '\u2118x'), ('other-id-continue', 'a\u00b7b'),
    ('boundary-astral', 'a' * 63 + '\U00010428a'),
    ('boundary-combining', 'a' * 63 + '\u0301a'),
    ('boundary-title-astral', 'A' + 'a' * 62 + '\U00010428a'),
    ('boundary-titlecase', 'A' + 'a' * 62 + ' \u01c5a'),
    ('boundary-two-byte', 'a' * 63 + '\u00e9a'),
]
data = {'reference': platform.python_version(), 'cases': [
    {'name': name, 'input': text, 'expected': ''.join('1' if x else '0' for x in (text.isidentifier(), text.istitle()))}
    for name, text in cases
]}
Path(__file__).with_name('goldens.json').write_text(json.dumps(data, ensure_ascii=True, indent=2) + '\n', encoding='utf-8')
print(json.dumps(data, ensure_ascii=True))
