# Lokad.Lython 0.11.0 — active delivery plan

Updated 2026-11-03 (Paris). Git-ignored; never force-add. Publication/tagging
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
| B127 shared iteration7da7335a | COMMITTED/FULLY QUALIFIED41535109342; focused252 internal/1084 public per actual.NET11/10. CheckedEnumerator/CheckedRange/range.IterateChecked/PeekIterator removed;58 net production lines gone. Async precedence and mixed-cleanup liveness fixed. Text modestly faster; original2–3% range regression resolved by B131 below. |
| B130 local value forwarding060a8359 | COMMITTED/FULLY QUALIFIED41535109342; focused192 internal/194 public per actual.NET11/10.16 compiler lines, runtime guard/evaluator unchanged; original instructions preserve dynamic bindings. Complete named CSV/text alloc−30.47/−34.71%, warm elapsed about5–8%/8–10%, fresh about5% each against controls. Canonical text variable; no universal gain. |
| B131 structured fallthrough3bbeab0f | COMMITTED/pushed/focused-qualified670 internal/1701 public per actual.NET11/10. Redundant for/while/if/comprehension body blocks/jumps removed; no runtime change. Range about8–9% faster against controls, about7–8% ahead of90785737, closing B127 range debt. CSV/text mixed. |
| B132 compiled operand layoutfd7bc7e4 | COMMITTED/pushed/focused-qualified4031 internal/6855 public per actual.NET11/10. Runtime growth/depth arrays/nested driver/retained block graph removed. Range elapsed about4% below control, report allocation−1.51%; other scripts flat/mildly better with drift. Compile elapsed about2% higher, accepted debt. |
| CPython checkpointB125 | COMPLETE at90785737, not today's HEAD. Fresh CSV/JSON/text/report2.00x/1.68x/3.76x/3.21x; warm2.05x/1.74x/4.08x/3.43x. Search inconclusive; original clocks/drift preserved. Config/copy historical082179c4. Compile CSV/report1.54x/1.52x, startup unmeasured. docs/BENCHMARKS.md specifies boundaries. |
| Regex producer dev.10 | Development qualification COMPLETE/cached:84 internal/424 public per target, six real compile/match/cursor cancellation probes. Ordinary nonpackable adapter pin0.4.0 unchanged. Core runtime dependency count0. |

**Full delivery COMPLETE/GREEN41535109342/exact060a8359**: Windows/Ubuntu
ordinary Release.NET11/10, original Stress both and installed consumers PASS.
Cached terminal receipt in2026-11-02-native-local-forwarding/delivery-ci-status.json.
Covers B127+B130, NOT B131/B132. New full delivery41540517622/exactfd7bc7e4 is dispatched with original Stress; it batches B131+B132. Query minutes apart, cache terminal once; do not await it before independent work. NEVER query this run again, or earlier terminal
41519435155/41504778956/41501504578/etc. No unchanged qualification replay.

All B129/B130/B131 VM jobs exit0/inactive; workers joined, owned fixtures removed,
quiet gates/identities/full goldens pass, lease free. B132 timing/allocation jobs complete; no active runtime prototype. Next B133 refresh uses the committed runtime.
B129 demonstrates ordinary named-helper overhead on identical core; it is not an
external baseline. Real managed WriteAllBytes uses zero preallocation locally and
on VM: no fixture correction follows from the rejected I/O asymmetry hypothesis.

## Priority1 — delivery and current external baseline

1. Complete full41540517622 at exactfd7bc7e4; correct actual failures without
   weakening assertions. Windows/Ubuntu.NET11/10, original Stress and installed
   consumers. Cached exact060a8359 is already fully green, never requery it.
2. B133 COMPLETE (below): refreshed the same five complete CPython workloads plus CSV/report compile
   costs at the committed B132 checkpoint. Reuse the B125 driver, original goldens
   and quiet VM, fresh/warm separate, identical Lython controls, normal GC/tiering.
   One bounded12-job run sized about4min, per-job persistence, final users joined.
   Retain search uncertainty/adverse controls; correct measurement only on evidence.
   Update docs/BENCHMARKS.md with measured revision/machine/date and actual included
   I/O/staging/check/prior/final cleanup. No source speedup from independent tables.
