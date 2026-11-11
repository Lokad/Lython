# Current captured and ordinary text costs — 2026-11-11

Two short CPU captures on retained, fully qualified `5f9059fa` executable source,
using frozen R241 `f702` core/precommit `30b03` metadata. The documented final
comment correction changes no executable source. Fixed common quiet worker and
normal GC/tiering on the existing F4as_v7 VM; perf maps and a same-process native
loop listing belong to these actual captures. No old offsets are transferred.

| Profile | Guest CPU seconds | Guest leaf samples | Unknown |
| --- | ---: | ---: | ---: |
| Captured counter/text helper | 5.89 | 1,168 | 52 |
| Ordinary text helper | 5.74 | 1,140 | 64 |

Captured leaves include native counting 169, shared dispatch 138, word preparation
96 and native iteration 48. Binding, work construction and the two call-entry
methods contribute 80 named leaves. Ordinary code uses existing native-body
evaluation; its shared dispatch has 82 leaves. Inlining, useful work and remaining
unknowns prevent treating these as removable percentages. These are source-selection
profiles, without a timing pair, allocation attribution or CPython ratio.

The construction gate finds no duplicate backing array after R241. Blindly merging
transient binding metadata with execution state would move real presence/default
and admission boundaries without demonstrating an allocation reduction. No such
prototype was built. The maintained two-word lines also provide little room for
a raw-word design that adds a scratch object while retaining final-word identity.

R243 instead investigates executing the complete captured helper through the
existing bounded native-body proof/evaluator: actual incoming cells, a store and
its continuation. Its source-level target removes binding/work-state/shared-VM
execution while preserving owner writes and pre-effect guards. It requires
semantic, cancellation, refusal, lifetime and controlled whole-script evidence
before retention; no predicted speedup.

[Assessment](assessment.json), [raw profiles/code/maps](observations.tgz) and
[archive receipt](archive-receipt.json) preserve all loaded hashes, goldens,
unknowns and cleanup. Both quiet gates pass; every worker joins, RAM fixtures
are unmounted/removed, and the persistent archive is verified under its owned
lease and after retrieval. B582 is terminal/cached. No remote files were deleted.
