# Lokad.Lython 0.11.0 — active delivery plan

Updated 2026-11-02 (Paris). Git-ignored; never force-add. Publication/tagging
remain postponed. Whole-script performance remains an ongoing objective.
Completed investigations and original adverse conditions live in benchmarks/results/;
private plan snapshots preserve the earlier working narrative, outside docs/.

## Actual delivery status

| Deliverable | State / evidence and limits |
| --- | --- |
| Shared compiler facts61237f58 | COMMITTED/FULLY QUALIFIED. Iterative shared queries remove exponential alias expansion. 2026-11-02-shared-compiler-facts. |
| Native helper returnsf9ae2d65 | COMMITTED/FULLY QUALIFIED. Established CSV/JSON allocation−19.92/−19.31%; elapsed diagnostic. Later expression work removes interpreter storage, with historical CSV/JSON CPU debt preserved. |
| UTF-8 formatting6e1ca104/df548ddf | COMMITTED/FULLY QUALIFIED. Allocation−8.67%; controlled B102 confirmation about5–8% lower elapsed/CPU. Original adverse15.125ms unexplained/preserved; no current correction indicated. |
| Bulk CSV, casing, output storage/formatting | COMMITTED/FULLY QUALIFIED throughc3fa26cc/aa6cf3cb. Reconsidered CSV082179c4 allocation−13.7..15.1%, CPU−7.2..11.1%. B110 parse-time fusion and B108 mutable dispatcher measured worse and are absent. |
| B123 plain JSON090574d1 + B124 native contexts90785737 | COMMITTED/FULLY QUALIFIED41519435155. Report/JSON elapsed about9–11%/6–8% lower; scan cancellation corrected. Context omission alloc−10..13% for CSV/JSON/text,−36.6% search; elapsed/CPU mixed, no general CPU gain. |
| B127 shared iteration7da7335a | COMMITTED/pushed/focused-qualified252 internal/1084 public per actual.NET11/10. CheckedEnumerator/CheckedRange/range.IterateChecked/PeekIterator removed;58 net production lines gone. Async precedence and mixed-cleanup liveness fixed. Text modestly faster; original2–3% range regression resolved by B131 below. |
| B130 local value forwarding060a8359 | COMMITTED/pushed/focused-qualified192 internal/194 public per actual.NET11/10.16 compiler lines, runtime guard/evaluator unchanged; original instructions preserve dynamic bindings. Complete named CSV/text alloc−30.47/−34.71%, warm elapsed about5–8%/8–10%, fresh about5% each against controls. Canonical text variable; no universal gain. |
| B131 structured fallthrough3bbeab0f | COMMITTED/pushed/focused-qualified670 internal/1701 public per actual.NET11/10. Redundant for/while/if/comprehension body blocks/jumps removed; no runtime change. Range about8–9% faster against controls, about7–8% ahead of90785737, closing B127 range debt. CSV/text mixed. |
| CPython checkpointB125 | COMPLETE at90785737, not today's HEAD. Fresh CSV/JSON/text/report2.00x/1.68x/3.76x/3.21x; warm2.05x/1.74x/4.08x/3.43x. Search inconclusive; original clocks/drift preserved. Config/copy historical082179c4. Compile CSV/report1.54x/1.52x, startup unmeasured. docs/BENCHMARKS.md specifies boundaries. |
| Regex producer dev.10 | Development qualification COMPLETE/cached:84 internal/424 public per target, six real compile/match/cursor cancellation probes. Ordinary nonpackable adapter pin0.4.0 unchanged. Core runtime dependency count0. |

Current full delivery **41535109342/exact060a8359 IN PROGRESS**: Windows/Ubuntu
ordinary Release.NET11/10 and installed consumers have progressed to original
Stress; latest saved status3 has both stress jobs running. Includes B127+B130,
NOT B131. Check minutes apart and cache terminal once. Fully qualified proof until
then is cached41519435155/exact90785737. Never requery terminal runs, including
41504778956 and original failed41501504578. Do not repeat unchanged qualification.

All B129/B130/B131 VM jobs exit0/inactive; workers joined, owned fixtures removed,
quiet gates/identities/full goldens pass, lease free. No active prototype or VM job.
B129 demonstrates ordinary named-helper overhead on identical core; it is not an
external baseline. Real managed WriteAllBytes uses zero preallocation locally and
on VM: no fixture correction follows from the rejected I/O asymmetry hypothesis.

## Priority1 — normal-engine operand/control redesign (B132)

Current CPU source gateB126 and B128 census show material normal dispatch/operand
work; final argument arrays already are final locals, tiny cleared pools already
exist. Blind buffer merges/arenas repeat B61/B69 failures. B131 removes one real
transition; continue with a coherent normal-engine simplification rather than
another builtin special case.

Reassess B66 static operand layout/linear execution. Its original90s timeout left
no usable performance result or normal-cleanup proof: uncertainty, not a failed
design. Preserve the entire original trial. Reuse its validated shape lessons,
not its stale runtime patch wholesale; current native helpers, shared cursors,
async semantics and all subsequent correctness fixes must survive.

Selected design: compiler-checked maximum operand capacity and protected-entry
depths, then one linear instruction stream with absolute control ranges. Delete
runtime operand resizing/replacement, per-frame block-entry depth arrays and the
nested block/instruction driver together. Compiler builder blocks remain temporary;
retained duplicate block graphs disappear. No second evaluator, mutable activation
pool, runtime mode, new allowance or fallback. Name/map/capture metadata and native
expression links still reference the actual instructions. Unknown stack shapes
are compiler defects, never silent alternate execution.

