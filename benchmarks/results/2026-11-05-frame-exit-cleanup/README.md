# One executable frame-exit cleanup invariant

Baselinef8ebdc7c/productionb69a0a49 plus the retained D59 patch. Coreec4b6378
built before commit metadata; source snapshot includes the final twelve regressions.

Four regressions demonstrate the original hole: instruction-entry cancellation
or refused admission, through sync/async execution inside a guest handler, leaves
that abandoned handler's exception installed after frame exit. Operation errors
already restored it. The first reproduction used an unnamed handler; final cases
also exercise named binding deletion through a live globals() namespace.

Consolidate existing AbandonFrame at the two actual driver exits when neither
yielded nor awaiting a child. Remove its five scattered return/routing calls and
duplicate retirement placement. Retire in the same reverse order, clear saved
handlers/delete their names, restore entry exception in finally even if retirement
or name deletion fails. Span is needed only for actual bindings; no dummy per-frame
span allocation. Entry-vs-operation routing and first refusal origin remain.

No new unwind framework, policy, owner, counter, scope, retry or unsafe code.
Yielded generators/pending calls keep their state. Storage-only generator release
still preserves the active caller. Cursor queue/awaited cleanup and active alias
funding remain; stack-release failure behavior is deliberately outside this patch.
Store/worker lifetime fences remain. No speedup or allocation reduction claim.

## Validation

Initial reproduction:4failed/8passed on actual.NET11, preserved before-log. Fix
passes all12 on actual.NET11/10. Broader focus226internal/1274public per target
passes, including delayed operations, exceptions/finally/with, closures/generators,
first refusal/cancellation, temporary/worker lifetimes and shared budgets.
Full4143internal/16004public per actual target pass. Initial clean12.24s build,
final test-fixture build6.14s/zero warnings/errors. No timeout/scale/containment or
checkpoint-count/location relaxation.

The earlier D57 layout is fully withdrawn and D56 source is separately qualified
via42065949621/all8; that receipt does not qualify D59. Batch Windows/Ubuntu,
original stress and installed consumers at this retained delivery:
[CI42074585232](https://github.com/Lokad/Lython/actions/runs/42074585232) is GREEN/all8
on retained13d69be6. [Terminal receipt](delivery-ci-status.json) cached; never replay.
A representative
CPython refresh covers the coherent retained group; no extra isolated D59 timing.
Publication remains postponed; broader whole-script performance work stays open.

[Contract](source-contract.md), [before regression](regression-before.log),
[retained source](retained-source.tgz), [source patch](source.patch).
