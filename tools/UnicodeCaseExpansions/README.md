# Unicode case expansion data

Run from the repository root with CPython 3.13 and Unicode 15.1.0:

```powershell
python -X utf8 -I tools/UnicodeCaseExpansions/generate.py
```

The tool rewrites the finite multi-scalar uppercase/title mapping and its native
and public CPython goldens. The reference set contains 102 uppercase expansions
and 48 title expansions. Single-scalar mappings, contextual casing, UTF-8 storage
and admission remain in Lython's existing managed implementation. The generator
refuses a different Unicode version; Python is a development reference only and
is not a runtime dependency.

The initial reference was CPython 3.13.2. Native tests exercise upper, title,
capitalize and swapcase for every member; the public aggregate checks the complete
output under both execution APIs and verifies shared-budget cleanup. Separate
ordinary call goldens include the Greek mapping that exposed the incomplete table.
