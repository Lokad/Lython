# Overlapping histogram bindings

R375, 2026-11-15. Independent isolated CPython probes expose two compiler-fusion
bugs in the qualified cf754/957 runtime. With a pre-existing key, reusing the loop
word as the dictionary wrongly updates the old dictionary twice instead of
raising AttributeError before any update. Reusing the word as the running total
wrongly completes with total2 instead of raising TypeError after one update.
The original and corrected outputs survive. A literal first variant is statically
rejected; the parameterized variant demonstrates the actual runtime divergence.

Exclude overlapping dictionary/word/total local bindings from whole-loop fusion.
Keep the ordinary executable loop: Python errors, binding order and side effects
are preserved, without removing supported syntax or introducing another backend.
No public API, ownership, limit, cancellation or stack-policy change; no speedup
claim. Preserve this correctness fix independently of the subsequent performance
prototype.

165 focused public and 34 admission/ownership checks pass per actual .NET11/10.
Both examples match isolated CPython under both APIs on both frameworks. The
source/scope correction is separate from the exact cf754 shared-slot full
qualification; later delivery qualification is batched at a coherent boundary.
[Original divergence](aliased-bindings-original.jsonl),
[focused checks](alias-fix-checks.json), [sources/probes](source-probes-and-checks.tar.xz).
