# Precedence consolidation, current-source reassessment

2026-11-05, quiet AzureF4as_v7 Ubuntu24.04.4/.NET11RC3, normal GC/tiering.
Baseline ab487dfa/frozen04f3e00d; documentation-only parent71ba8ae2. Candidate
`0d5af60aaf9834010883b7d3e03b7aeb7849668a76b55742b36750760e4373cc`.
[Exact identities/source](candidate-identity.json). Publication held.

RETAIN the complete precedence-climbing parser. Ordinary arithmetic/bitwise
operands no longer descend six grammar layers regardless of the next token.
Remove five forwarding methods and ParseNextLayer; replace layer+token dispatch
with one token-to-layer/operator decision. Thirty net production lines disappear.
Boolean/not/comparison/unary/power paths, AST order/spans, existing diagnostics,
independent per-layer chain bounds and cancellation remain. No new backend, cache,
argument storage, budget policy or public option/API.

The [current compiler profile](../2026-11-05-frontend-source-gate/README.md)
establishes current relevance. The [original B46 rejection](../2026-11-01-precedence-parser-rejected/README.md)
and all its raw conditions remain: CSV/text compileCPU−6–8% missed a rigid10%
threshold; fresh-config unchanged-control drift19.05% made that result inconclusive.
The superseding policy permits this bounded reassessment; old results are neither
promoted automatically nor replaced by the new collection.

Nonincremental Release62.95s, zero warnings/errors.656 white-box/3499 public focused
checks per actual11/10 pass, including parser/compiler cancellation and first refusal,
grammar/diagnostics, generators, type/annotation/function scopes, protocols and
shared-budget cleanup. Two added mixed-chain cases preserve independent arithmetic
depth bounds and allowance reuse through both execution APIs. The27-case arithmetic/
compiler-cancellation selection passes per target; it overlaps the main selection,
not27new cases. Its test-only rebuild embeds newer documentation-only revision
metadata, producing different DLL hashes with identical production source; both
identities are explicit in [validation](validation.json). Timings use frozen0d5 only.

## One controlled source pair

Five complete jobs136s, three isolated images/two unchanged baseline roles,
6s warmup/image,2s settle,9rotated200ms batches,240s owned service. All exact binary/
CoreLib/hash, independent CPython stdout/file golden, before/after quiet, baseline/
final-zero, owned-fixture-removal and process-join gates pass.

| Complete script | Phase | Candidate | Elapsed vs old / old control | Old spread |
| --- | --- | ---: | ---: | ---: |
| CSV summary/helpers | Compile |73.96us|-6.65% / -7.79%|1.23%|
| Text summary/helpers | Compile |55.08us|-7.72% / -8.38%|0.71%|
| JSON-to-Markdown report | Compile |51.60us|-11.39% / -8.54%|3.12%|
| Config read/edit/write | Fresh |0.283108ms|-1.33% / -1.09%|0.24%|
| Text control | Warm |3.103407ms|+3.79% / +0.62%|3.16%|

Compilation elapsed improvements are observed against both roles; report magnitude
is limited by its3.12% unchanged spread. Compilation allocation changes0.00–0.03%
are effectively flat. Fresh config is favorable; do not add this independent result
to earlier JSON percentages or declare earlier debt closed. Warm text remains
inconclusive with baseline variation and both adverse candidate signs preserved;
there is no warm benefit claim or identified CPU cause. Accept remaining warm
uncertainty for the measured compile improvement and simpler grammar route.

Compile-only covers public Compile/validity/disposal, excluding guest IO/execution.
Fresh adds the complete public run, mediated RAM IO, full checks and prior-result
cleanup; warm reuses compilation with that execution boundary. Staging/reset,
startup, response hashing and final cached-owner teardown are excluded. All users
are joined. Process allocation includes reset/runtime noise; untimed initial
admission peaks are not fresh overlap, heap/RSS or embedding latency. Current
elapsed is not a CPU measurement. No updated CPython ratios or whole-objective
completion follows. Full delivery41940441052 on exact f7fed42c is GREEN: Windows/Ubuntu ordinary
actual.NET11/10, installed Release consumers and original Stress on both actual
frameworks. [Terminal cached receipt](delivery-ci-status.json), observed00:49:29UTC.
This does not qualify the later cased-whitespace prototype; publication held.

[All metrics/IQR/allocations](assessment.json), [compact table](measurements.md),
[focused logs](focused-checks/), [original source contract](pre-code-plan.md).
Raw archive SHA256:
`7444b984a1e889797b20bfc202a0bf572edfb29aa7ee6f5f4f2f2382b339d3c3`.
Archives preserve driver/catalog/gates/samples; source/tests have snapshots/patches.
[Disk setup correction](setup-correction.md) and [premature test start](iteration-note.md)
were before samples/test execution respectively. No failed performance data or
semantic failure was removed, no policy threshold or timeout relaxed.
