# Immutable scalar.5 package assessment

Latest supplied core/PythonRe pair: **0.4.0-scalar.5**, Release/net10.0 producer
**f3366d535e005a9e9cce10af604f00f47b2a8d10**. Both package SHA256/SHA512 values,
symbol packages/PDBs, DLL hashes, nuspec producer and exact core dependency agree
with the frozen manifest. Loaded DLL informational versions and hashes match.
No sibling source was edited or rebuilt; no package was published or promoted.

The first mandatory downstream gate fails. Exact DLL metadata still references:

- Core: Unsafe.Add/As/AsRef/ReadUnaligned/SizeOf and Vector128/256.LoadUnsafe.
- PythonRe: Unsafe.Add/As/AsRef.

These violate Lython's lasting managed-only constraint. Promotion stops here;
no scalar.5 semantic/adapter matrix or performance campaign ran. The production
pin remains PythonRe **0.2.0**. The metadata tool's successful exit means the
audit completed; its explicit `managedOnly: false` is the failed acceptance gate.

Scalar.5's cursor workspace reuse, retained scratch and EOF/failure/disposal
contracts are acknowledged as supplied improvements. The producer's SPEC now
describes accepted finite depth and working-byte policies, resolving the earlier
documentation contradiction. These observations do not qualify the new package's
semantics, live memory or performance downstream. Later scalar.6/7-dev PCRE2 work
has a separate acceptance boundary and does not replace this core/PythonRe identity.

Scalar.4's 24 direct controls and scalar.2's 61-case adapter observations remain
[historical evidence](../2026-10-12-regex-scalar-4/README.md), not scalar.5 results.
The authorized Utf8Regex handoff was refreshed with exact artifact identities,
the failed gate and a request for a new immutable compliant pair.

Hashes, dependencies and metadata output are in [evidence.json](evidence.json).
The owned metadata audit takes 101 ms with a 30-second cap. Frozen packages,
symbols, provenance, logs and before/after handoff copies remain under ignored
`.git/agent-notes/regex-scalar-5-20261012/`.
