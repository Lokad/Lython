# Utf8Regex dev.117 package boundary

Tested the frozen, unpublished 0.5.0-dev.117 package set from
`d657d7e61696643ceeaa6cd1a53aebf7348457d5`. Direct Windows probes
verified the Core and PythonRe package and assembly hashes on
actual .NET 10.0.8 and .NET 11.0.0.

- A named Unicode capture uses the expected UTF-8 name. Bounded
  full match sees lookbehind before the start and uses the original
  byte offsets. The cursor reports the outer group as last closed.
- Both the direct match and cursor reject a reversed anchored
  window at `startOffsetInBytes`. CPython 3.13.2 returns an empty
  whole match and participating capture at the independent start.
- The frozen Core estimates still use `Unsafe.SizeOf<T>`. PythonRe
  exposes no exact pre-optimization program equality key.

The producer's receipt reports 405 downstream Lython passes per
platform and framework on its pinned `98b9f87f7` replay. Its package
and assembly digests match the files examined here. These receipts
describe the producer replay. The direct probe establishes the
contract boundaries above. The optional repository adapter stays on
0.4.0. Final qualification requires a managed release, Python windows
and exact program-key metadata, with current-token interruption.

[Candidate manifest](producer-candidate.json) · [Checks and receipt](checks.tar.xz) ·
[dev.116 check](../2026-11-21-regex-dev116-boundary/README.md).
