# Lowered operation selection — 2026-10-11

**Rejected; the prototype remains isolated.** The prototype stores the exact
operation kind in each actual lowered node.
Synchronous function dispatch selects that operation instead of repeatedly
classifying node types. The same original handlers, actual contexts and class
cells, before-child fuel checkpoints, depth transitions, logical funding,
definition pipelines and genuine async routes remain.

This is a dispatch change. The broader executable method-body compiler remains
pending: its existing instruction guards, additional interpreter admission and
synchronous function adapter need separate contract work before a factory switch.

## Short timings and controls

Fourteen paired microbenchmarks cover seven unchanged canonical cases twice.
Each complete owned group, including cleanup, takes 8.038–8.051
seconds under a 30-second hard cap. CPython uses the same inputs and complete
output checks. Negative changes mean less Lython time; spread is the larger
retained IQR/median of the two Lython producers. These are diagnostic old/new
observations, without a milestone-qualified Python multiplier.

| Case | Pass 1 time change | Pass 2 time change | Retained spread, passes 1 / 2 |
| --- | ---: | ---: | ---: |
| `calls.keyword.medium` | +0.23% | +1.72% | 9.01% / 1.43% |
| `calls.method.medium` | +3.20% | -3.37% | 1.57% / 3.90% |
| `calls.positional.medium` | -1.63% | -1.90% | 1.41% / 0.76% |
| `control.empty.control` | +0.03% | -1.65% | 4.60% / 5.11% |
| `lists.stable-sort.medium` | +3.43% | +9.34% | 22.15% / 7.00% |
| `loops.integer.large` | +1.42% | -3.54% | 0.91% / 0.95% |
| `strings.pipeline-ascii.medium` | +5.09% | -2.91% | 2.66% / 2.97% |

Method time rises 3.20% in the first pass. The second pass's 3.37% reduction is
below its 3.90% spread, so neither pass clears the unchanged rule. The complete
decision is retained in the evidence. The rule requires method gains beyond
max(2%, retained spread) in
both passes, no repeated material control timing regression and no repeated
complete-job managed allocation growth above 0.5%. No full lane or favorable
recollection ran; ten-minute lanes remain reserved for declared milestones.

## Separate allocation and native diagnostics

Separate fixed-count allocation and native groups take 4.028–4.040
seconds including cleanup. Allocation is process-wide managed allocation over
complete warm jobs: two 100-invocation passes after 16 warmups, ordinary GC/tiering
and no forced collections. Positive savings mean fewer bytes.

| Case | Saved bytes/job, passes 1 / 2 | Saved fraction, passes 1 / 2 |
| --- | ---: | ---: |
| `calls.keyword.medium` | -0.24 / 0.24 | -0.00% / 0.00% |
| `calls.method.medium` | 2,949.12 / 79.92 | 0.16% / 0.00% |
| `calls.positional.medium` | 0.00 / 26.56 | 0.00% / 0.01% |
| `control.empty.control` | 0.00 / 0.00 | 0.00% / 0.00% |
| `lists.stable-sort.medium` | -2,666.96 / -26.64 | -0.08% / -0.00% |
| `loops.integer.large` | 0.24 / 0.00 | 0.00% / 0.00% |
| `strings.pipeline-ascii.medium` | 0.00 / 0.00 | 0.00% / 0.00% |

All emitted native tiers, inline summaries and call targets are retained;
selected bodies appear in [the evidence](evidence.json). Native code size does
not establish CPU causality. Tier1 expression dispatch is 2,515 bytes on the
baseline and 2,359 on the candidate; statement dispatch is 1,247/1,077 bytes.
The candidate retains checked-cast call targets; smaller listings do not establish
a repeatable CPU gain. Added discriminator fields may increase compiled
node footprint; warm-job allocation checks do not qualify compilation allocation
or startup performance.

## Correctness and reproducibility

Eight existing fixed checkpoint fixtures now exercise shared and direct function
dispatch on original production; all ten boundary-suite tests pass. Unchanged
class-factory, augmented-target, method-body, class-cell/closure, decorator/generic,
generator and delayed-host lifetime tests provide broader behavior coverage.
This does not claim the remaining method-factory compiler inventory is complete.

Both clean producers pass 9,466 matching Windows Debug tests and 9,466 matching
Linux Release tests, with matching probe builds and seven complete canonical
CPython cases per producer before timing. Tests and harness are identical.
Twenty owned services are terminal, all 66 recorded PIDs are absent, source and
prepared inputs are rechecked, and the VM lease is free. Routine timing,
allocation and native groups each retain the 30-second cleanup bound.

Baseline `e74a805d12a72f4b8a110072314d733f90f90732`, production `f006f62cad79f63bc1b2564494ce742b6fe74000`.
Isolated candidate `d6ca83220d2d666f090bc86f1e3f26325bf4d55a`, production `7d82cb2640d5002c7ad5023a0b4581af503ff5dc`.
Tests `e3aacf797a8e49e3845a0c0552d88427fcc38d10`; harness `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.
The broader compiler, original pipeline/CSV causes, milestone qualification and
external Utf8Regex work remain pending.
