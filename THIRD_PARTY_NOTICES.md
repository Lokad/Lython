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

The managed floating divmod algorithm in
`src/Lokad.Lython/Runtime/Numbers/PyNumberOps.cs` is adapted from CPython 3.13's
`_float_div_mod`. Copyright (c) 2001–2026 Python Software Foundation, under the
same licenses reproduced above.

Source: https://github.com/python/cpython/blob/24d048d3601c2f1d41fa357897c1c91bb0a0bc13/Objects/floatobject.c