3. Continue the whole-script objective. Current B132 timing establishes no broad
   material CPU gain. B134 source gate: inspect current complete call/frame and
   per-record construction after the new driver; one short current CPU pair only
   if existing samples no longer identify real work. Select a coherent deletion/
   replacement from demonstrated cost, rather than accumulating builtin cases.
   Treat compile debt explicitly; no automatic need to merge frontend phases.

B132 history/plan/initial failures and exact measurements are preserved in
benchmarks/results/2026-11-02-compiled-operand-layout/. Compiler shape analysis
replaces runtime workspace growth and handler-depth discovery; linear instructions
replace the nested driver and retained blocks. Earlier workspace admission,
including unused branches/unstarted generators, is documented. Existing4031/6855
per-target acceptance passes without relaxing semantic/refusal/cleanup assertions.
B66's original timeout remains inconclusive/preserved, not retroactively successful.

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


## Current active work

B133 COMPLETE on exactfd7bc7e4:12 jobs223.132s, all identities/goldens/quiet/cleanup PASS. Fresh CSV/JSON/text/search/report1.94x/1.64x/3.38x/5.28x/3.33x, warm1.97x/1.69x/3.85x/7.18x/3.26x. Text/report drift and earlier inconclusive search retained. CSV/report compile1.57x/1.58x; startup unmeasured. docs/BENCHMARKS.md refreshed; no causal source inference. B134 CPU pair is running. B132 is committed; full delivery41540517622
is active. No producer change or regex qualification replay required. Publication
remains postponed; completion of this comparison will not complete the objective.

### B134 bounded current CPU gate (after B133, same VM lease)

Existing90785737 profiles predate shared iteration and the linear engine. Report
still has a full-frame formatting helper; CSV retains per-record dictionary/text
construction after delivered bulk work. Inspect exactfd7bc7e4 on these two complete
scripts with the existing perf199Hz/frame-pointer collector,1024 jobs each,
ordinary GC/tiering and perf-map diagnostic override only. One pair plus offline
leaf/inclusive/origin analysis; keep unknown and partial chains visible. Full
before/after goldens, identity/quiet gates, normal worker/profiler exit and fixture
cleanup. This is CPU/source attribution, never elapsed or exact removable share.
Select a coherent activation/transport or construction replacement from actual
current cost; if unresolved, record the uncertainty rather than invent dominance.
No additional samples or local tweaks without a new evidence-based question.

## B135 next coherent construction change — neutral CSV row ownership

B134 COMPLETE27.950s/exit0, two actual guest captures/full goldens/quiet/joined
cleanup PASS. CSV DictReader.NextAsync inclusive39.9%, parser Feed18.6%; overlapping
useful work, not removable fractions. List storage construction2.4% leaf. Report
unknown22.8% prevents precise decomposition. No dominant context/boxing constructor
established. Evidence:2026-11-02-linear-engine-cpu. No more capture is selected.

Replace parser's reusable governed List<object> staging with the final governed
PyList, created lazily for the first field and detached after successful queue
publication. Remove AddRowCell's staging growth, row capacity/refund/1024-trim policy
and finish-time List-to-PyList copy. Keep ready-queue capacity admission and all
native parser semantics; never move headers/dictionary callbacks into parsing.
This is distinct from rejected B110 fusion. No new buffer/owner/pool/option.

Ownership: private partial rows use existing unpublished-storage discard on reset/
source cleanup. Completed/queued/escaping rows retain their original owner and
aliases; never clear/refund published backing. Existing PyList capacity preflight
funds growth and replacement before allocation; first refusal stays terminal.
Capacity may be admitted earlier on the first field; identical tiny thresholds
are not required. Fields/strings remain batch-owned, no guest heap tracing.

Acceptance: csv.reader/DictReader, empty/unequal/wide/multiline rows, quoting/escapes/
Unicode/numeric modes, retained fieldnames/rows, mutable/custom headers, queue rows
surviving EOF/failure, resumed sync/async input, malformed-input reset, cancellation,
first refusal/side effects, pending cleanup and shared-budget reuse. Focus existing
CSV/streaming/ownership checks; add regressions only for actual gaps.

One bounded complete CSV warm/fresh source-pair comparison with identical controls,
JSON/text controls and separate allocation. Consider initial row/queue admission
tradeoffs explicitly; retain all adverse results, decide simplification/performance
without a rigid percentage veto. Do not claim or complete the whole-script objective
from this one change. Full fd7bc7e4 delivery runs independently; next qualification
batches any retained later source at a meaningful boundary.
