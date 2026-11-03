# Lokad.Lython 0.11.0 — active delivery plan

Updated2026-11-03 (Paris). Git-ignored; never force-add. Publication/tagging remain
postponed. Whole-script performance is OPEN beyond any individual trial. Current
HEAD1e73f72f contains fully qualified runtimefd7bc7e4 plus documentation/evidence.
B136 source assessment and B137 controlled JSON-reader trial are COMPLETE; B137
WITHDRAWN. B138 property-name sharing is an ACTIVE PROTOTYPE. Its owned VM comparison is running; no performance claim. Retained reentrancy tests and
all adverse evidence committed1e73f72f; focused179 internal/822 public per target PASS
after restoring runtime. Prior FULL GREEN still applies to unchanged runtime.
Completed narratives/failed conditions live in benchmarks/results/; earlier plan
snapshots remain private, outside docs/. Do not duplicate unchanged qualification.

## Actual delivery status

| Milestone | Status / useful evidence and limits |
| --- | --- |
| Shared compiler facts61237f58 | COMMITTED/FULLY QUALIFIED. Iterative shared queries remove exponential alias expansion. 2026-11-02-shared-compiler-facts. |
| Native helper returnsf9ae2d65 | COMMITTED/FULLY QUALIFIED. Established CSV/JSON allocation−19.92/−19.31%; elapsed diagnostic. Later expression/context work also qualified; original CSV/JSON CPU debt remains preserved. |
| UTF-8 formatting6e1ca104/df548ddf | COMMITTED/FULLY QUALIFIED. Allocation−8.67%; controlled B102 confirmation about5–8% lower elapsed/CPU. Original adverse15.125ms unexplained/preserved; current evidence indicates no correction. |
| Bulk CSV/Unicode/output work | COMMITTED/FULLY QUALIFIED throughc3fa26cc/aa6cf3cb. Reconsidered bulk CSV082179c4 allocation−13.7..15.1%, CPU−7.2..11.1%; rigid historical thresholds no longer veto it. Slower B110 fusion/B108 dispatcher absent. |
| B123 JSON090574d1 + B124 contexts90785737 | COMMITTED/FULLY QUALIFIED41519435155. Plain native JSON/storage and scan-cancellation correction retained. Context omission alloc−10..13% CSV/JSON/text,−36.6% search; timing mixed, not a general CPU gain. |
| B127 shared iteration7da7335a | COMMITTED/FULLY QUALIFIED41535109342. CheckedEnumerator/CheckedRange/range.IterateChecked/PeekIterator removed;58 net production lines gone. Async precedence and mixed-cleanup liveness fixes preserved. Original range regression closed by B131. |
| B130 forwarding060a8359 | COMMITTED/FULLY QUALIFIED41535109342. Existing native plan forwards adjacent unobservable locals; runtime proof/evaluator and original instructions preserved. Named CSV/text allocation−30.47/−34.71%, warm elapsed about5–8%/8–10%, fresh about5% against controls. |
| B131 fallthrough3bbeab0f | COMMITTED/FULLY QUALIFIED41540517622. Redundant structured-entry blocks/jumps removed; range about8–9% faster against controls, ahead of pre-B127 source. CSV/text mixed. |
| B132 operand layoutfd7bc7e4 | COMMITTED/FULLY QUALIFIED41540517622. Runtime growth, frame depth arrays, nested block driver and retained block graph removed. Range about4% below control; report allocation−1.51%. Other scripts flat/mildly better with drift; compile elapsed about2% higher, accepted debt. |
| B133 CPython refresh / B134 CPU gate | COMPLETE atfd7bc7e4. Current guide/evidence committed845b63c8. Remaining gaps substantial; no causal speedup from independent checkpoint medians. |
| B135 direct CSV row | WITHDRAWN; source/tests restored. Canonical allocation unchanged, variable-width allocation+5.3436%/control0%, elapsed+1.77%/control+0.02%. Removes copying but repeats unknown-width growth. All adverse data retained in2026-11-03-direct-csv-row-withdrawn. |
| B136/B137 JSON construction | SOURCE GATE COMPLETE / PROTOTYPE WITHDRAWN. Report timing mixed, warm JSON-lines slightly worse, allocation immaterial. Manual reader/workspace removed; reentrancy regressions retained. Evidence2026-11-03-json-container-reader-withdrawn. |
| Optional producer dev.10 | Development qualification COMPLETE/cached:84 internal/424 public per target, six real compile/match/cursor cancellation probes. Ordinary nonpackable adapter pin0.4.0 unchanged; core dependencies0. |

