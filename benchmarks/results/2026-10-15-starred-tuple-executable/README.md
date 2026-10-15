# Ordinary starred tuples share executable display lowering

Ordinary `(*left, value, *right)` now uses the existing generator display
operations, including immutable tuple completion. The compiler change is one
replacement: remove this expression fallback and its local-mirroring requirement.
No opcode, runtime handler, ownership layer, iterator or configuration is added.
Evaluation and starred iteration remain interleaved before final conversion.

The working list and final tuple are both funded during conversion. Those
estimates can remain under the finite execution/result owner, so admission may
refuse earlier than the previous lowered temporary-list path. Existing per-item
fuel checkpoints apply. These tradeoffs are documented in SPEC.md and MEMORY.md;
there is no collection-based recovery or guest-object tracing.

Qualification uncovered a separate compatibility bug: empty literals/expansions
created distinct tuples instead of reusing the existing immutable empty tuple
returned by `tuple()`. Prerequisite5cf03da9 fixes executable, prepared and required
lowered/syntax construction paths. Empty iterable effects still execute before
canonicalization. No new cache/identity ledger; both timing roles contain this fix.

| Warm helper/tuple-merge job | Earlier Lython | Candidate Lython | CPython3.13.16 |
| --- | ---: | ---: | ---: |
| 2,048 calls; six-element immutable results | 2.659 ms | 2.629 ms | 0.3477 ms |

One [diagnostic](warm.json): observed1.1% lower median is within candidate5.6%
IQR/median (baseline1.1%, CPython0.6%). **No elapsed improvement established**,
qualified multiplier, confidence interval or complete file-job claim. Keep the
shared executable path and explicit admission; do not attribute unchanged timing
to a particular cost. No retry, retuning or new tuple-storage representation.
The owned command took7.630seconds including cleanup.

Measured2026-10-15 on AzureStandard_F4as_v7, Ubuntu24.04.4 LTS, four AMD
EPYC9V45vCPUs/16GiB RAM; SDK10.0.401/runtime10.0.12, CPython3.13.16 with
ordinary GC/GIL and `-I -S`, explicit UTF-8 protocol. Normal Lython public policies,
cooperative allowance, GC/tiering and managed stack protection remain; CPython
has no equivalent containment.

Baseline5cf03da9fe224f7452ce35f597c975a1d7a9f36e contains the shared helper fixture
57a99472 and empty-tuple fix. The candidate differs only in the frozen compiler
source in [the declaration](comparison-declaration.json). The [case](catalog-micro.json)
and all timing/Python sources are identical. Both roles reuse compiled code and
create fresh state; the timer includes setup, execution/projection and previous
result disposal, excluding compilation, process startup, transport and full hashing.
Real host suspension is correctness-tested separately, not an injected-delay ratio.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
verify independent output witnesses at all three core sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies the one frozen compiler
difference, identical tooling, seven exited workers, inactive services and an
available VM lease. No file fixtures are used in this case.

[Local checks](local-validation.json):2,316ordinary white-box,10focusedwhite,
352semantic and284governed/representative/file-worker checks pass (overlap), each
under30seconds including cleanup. Shared list/tuple controls cover effects,
aliases, closures/locals, yield/send, real suspension/cancel, growth refusal and
retained-result funding. Denial at final tuple conversion preserves the previous
target/span and releases temporary reservations. Seventeen
[independent CPython3.13.2 goldens](cpython-goldens.json) include empty literal,
expanded and class-body identity. The original identity failure is preserved
privately; no budget/timeout relaxed. Package/platform qualification is recorded
at delivery. Set/dictionary displays and comprehensions remain open.
