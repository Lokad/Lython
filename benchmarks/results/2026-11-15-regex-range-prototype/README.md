# Development regex range adapter

R409, 2026-11-15. **Development prototype, not adopted normal adapter or final
released-package qualification.** Core remains decoupled and unchanged at fully
qualified1b82cd46/frozen0edc0c1c. Normal non-packable adapter remains on released0.4.
Both private adapters compile against the same exact unpublished0.5 candidate,
source dcfe880a accepted by its producer atc5e02a27; immutable asset hashes are
recorded. Separate sibling worktree edits are not consumed. No core release gate
or runtime dependency edge is added.

## Displacement and semantics

The native candidate advances directly into byte-range captures, removing detailed
captured-text projection, per-advance cancellation forwarding registration and
generic closure transport. It explicitly disposes compiled programs. The flag
bridge remains: parse Verbose and reported flag bits are independently meaningful;
EffectiveOptions alone cannot reconstruct that distinction. Four public controls
cover both mismatches and global inline flags, with cleanup returning the budget.

Keep the construction-token bridge and existing conservative MatchBytes/scope
funding. No new memory-management mechanism or work/time policy. New producer
allowance callbacks forbid regex reentry/nonthrowing-refund violations; direct
forwarding to reentrant host budgets remains unqualified. Native group-name
enumeration, cancellation during token-free cursor preparation and separate
one-match result ownership remain producer-specific limitations. UTF8 lookup is
not enumeration. The core's cooperating-provider interruption/unwind contract
remains separate from actual backend capabilities.

Each private source passes361public regex tests on actual.NET11 and10; unchanged
normal0.4 passes361each too. These cover Python semantics, windows/captures/flags,
cancellation and ownership/refusal. Original private test-image failure (357pass,
four missing workflow fixtures) is retained; adding those inputs corrects the
test image, not test assertions. No full core qualification is repeated.

## Complete affected scripts

| Script | Allocation / invocation before → native | Elapsed change | Wider process CPU change |
| --- | --- | --- | --- |
| Extract512named integer captures from Unicode text |423,411.5 →340,875.5B (−19.49%)|−24.76..−21.72%|−26.05..−20.73%|
| Rewrite512matches with named replacement groups |252,131.5 →169,595.5B (−32.74%)|−37.37..−33.55%|−36.62..−33.38%|

Source-pair development comparison, no CPython or cross-producer speedup claim.
Allocation is a separate local process-wide CLR counter diagnostic, five64-run
batches after full warmup; no forced collections. Full goldens/result cleanup,
unchanged compilation balance and final zero balance are asserted.

Quiet Azure F4as_v7/Ubuntu24.04.4, four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3
workstation GC/normal tiering. B799 finishes72.513s, two processes/source,5s
full-request warmup, five rotated0.5s batches. Exact core/producer/worker images,
source/input/output and execution boundary are identical except adapter. All
process-median pairings are retained; reference spreads2.28/4.12%, native1.73/1.90%.
Warm clocks include public synchronous Run, output validation and prior-result
cleanup; compilation/startup/reset/protocol/final cache teardown excluded. Wider
process CPU includes reset/protocol. No I/O, pending-operation latency, heap/RSS
or embedding-tail claim. All quiet/golden/budget/Closed/join/archive/cleanup gates
pass; terminal B799 cached, no workers or RAM fixtures remain.

Original B798 owner fails before Ready because custom fixture metadata omitted
required size/text. No timing observations exist. The failure/journal/catalog and
empty RAM fixture are archived under the idle lease before unmount; B799 corrects
only metadata. One exact historical38c0 binary with two aliases is retired after
fresh UID/path/hash/link/all-alias and committed archive/member proof. Current
images, evidence/toolchains and254/252MiBfloors remain. Both conditions are retained,
not silently rerun or relabeled. Producer publication holds only final adoption;
independent core/runtime development continues.

[All medians/controls](summary.json),[allocation](allocation-summary.json),
[immutable identities](prototype-identity.json),[focused checks](consumer-checks.json),
[source/assets/tests/procedure](sources-assets-and-checks.tar.xz),
[raw observations](observations.tgz),[original failure](original-failed-observations.tgz).
