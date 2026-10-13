# Immutable scalar.44 package assessment

On 2026-10-13, Lython assessed core/PythonRe **0.4.0-scalar.44**, Release/net10.0
producer **7e2b7e4d2cf39a9a4fb7c42f56323bfc38be4fac**. Package SHA256/SHA512,
symbols/PDBs, DLL hashes, nuspec producer and PythonRe's core dependency match
the frozen manifest. Loaded DLL informational versions and hashes also match.
No sibling source was rebuilt and no package was published or promoted.

The first mandatory downstream gate fails. The core DLL still references
Unsafe.Add/As/AsRef/ReadUnaligned/SizeOf and Vector128/256.LoadUnsafe, violating
Lython's lasting managed-only constraint. The reused member-reference gate finds
no Unsafe references in the PythonRe DLL, resolving its earlier octal witness.
That improvement does not establish compliance of the core/PythonRe pair.

The audit completes in **92 ms** with a 30-second bound including cleanup.
Its exit code means the audit completed; **managedOnly: false** is the failed
acceptance result. No scalar.44 semantic, adapter, resource or performance matrix
ran after that gate. Lython remains pinned to PythonRe **0.2.0**.

Finite literal Count simplification, original cold FullMatch/template interruption,
retryable preparation, eager construction Compile controls and capture metadata
admission are acknowledged producer improvements. Their recorded scopes do not
constitute downstream qualification. The sibling handoff requests a new immutable
pair after the remaining core mechanisms are removed.

Hashes, provenance and gate output are in [evidence.json](evidence.json).
Frozen packages, symbols, logs, the reused audit source and before/after handoff
copies remain in ignored `.git/agent-notes/regex-scalar-44-20261013/`.
An initial snapshot assumed a different nuspec XML namespace and stopped;
the corrected continuation verifies and preserves the same package bytes.
Earlier [scalar.5](../2026-10-12-regex-scalar-5/README.md) results remain historical.
Do not repeat the audit against unchanged scalar.44 artifacts.
