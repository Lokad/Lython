# Lokad.Lython 0.11.0 — active delivery plan

Updated2026-11-03 (Paris). Git-ignored; never force-add. Publication/tagging remain
postponed. Whole-script performance is OPEN beyond any individual trial. Current
HEAD802fb318 is FULLY QUALIFIED by41554275552. B139 hash handoff WITHDRAWN;
source restored with fresh nonincremental build and319 internal/1535 public per
target PASS. B140 compiled-literal source gate COMPLETE: no implementation selected.
No active VM/CI job. B141 native wildcard cursors are the next selected milestone.
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
| B138 JSON property names | COMMITTED802fb318/FULLY QUALIFIED41554275552. Report warm about9–13% below controls, allocation−13.81%; JSON-lines allocation−21.47%, modest timing benefit. Unique control elapsed+31.50%/CPU+69.67%/allocation+27.99% remains OPEN debt. Native index capacity admission replaces per-entry reservation; evidence2026-11-03-shared-json-property-names. |
| B139 hash handoff | WITHDRAWN. Report/JSON elapsed essentially flat versus802; unique allocation+4.855%/long+1.862%. Extra key/factory API removed. Full same-run controls and source patches retained2026-11-03-native-key-hash-withdrawn. |
| Optional producer dev.10 | Development qualification COMPLETE/cached:84 internal/424 public per target, six real compile/match/cursor cancellation probes. Ordinary nonpackable adapter pin0.4.0 unchanged; core dependencies0. |

**FULL GREEN41554275552/exact802fb318885b59051c7387ec19fd7e1af48ac4d3:**
Windows/Ubuntu ordinary Release.NET11/10, original Stress both and installed
consumers PASS; every required step verified. Cached in2026-11-03-shared-json-
property-names/delivery-ci-status.json. Restored source exactly802; binaries rebuilt
at802 carry its informational version rather than the earlier1e73 build metadata.
NEVER query41554275552 or older terminal41540517622/41535109342/41519435155/
41504778956/41501504578/etc again. No unchanged qualification replay.

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

## Priority1 — B141 native UTF-8 wildcard matching and rune-cache removal

B139 did not justify its larger key/factory representation. B140 source gate found
that source literals are per-code-object, generator/class-function compilation has
separate roots, and shared literal indexing needs coordinated compiler storage and
admission (current coarse compilation estimate96 bytes/source character). Directly
reusing compiled strings also changes cache lifetimes. Do not start that frontend/
ownership redesign merely to salvage the interner. B138 is retained with its
measured algorithmic tradeoff: repeat-schema workloads improve, unique names pay
extra index/probe/storage costs. Keep this limitation visible; no fixed threshold
veto, heuristic cache limit or claim that its regression is corrected. A future
coherent storage redesign may revisit it, not another narrow hashing attempt.

Selected B141 source evidence: FnMatchModule.MatchSimple calls GetRunes for BOTH
inputs. Unicode strings retain a PyString array plus one PyString/backing per
non-ASCII scalar; ASCII arrays are recreated per match. Character ranges then
convert single-rune strings back to CLR text. GetRunes has exactly one production
caller. Its cache and partial-build rollback exist solely to fund this avoidable
materialization; compiled literals also lack an execution governor on that path.

Replace these rune arrays with two UTF-8 byte cursors and scalar decoding only at
Unicode comparisons/ranges. Keep the current iterative wildcard/backtracking and
character-class grammar; no second matcher, regex dialect, program compiler, token
array, pool, new owner or policy. Remove PyString.GetRunes, _cachedRunes, its cache
charges/rollback and orphaned per-rune conversion helpers. Offset-table/index
protection has other callers and stays. Keep already-funded input bytes and result
list/aliases; matching itself needs no substantial scratch. Remove architecture-
only rune-cache tests/prewarming; preserve repeated matching, Unicode/class/range/
malformed-bracket goldens and the real in-flight cancellation regression.

Compatibility: Python fnmatch values, ordering/aliases, scalar '?' and Unicode
ranges stay unchanged. No promise of internal rune-object identity. Keep current
host-mediated glob/file behavior and translate/provider contracts. Refusal remains
terminal; result construction still preflights. Cancellation must reach scanning,
class parsing and non-allocating backtracking; no quota/counter enforcement.

Acceptance: focused Release fnmatch/glob/string/cancellation/admission/ownership
checks; independent CPython goldens for astral/combining/ranges/negation/malformed
classes; existing shared-budget/result/cancel cleanup. One bounded complete file-
list filtering comparison (Unicode and ASCII input) with unchanged report/text
controls, warm and compile-to-result separate; allocations separately. Hypothesis:
remove dominant per-character materialization, not just improve one local loop.
Keep all adverse/control results; refine only on concrete evidence and decide any
regression debt at completion. Retained change gets one established full platform/
stress/package boundary; publication remains postponed. Refresh external comparisons
at that coherent checkpoint, with honest compile/I/O/cleanup boundaries.

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

B141 measured/retain decision: seven jobs+allocation complete179.037s/exit0, all
identities/goldens/quiet/joins PASS. Unicode file filter warm−48.06/−48.61% versus
control−1.78/+0.62%; CPU−76% versus about+1.5%. ASCII elapsed−10.84%/control+0.08%,
CPU−36.18%/control−1.11%. Fresh Unicode−49.49%, ASCII−13.57%; CPU controls drift.
Allocation Unicode−84.94%, ASCII−43.56%, text−2.88%, report−0.79%. Text/report CPU
not improved conclusively; do not claim general speedup. Retain coherent deletion.
Focused29 internal/185 public per actual target plus18 streaming/golden cases per
target PASS;54,832 independent scalar/grammar differential comparisons agree with
CPython. Native direct matching uses no admitted scratch and preserves real in-flight
cancellation. No live VM work; full new delivery checks pending.

B142 correctness before that delivery: public probe confirms existing CLR
ArgumentException from fnmatch.translate('[😀-🙏]'). Its UTF16 range loop enters a
low surrogate. Translation also uses ^...$ without DOTALL, disagreeing with native
fnmatch on newlines/trailing newlines. Glob has another duplicated class translator
with different negation/closing-bracket rules. Consolidate ONE scalar-aware class
translator reused by fnmatch/glob; remove duplicate class code. Advance entire Rune
values and append spans, preserving cancellation and existing allocation preflights.
Use Python-compatible scoped DOTALL plus strict \Z end anchors for both translated
patterns. This intentionally changes returned regex text, preserving correct match
semantics. No regex backend migration or new dependency/policy.

Acceptance: astral/BMP ranges, invalid ranges, ! versus literal ^, leading ], literal
hyphens/unclosed classes, newlines and exact end; independent CPython match/search/
fullmatch goldens through the optional pinned adapter, existing native and translated
glob checks, refusal/cancellation/cleanup. Existing exact-format assertions update
to reference-compatible output; do not drop semantic assertions. B141 timing remains
its frozen source pair; no speedup claimed for B142. Batch one full platform/stress/
installed delivery after both; refresh external comparisons for final source at
that coherent checkpoint. Publication postponed.