Contract tradeoff: fund the conservative whole-frame maximum before construction,
including generators before first advancement. This may reject earlier, including
unused branches, but never introduces a cap/hidden allowance. Borrow the same
owner; retained values/cursors/pending host operations remain funded until their
actual awaited completion and cleanup. Preserve separate large native-call packet
admission even when operand capacity already exists. No retry after refusal.

Acceptance: ordinary/default/keyword calls, closure cells/introspection, class/type
protocols, evaluation/error/traceback order, short-circuit flow, match, loop control,
try/with/finally, imports, generators/yield/send/delegation, actual suspension and
mixed disposal. Prove all supported operand shapes including hidden descriptor
arguments; effective current-token checks, managed nesting and terminal first-origin
refusal remain. Adapt architecture-only assertions, preserve independent semantic,
side-effect, alias/funding and cleanup coverage. No dormant old engine if retained.

Completion: focused shape/control/cancellation/ownership tests, then one bounded
quiet-VM comparison of complete CSV/text/JSON/report with unchanged controls,
compile-only and fresh execution separated. Size jobs for usable observations,
save each completed job, retain adverse data; a justified bounded correction is
allowed. Compare allocation separately. Decide remaining elapsed/compilation debt
explicitly before retention. Batch full platform/stress/installed consumers at
this coherent boundary, including B131; do not rerun exact060 while it qualifies.

## Subsequent redesign roadmap

| Area | Work/storage to eliminate; affected scripts | Acceptance / bounded decision |
| --- | --- | --- |
| Complete activation/operand transport | After B132, reassess remaining ordinary call/context construction using current source and complete CSV/text helpers. B130 closes adjacent named intermediates; nonadjacent/reused locals remain. Do not grow a native statement VM or resurrect slower arenas without new evidence. | Defaults/keywords, identity/locals/cells, rebinding, exceptions, genuine suspension, entry safety, first refusal and funded pending users. Select one displaced implementation before coding; controlled complete-script before/after. |
| Per-record collections | Existing native CSV bulk construction delivered. Consolidate actual remaining cursor/copy/growth/admission where complete CSV/JSON/text show material work; B110 fusion remains removed. | Unequal/custom/duplicate fields, aliases/retained rows, callbacks, admitted capacity/overlap. One clear construction invariant and controls, explicit regression decision. |
| Scalars | Exact machine-integer promotion only if new evidence makes accumulation material; BigInteger boxing alone does not prove dominance. | Overflow/hash/equality/identity/public projection, admission/cancellation. Eliminate actual dispatch/storage; no universal tagged migration by assumption. |
| Text/frontend | Remove remaining real conversions or repeated facts; shared compiler facts, UTF-8 storage/hashing/casing/formatting/plain JSON already delivered. | Unicode/error/scoping/diagnostic semantics, storage overlap; compile-only and fresh boundaries. Current60–90us compilation is not evidence for blind phase merging. |

Refresh representative external CPython results at the next meaningful retained
redesign checkpoint, not after every trial. Same complete source/input/goldens;
state measured revision, fresh compilation versus warm, I/O/staging/checks and
prior/final cleanup costs. Search waiting/control instability remains unresolved.
Performance objective stays open beyond an isolated experiment or delivery run.

## Independent regex track

Lython-owned Python UTF-8/byte-range/scalar/original-subject contract, one provider
across compilation/runtime/imports, captures/effective flags/replacements/errors,
current tokens and explicit ownership are delivered. Continue concrete core work
independently; adapter bridges/test providers/pinned development producers can
support it, with no backend plumbing in core. Distinguish contract tests, semantic
validation and actual backend performance; no unchanged qualification replay.

Only affected final RELEASED adapter qualification is deferred: native UTF-8
capture-name enumeration, one-shot ranges with working-funding receipt, actual
reversed-window support, corresponding performance/managed-safety checks. Missing
publication does not block implementation/testing/performance investigation.

## Measurement policy and lasting constraints

Timeouts, unstable unchanged controls and inconclusive results establish
uncertainty. Bounded evidence-led confirmation/refinement/measurement correction
is allowed. Preserve original failed conditions and every adverse result.
Percentages inform decisions, not automatic vetoes. Separate architectural
simplification, allocation reduction and demonstrated CPU improvement. Record each
new redesign's eliminated implementation, workloads, semantics and completion/debt
decision; preserve slower B61/B69/B108 lessons without permanent experimental bans.

Fully managed/memory-safe; no unsafe/Unsafe/pointers/PInvoke/native probes.
Host mediation, managed stack admission and host-token cooperative cancellation
remain. No fuel/quotas/watchdogs, hidden caps/default allowances, heap tracing,
weak ledgers/forcedGC or new memory infrastructure. Coarse borrowed admission
preflights capacity/metadata/overlap, funds retained aliases/results/caches/pending
users through actual awaited cleanup. First refused admission terminal with original
structured origin; no retry/fallback. Explicit guest MemoryError stays catchable.
Bounded streaming uses host-driven batches, never cumulative allocation accounting.

Use focused Release checks during iteration; batch full Windows/Ubuntu.NET11/10,
original Stress and installed package consumers at meaningful delivery boundaries.
Current concise docs only; history/evidence outside docs/. Publication postponed.
