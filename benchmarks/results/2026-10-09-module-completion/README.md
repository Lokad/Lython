# Module completion experiment, 2026-10-09

The synchronous module completion candidate is **not accepted**. Two declared
repetitions showed modest improvements for empty and tiny scripts, but the
integer loop took slightly longer in both repetitions. The candidate stays
isolated; no additional collection was made to seek a favorable result.

Each short collection took **6.49–7.13 seconds**, including worker startup,
warmup, verification and shutdown. Each owned process group had a 30-second
hard collection and cleanup cap. No full comparison lane ran in this round.

## Experiment and boundary

Candidate `5ee4abf57720fc28263d32ed3e101c9155f2ca79` consumes the synchronous
module interpreter's returned value directly, instead of throwing and catching
`ReturnSignal` for ordinary completion. Baseline
`cf3eaed909a0d0fd7dad6eb71127fd48f04dc632` includes the independent writer
cleanup correction described below, as does the candidate. Only the direct
completion change differs between these two production trees.

The unchanged quick catalog supplies `control.empty.control`,
`control.tiny.control` and `loops.integer.large`. Each engine precompiles the
same ordinary Python source, then executes the complete job with fresh state
and validates the complete output on every invocation. Lython uses the normal
synchronous public API, including its dedicated execution stack, containment,
memory limits and result/output projection. CPython uses ordinary isolated
execution and capture, without an equivalent in-process governor. Entry cost
is included and no control time is subtracted from the loop.

The declared order was empty, tiny, loop, repeated once. There are seven
measurement batches and two verifications per engine per collection.
Worker GC, tiering and PGO settings remain normal. Preparation, builds and
correctness tests occur outside collection. This diagnostic is not a milestone
qualification and supplies no certified ratio or confidence interval.

| Job / repetition | Baseline µs | Candidate µs | CPython µs | Baseline / candidate IQR fraction |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 85.47 | 82.08 | 1.52 | 0.047 / 0.033 |
| Tiny / 1 | 83.68 | 77.06 | 2.19 | 0.038 / 0.040 |
| Integer loop / 1 | 2,365.05 | 2,383.29 | 638.73 | 0.008 / 0.010 |
| Empty / 2 | 84.72 | 82.20 | 1.53 | 0.042 / 0.019 |
| Tiny / 2 | 86.09 | 82.93 | 2.24 | 0.025 / 0.037 |
| Integer loop / 2 | 2,258.37 | 2,332.83 | 639.03 | 0.016 / 0.002 |

Empty script medians fell 3.0–4.0% and tiny script medians fell 3.7–7.9%,
with visible short-run variation. Loop medians rose 0.8–3.3%. These results
do not demonstrate a worthwhile general runtime improvement, nor attribute
the small loop difference to a particular allocator or JIT effect.

## Independent correctness correction

Preparing return-path regressions exposed a synchronous writer cleanup bug:
when publishing a still-open writer failed during module return, execution
returned the error immediately and could leave another writer unpublished.
The asynchronous normal completion path already attempted the remaining
cleanup, as required by the specification.

Commit `cf3eaed9` makes the return catch paths perform the existing best-effort
cleanup after publication failure, retaining the first failure and honoring
cancellation. The correction covers executable and lowered synchronous return
paths and the asynchronous return-signal fallback. Eight added public checks
cover implicit completion, `return None`, `return 7`, transient publication
failure and a permanently failing writer beside another pending writer.
The original runtime fails all four added synchronous cases; its four
asynchronous cases pass. Both corrected producer configurations passed 583
focused public checks locally, and each pinned Linux Release producer passed
the same 583 checks before timing. No performance claim is made for this fix.

The candidate's broader local Release public suite passed 7,741 tests and
failed the existing ordinary-reader/asynchronous retained-row CSV test:
129 bytes denied at a 3,145,723-byte peak under its unchanged 3 MiB budget,
after two completed reads. A similar earlier failure is retained in the local
plan. This recurrence reopened the lifetime investigation; focused passes
do not establish its cause or justify changing the budget.

## Reproduction and evidence

Run `--compare list --profile quick --out catalog.json` from the Release
comparison executable. Run its `--compare micro` command with that catalog,
the pinned executables, a baseline worker and a fresh receipt filename, selecting
the three case IDs above in the declared order. Hold the shared VM lease and
wrap every collection in an owned service with `RuntimeMaxSec=30`,
`TimeoutStopSec=0` and `KillMode=control-group`. Never overwrite receipts.

The dedicated machine and toolchains match the preceding
[string reclamation experiment](../2026-10-09-string-reclamation/README.md):
AMD EPYC 9V45, four-core Ubuntu 24.04.4; SDK 10.0.401/runtime 10.0.12;
optimized PGO/LTO CPython 3.13.16 with normal GC/GIL and experimental JIT
disabled. Source trees were clean before and after collection. All services
succeeded, recorded workers exited, the lease was free afterward and recorded
inputs rehashed unchanged.

An independent audit checked all six receipts and 253 responses, including
complete invocation counts, source/fixture/golden/actual output digests,
Release SDK/library identities, worker settings, clocks, measurement counts
and service completion evidence. Raw receipts, source declarations, preparation
logs and audit remain local under
`.git/agent-notes/module-completion-20261009/`.

| Evidence | SHA-256 |
| --- | --- |
| Quick catalog | `e6cf197948d903c59d4db5e940d9ca50433cbb0c6399ab999817f48775fa4fe1` |
| Baseline Lython library | `5acaa8420164c40c6e90ae43d6e0226a98a3e2e43897ab33d9f825eccb546072` |
| Candidate Lython library | `03d26d056a85913d0074c21e43ce054c448a02a0e56aca5ba1a37a97ea60c70a` |
| Baseline worker | `42594b7448a4b1a43ffd7d3ddd9842b3e6fbc9e8fb0b74d63755b4e418507690` |
| Candidate worker | `475f6fb79ddcff3431587805c7e66f274d82614ce77e3e583612f3d75866bdc9` |
| Empty / 1 receipt | `34e680e172bf3047f749a6add95f4b02b05f475154b603bb1309ba8566ee1df2` |
| Tiny / 1 receipt | `239647c2079db76689e0a25c1d581413da5b7187563248cd9a8736890835db26` |
| Loop / 1 receipt | `fed86a822880ff3ea23322591edd7096da48c2a99355ab20c090ba972502640d` |
| Empty / 2 receipt | `5a1955510d1be9f2854914519e019fd0aae88045a2b975584383256c6842f4df` |
| Tiny / 2 receipt | `56b080e0df65cc35b8ced3228e5261fa358fd1b73916c1ea419d6ec231cc8989` |
| Loop / 2 receipt | `e5c2a6c1b2ab8e26051dfb5e47557083f46d2080db2672f3fdb8e7bc49de29fa` |
