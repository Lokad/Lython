# Bounded cancellation in native whitespace scans

Source bce54732 plus the archived D55 patch; candidate/control7895ac6d,
reference705d2e51 (same D54 production, frozen before commit metadata).
Replace per-scalar shared StructuralTraversal.WorkTick updates in forward,
reverse and fused-cased whitespace collectors with checks before64-byte chunks.
Complete scalar decoding crosses a boundary by at most3bytes, so at most64scalars
execute between checks. Structural/cycle callers retain their existing machinery.
No new counter, policy, layout, owner, cache or allowance. Existing output/growth/
end checks, conservative capacity and independently owned fragments remain.

This removes repeated shared-state work. The source bound is distinct from a
measured interruption-latency claim. Context-free helpers have no host token.
Release build33.80s/zero warnings/errors;150internal/237public focused regressions
pass on each actual .NET11/10: whitespace inventory/bounds, casing/expansions/final
sigma, native returns, cancellation, terminal refusal, retained storage and reuse.
No test scale, timeout or containment assertion changed. Retained commit0752be3c
is included in full local4131internal/16004public per actual target onb69a0a49.
Its platform delivery is batched with D56 as
[CI42065949621](https://github.com/Lokad/Lython/actions/runs/42065949621), GREEN/all8
onb69a0a49. [Terminal receipt](../2026-11-05-compiler-bounded-retirement/delivery-ci-status.json)
is cached; no unchanged replay. D54's earlier receipt remains separate.

## Complete-script comparison and uncertainty

Workerb94, CPython3.13.16, quiet dedicated Azure VM, ordinary GC/tiering. B284
86.34s/three jobs within120s:5s warmup per engine/job,7rotated~150ms samples,
candidate/reference/independent identical-current/CPython process roles.

| Warm complete job | Candidate/reference | Identical current/reference | Limits |
| --- | ---: | ---: | --- |
| Standard fused text |−1.48%|−0.16%|Current spread1.35%; small observation|
| Unfused text |−2.40%|−1.99%|Current spread.42%; control IQR2.44%|
| Mixed-Unicode fused text |+0.03%|+5.87%|Current spread5.83%; inconclusive/adverse retained|

The original Unicode spread justifies one unchanged-binary confirmation. B285
51.94s within90s, one identical case:8s warmup per role,11rotated~250ms samples,
all other boundaries/gates unchanged. Candidate/reference+0.20%, identical-current/
reference+2.18%, current spread1.98%, IQR1.02/1.84/1.29% for candidate/control/
reference. **No established Unicode improvement; possible small regression stays
open.** Confirmation does not erase B284 or prove the source of its dispersion.

Retain the bounded-check simplification. Standard/unfused observations are useful
but do not establish general or causal CPU gains. No allocation/RSS/startup/host
latency claim. No further isolated retiming scheduled; materially new evidence can
justify a later complete redesign. The broader performance objective remains open.

Both blocks pass quiet/source/fixture/full golden/library/Closed/final-zero/
joined-worker/removed-fixture gates. Included: invocation, actual mediated
/dev/shm filesystem I/O, complete output/file checks, prior-result cleanup.
Excluded: compilation, process startup, staging/reset/transport, final cache/
process/fixture teardown. These diagnostic ratios do not refresh today's full
representative CPython guide.

## Setup failure retained

The first B284 extraction failed with ENOSPC, before any worker or observation.
Maintenance removed uploads only after matching local size/SHA256 and coalesced
byte-identical worker files with hardlinks, preserving all source/core/raw paths.
The first maintenance attempt stopped on a protected non-owned worker link;
resumption excluded non-owned files. A receipt records341 additional links and
597,819,392→699,375,616free bytes; earlier successful changes had already restored
space from zero. No timing occurred during maintenance. The same payload was
re-extracted before setup/identity/quiet gates; failed conditions stay separate.

[Contract](source-contract.md), [initial observations](summary.json),
[confirmation](confirmation/summary.json), [source patch](source.patch),
[maintenance receipt](vm-cleanup.json). Raw archives include original observations,
sources, identities and orchestration. No new benchmark framework.
