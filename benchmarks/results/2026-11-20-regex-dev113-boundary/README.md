# Utf8Regex dev.113 package boundary

The immutable local 0.5.0-dev.113 candidate comes from producer revision
`b7b73e59f`. The direct probe verified the Core and PythonRe package and
DLL hashes on Windows .NET 10 and .NET 11. Both copied the UTF-8 capture
name `é` and matched an ordinary anchored window. Both threw
`ArgumentOutOfRangeException` for original-subject byte start 3 and end 1.
CPython 3.13 returns an empty anchored match and an empty participating
capture at scalar position 2 for this input.

The exact producer source also uses `Unsafe.SizeOf<T>` in Core's
construction estimates. A released package must support the original
reversed anchored window and meet the managed-code policy before final
adapter qualification. The optional Lython adapter uses the 0.4.0 pin.

The producer's exact-feed Windows replay passed 405 Lython checks per
framework at host revision `98b9f87f7` with its development adapter.
This probe checks the binary boundary. Current-source Lython adapter
qualification and Linux downstream replay remain separate.

[Candidate manifest](producer-candidate.json) · [Probe and provenance](checks.tar.xz) ·
[Earlier dev.111 boundary](../2026-11-20-regex-dev111-boundary/README.md).