**FULL GREEN41540517622/exactfd7bc7e4:** Windows/Ubuntu ordinary Release.NET11/10,
original Stress both and installed consumers PASS. Cached receipt in
2026-11-02-compiled-operand-layout/delivery-ci-status.json. Focused4031 internal/
6855 public per actual target; after B135 restoration101/425 per target PASS with
fresh nonincremental builds. Evidence-only commits change no qualified runtime.
NEVER query41540517622 or earlier terminal41535109342/41519435155/41504778956/
41501504578/etc again. No unchanged full qualification replay.

## Current measurements and uncertainty

B133 exactfd7bc7e4,2026-11-02, quiet F4as_v7 Ubuntu24.04.4/.NET11RC3 versus isolated
CPython3.13.16. Fresh CSV/JSON/text/search/report1.94x/1.64x/3.38x/5.28x/3.33x;
warm1.97x/1.69x/3.85x/7.18x/3.26x. Text/report controls drift4.5%/7.6% fresh.
CSV/report compile-only1.57x/1.58x; startup unmeasured. Config/copy remain historical
082179c4. Earlier90785737 search270%/218% fresh and138%/77% warm IQR/control results
remain inconclusive; current steadier search does not explain waiting. Guide:
docs/BENCHMARKS.md. It states compilation, staging/I/O/check/prior/final cleanup
boundaries. Twelve jobs223.132s, full identities/goldens/quiet/joined cleanup PASS.

B134 current report/CSV guest-thread CPU pair:1258/1094 samples. JSON construction
about30% inclusive of report CPU; CSV iteration/parsing about40%. These contain
useful work and overlap. Report unknown leaves22.8%, CSV5.3%; no exact removable
fraction or dominant separate context/boxing cause. No more capture is selected.
B135 tests passed99/425 per target, but controlled variable-row regression justifies
withdrawal. No prediction hint, second parser, pool or owner survives. A future CSV
proposal must eliminate both copying and repeated growth. This is not a permanent
ban. All VM services exit0/inactive, workers/fixtures joined/removed, lease free.

## Priority1 — B138 execution-owned immutable JSON property names

SOURCE GATE COMPLETE; implementation focus-qualified181 internal/822 public per actual.NET11/10. Controlled VM comparison and allocation diagnostic active; not yet retained or fully qualified. B137 shows
container async control alone has no established benefit. Native decoding currently
copies, allocates, hashes and reserves every repeated property name. Replace that
repeated storage with ONE lazy execution-owned native UTF-8 content table shared by
loads/load/decoder calls and reentrant hooks. No global/per-parse pool or parser.
Only owned decoded strings become stored keys; borrowed document bytes are used
only during lookup. Values and mutable containers remain independent. Python's
immutable identity contract permits sharing (local CPython datamodel reference).

Remove unconditional property-name copying on hits; escaped keys keep the existing
validated decoder and consolidate only fresh unpublished duplicates. Keep one
existing string grammar, original error offsets, hooks and depth policy. Table
metadata/entries/capacity/replacement overlap use conservative existing governor
preflights (flat slot estimate including growth headroom), not a new capacity ledger.
Strings and index remain funded by the execution/result owner; no token is cached.
No cache admission fallback: refusal remains terminal before new table mutation.
Unique keys incur extra index retention and can reject earlier; document this.

Acceptance: plain/escaped/Unicode/empty/duplicate property names, pairs/object hooks,
reentrancy and actual async I/O, borrowed-input mutation isolation, independent
mutable records, shared executions, retained result/cache funding and cleanup to
baseline. Preserve scanner cancellation and first refusal through cleanup. Focused
existing JSON/cancellation/admission/ownership checks plus targeted real gaps only.

Compare complete report/JSON-lines/config and a unique-key workload against the
unchanged runtime, with text control. Warm/fresh separated, allocations diagnostic
separate. Hypothesis is fewer repeated immutable objects/copies/root-resizes; no
predicted CPU percentage. Include control and adverse observations, allow bounded
refinement only for concrete evidence, and decide all regression debt at completion.
A retained change must delete displaced unconditional construction, pass meaningful
full platform/stress/installed checks, then resume substantial roadmap. No new
CPython refresh until a coherent retained redesign checkpoint; publication deferred.

## Remaining substantial roadmap

