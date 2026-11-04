# Shared completed JSON container loop

2026-11-04. Qualified baseline **5c827e68 / 21cddae7**; prototype on documentation
commit4cb02edc, frozen **9588b3c8**. Exact source snapshot/hash identifies the measured
code. Quiet Azure F4as_v7/Ubuntu24.04.4, actual.NET11RC3, normalGC/tiering.

The same recursive container grammar now executes its completed loop outside an
async state machine. An actually incomplete child/hook enters one await helper,
then resumes that loop. Ordinary per-container async builder/awaiter initialization
is displaced. Existing string/number grammar, error handling, dictionary/list
storage, immutable key snapshots and execution ownership remain. No explicit parent
array/workspace, second parser, new owner, retry or policy. Source adds44 lines for
explicit completion/resumption; source count alone is not the runtime-work measure.
This differs from the added workspaces withdrawn in B137/B176.

## Correctness and ownership

Nonincremental Release build46.13s, zero warnings/errors. Existing **181 internal /
815 public per actual.NET11 and.NET10** pass. Both APIs retain grammar/raw-prefix/
Unicode/error/order/duplicates, numeric/object/pairs/custom hooks, real suspension/
reentry,17nested pending containers, retained aliases, shared concurrent runs,
cancellation/terminal refusal and awaited cleanup/budget reuse. No tests or contracts
changed. [Validation](validation.json), [source](source-snapshot.tgz), [patch](source.patch).

## Controlled whole-script comparison

Eight complete jobs in **222seconds**, below300s owned limit. Three isolated images,
two old-source roles, shared CoreLib/auxiliaries/hash witnesses. Six-second guest
warmup/image, alternating preparation, two-second settle, nine rotated200ms batches.
All loaded identities, CPython complete goldens, baseline/finalzero balances,
fixture removal, quiet gates and joined cleanup pass. Source/runtime qualification
remain separate. No failed capture or timing-policy correction.

| Complete script | API | Candidate | Elapsed vs old / old control | Old-role spread / candidate IQR |
| --- | --- | ---: | ---: | ---: |
| json-summary-helpers | Run | 4.9549 ms | -2.15% / -2.84% | 0.70% / 0.67% |
| json-summary-helpers | RunAsync | 5.5912 ms | -2.86% / -3.80% | 0.98% / 0.66% |
| report-render | Run | 4.7023 ms | -2.00% / -2.70% | 0.72% / 0.47% |
| report-render | RunAsync | 4.9068 ms | -4.67% / -1.35% | 3.37% / 3.09% |
| config-edit | Run | 0.1964 ms | -0.45% / -0.16% | 0.29% / 0.40% |
| config-edit | RunAsync | 0.2086 ms | -1.16% / -1.50% | 0.35% / 0.56% |
| text-summary-helpers | Run | 3.0674 ms | -2.15% / +3.65% | 5.60% / 1.35% |
| text-summary-helpers | RunAsync | 3.5958 ms | +3.06% / -3.20% | 6.47% / 4.55% |

Process allocation is unchanged apart from config background noise <=0.02%; all
admitted execution/root peaks are exactly unchanged. These are elapsed results,
not an attributed CPU/RSS claim. ReportAsync remains variable (3.09%candidateIQR,
3.37%old spread). Unchanged text controls move5.60/6.47%; their straddling directions
are inconclusive, not measured text gains or established regression. No unchanged
retiming selected merely for prettier controls; justified new evidence can reopen it.

Retain the coherent completed-loop change: JSON's2.1–3.8% and sync report's2.0–2.7%
elapsed reductions exceed their old-role spreads. Config is near unchanged/slightly
shorter. Extra explicit continuation/source maintenance and actual pending-hook
performance remain limitations; pending semantics are qualified, pending-performance
speedup is not claimed. No general call/frame/tagged-value rewrite is justified by
this result. Whole-script performance objective remains open.

Invocation, actual mediated RAM-file I/O, full output/file checks and result disposal
are included; reset/staging/startup/compilation/final cached teardown are excluded.
RunAsync here uses completed I/O, with real suspension separately tested. No external
CPython performance refresh for this individual change. [All metrics](assessment.json),
[exact identities](candidate-identity.json), [original plan](pre-measurement-plan.md).
The reused driver's free-text purpose/boundary label is stale; selected cases/API
bindings/identities are correct. Raw observations stay unchanged. [Label correction](driver-metadata-note.md).

[Raw observations](observations.tgz), SHA256
`329785e42d7b703d33e996018d2d814f0beb319ca6f09e0ab0129ae9a9ad8ca8`.
Full Windows/Ubuntu actual11/10/original Stress/installed consumer qualification of
this new source is pending; prior41889236498 qualifies baseline5c827e68 only.
Publication remains postponed.
