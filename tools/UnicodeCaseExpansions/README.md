# Unicode case mapping data

Run from the repository root with CPython 3.13 and Unicode 15.1.0:

```powershell
python -X utf8 -I tools/UnicodeCaseExpansions/generate.py
```

The tool rewrites the non-ASCII lowercase, uppercase, titlecase, swapcase and
casefold mappings and the native and public goldens for full expansions. The tables
share UTF-8 results across operations. They cover 102 uppercase expansions and
48 title expansions alongside the single-scalar mappings. The runtime retains
ASCII bulk casing, contextual rules for title and final sigma, and preflight
admission for final UTF-8 output. The generator requires the pinned Unicode
version. Python is a development reference.

The initial reference was CPython 3.13.2. Native tests exercise upper, title,
capitalize and swapcase for every member; the public aggregate checks the complete
output under both execution APIs and verifies shared-budget cleanup. Separate
ordinary call goldens include the Greek mapping that exposed the incomplete table.
