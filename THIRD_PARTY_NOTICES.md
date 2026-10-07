# Third-party notices

The HTML5 entity mapping in `src/Lokad.Lython/Runtime/Text/Data/html5.json` and numeric-reference
conversion data are derived from CPython 3.13.16's `html.entities` and `html`
modules. Copyright (c) 2001–2026 Python Software Foundation. They are distributed
under the Python Software Foundation License and applicable historical Python
licenses, reproduced in [Python.LICENSE.txt](third_party/Python.LICENSE.txt).

The mapping is serialized as sorted compact UTF-8 JSON. Lython implements its
own bounded UTF-8 scanner and guest protocol dispatch. It does not include
Python's parser or load an ambient Python installation at runtime.

Source: https://github.com/python/cpython/tree/v3.13.16/Lib/html