| Area | Work/storage to eliminate | Acceptance / decision boundary |
| --- | --- | --- |
| Complete calls/operands | B132 removes runtime layout discovery; investigate remaining activation/transport on actual normal helpers. B130 closes adjacent intermediates; reused/nonadjacent bindings remain. No native statement VM or blind B61/B69 arenas/B108 revival. | Defaults/keywords, locals/cells/identity, rebinding, error/evaluation order, imports, generators, actual suspension, admission/cancellation and funded users. Name one real displaced route before code; compare complete CSV/text/report with controls. |
| Per-record construction | Bulk CSV delivered; B135 shows the reusable staging tradeoff. Consolidate actual remaining construction/representation costs rather than speculative parser/dictionary fusion. | Unequal/custom/duplicate fields, aliases/retained rows, callbacks, queued results and admitted capacity/overlap. Current-source design plus complete-script controls, explicit regression decision. |
| Scalars | Contained exact machine integers/promote-on-overflow only if current accumulation evidence justifies it; no universal tagged-value commitment by assumption. | Equality/hash/identity/aliases, arbitrary precision, projection, memory and cancellation; eliminate measured work/storage. |
| Text/frontend | Remaining real conversions/repeated facts. Shared compiler facts/UTF-8 storage/hashing/casing/formatting/plain JSON are delivered. Compile-only about60–90us does not justify blind pass merging. | Unicode/context/error/scoping/diagnostics, storage overlap/aliases; warm and fresh separately. Preserve B132's explicit2% compile debt until a justified correction. |

Refresh external CPython only at another meaningful retained redesign checkpoint.
Do not retime unchanged rows after every local change. The performance objective
continues after this delivery; independent regex work must remain available.

## Independent regex track

Lython-owned Python UTF-8/byte-range/scalar/original-subject contract, one provider
across compilation/runtime/imports, captures/effective flags/replacements/errors,
current tokens and explicit ownership are delivered. Continue concrete development
independently using adapter bridges/test providers/pinned development producers.
No backend plumbing/dependencies in core; distinguish contract tests, semantics and
actual backend performance. Do not replay unchanged dev.10 qualification.

Only affected final RELEASED adapter checks defer: native UTF-8 capture-name
metadata, one-shot ranges with working-funding receipt, actual reversed-window
support and corresponding performance/managed-safety qualification. Missing producer
publication never blocks independent implementation/testing/performance work.

## Policy and lasting contracts

Timeouts, unstable controls and inconclusive observations mean uncertainty.
Bounded evidence-led confirmation/refinement/measurement correction is allowed;
retain failed conditions and ALL adverse results. Percentages inform judgment,
not automatic vetoes. Distinguish simplification, allocation and demonstrated CPU
improvement; preserve slower call-control/arena lessons without permanent bans.

Fully managed/memory-safe; no unsafe/Unsafe/pointers/PInvoke/native probes. Keep
host mediation, managed stack admission and current-token cooperative cancellation.
No fuel/quotas/watchdogs, hidden caps/default allowances, tracing/weak ledgers/forced
GC or new ownership infrastructure. Coarse borrowed reservations admit capacity/
metadata/overlap and fund results/aliases/caches/pending users through actual cleanup.
First refused admission stays terminal with original origin, no retry/fallback;
explicit guest MemoryError remains catchable. B132 admits whole workspaces before
construction, including unused branches/unstarted generators. Bounded streaming
uses host-driven batches. AGENTS.md and docs/RUNTIME.md specify lasting contracts.

Focused Release checks during iteration; batch Windows/Ubuntu.NET11/10, original
Stress and installed consumers at meaningful delivery boundaries. Current concise
docs only; histories/plans/evidence outside docs/. Publication remains postponed.


B138 initial timing complete170.748s; ninth job/goldens/quiet/joins PASS. Report
warm−7.91/−8.23% versus control+1.93/−0.28%, JSON−2.15/−1.45% versus−1.09/−0.85%.
Unique-key control adverse+36.68% elapsed/+70.49% CPU versus+3.17/−4.00%. No
promotion. Allocation launcher failed from a final CR in the wrapper; timing is
complete and will not be repeated for that tooling correction. Run only the still
missing allocation diagnostic with the original frozen candidate. Retain failure.

One evidence-led refinement selected: replace per-entry index admission/retained
headroom with capacity-level admission at actual growth boundaries, including old/
new overlap before EnsureCapacity. Reuse existing temporary reservations and release
only the discarded private table capacity; retain names/aliases and the live index.
No new owner, ledger, eviction, cap or fallback. This removes root-resize calls for
entries fitting already funded storage and reduces unique-name conservative
retention. Recheck focused guarantees and one bounded affected/control confirmation;
explicitly decide remaining unique-key debt, without another unmotivated attempt.
