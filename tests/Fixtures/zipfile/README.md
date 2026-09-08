# Lython zipfile Fixtures (Z0)

This directory contains CPython-authored archive fixtures for the planned
contained `zipfile` module. They pin the trusted behavior that later stages
implement; no production code reads them yet.

Layout:

- `cases/<id>/case.json`: fixture manifest, trusted CPython 3.13 observations,
  and expected metadata/failure categories.
- `cases/<id>/input.zip`: the archive payload.

Rules (same hygiene as the openpyxl catalog):

- Fixtures must be small (each `input.zip` is at most 32 KB), synthetic, and
  public. Do not commit customer archives or private files.
- Every entry carries an explicit `date_time`; generation never consults the
  ambient clock. Regeneration is byte-identical for a fixed CPython/zlib pair.
- Compressed bytes may vary across zlib builds (recorded as `producerZlib` in
  each manifest). Assert content, metadata, CRCs, and behavior — never exact
  compressed bytes.
- Corrupt fixtures derive deterministically from a valid stored archive (byte
  flip or truncation); the manifest records the trusted CPython outcome
  (`testzip`, `is_zipfile`, `openError` type and message).

Regenerate (repository root):

```powershell
python tools/NewZipFixtures.py
```

The generator validates its own invariants (duplicate order, CP437 decoding,
descriptor flags, corrupt outcomes) and fails loudly if CPython disagrees.

Validity gate: `ZipFixtureCatalogTests` reads every fixture with the
independent BCL reader plus raw byte checks and documents where BCL behavior
diverges from the Python contract (duplicate lookup, CRC validation, CP437 and
comment decoding, error categories). The future implementation must agree with
the manifests, not with BCL.
