# Immutable regex dependency assessment

Lython froze Core/PythonRe `0.4.0-scalar.48` on 2026-10-14, producer
`cab9c04209c7f0d291623791e0d585af507d53af`, Release/net10.0. Package hashes,
symbols/PDBs, nuspec producer, dependency closure and loaded DLL identities
match the supplied manifest. [Exact evidence](evidence.json) records those bytes.
No sibling rebuild or publication occurred.

The mandatory managed-only gate fails: the core references Unsafe.Add/As/AsRef/
ReadUnaligned/SizeOf and Vector128/256.LoadUnsafe. PythonRe has no references
found by the reused member-reference gate. Producer acceptance is separate from
Lython's lasting constraint; disabling routes does not qualify these APIs.

Qualification stops at this gate. No adapter/resource/performance matrix ran;
Lython remains pinned to PythonRe 0.2.0. Later sibling source improvements are
outside these immutable bytes. Do not repeat the audit on unchanged packages.
The existing sibling handoff now records this decision and Lython's explicit
execution/batch/cache/result ownership contract. No new tracing framework is
requested. This is a dependency gate, not a performance measurement.
