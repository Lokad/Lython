# Lokad.Lython 0.11.0 — active delivery plan

Updated2026-11-03 (Paris). Git-ignored; never force-add. Publication/tagging remain
postponed. Whole-script performance is OPEN beyond any individual trial. Current
HEAD802fb318 contains B138; earlier runtimefd7bc7e4 remains fully qualified.
B136 source gate COMPLETE; B137 manual JSON reader WITHDRAWN and committed1e73f72f.
B138 shared property-name storage COMMITTED/FOCUS-QUALIFIED, retained as an
intermediate milestone with OPEN unique-input regression debt. Controlled timing
and allocation complete; no live VM work. Full delivery CI41554275552 is running
at exact802fb318885b59051c7387ec19fd7e1af48ac4d3; last checked2026-11-03 01:45 Paris, both platforms running ordinary.NET11 checks.
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
| B138 JSON property names | COMMITTED802fb318/FOCUS-QUALIFIED; CI41554275552 pending. Report warm about9–13% below controls, allocation−13.81%; JSON-lines allocation−21.47%, modest timing benefit. Unique control elapsed+31.50%/CPU+69.67%/allocation+27.99% remains OPEN debt. Native index capacity admission replaces per-entry reservation; evidence2026-11-03-shared-json-property-names. |
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

## Priority1 — close B138 delivery; B139 native key construction/hash design

Run ONE established Windows/Ubuntu Release.NET11/10, original Stress and installed
consumer checkpoint for retained B138. Work continues while CI runs; query minutes
apart and cache its terminal result. No unchanged fd7bc7e4 qualification replay.

B138 removes real repeat-schema storage/admission/hash work, with report CPU about12%
lower than controls and material report/JSON allocation reductions. It also adds a
content index that makes all-unique input slower and larger. Capacity-level admission
reduced conservative retention and repeated root-resize calls but did not remove
that algorithmic cost. Retain this intermediate milestone, not a general JSON win.

B139 gate: map how the existing OrderedPyDictStorage/IPyDictStorage and JSON key
construction duplicate key/index/record storage. Compare with complete call/frame
activation: ordinary positional calls already bind operand spans into final locals,
reuse that storage and use admitted explicit frames; no missing packet copy exists.
Current CPU evidence does not demonstrate separate activation/boxing dominance.
Do not resurrect slower arenas/call-control or infer value from large source files.

Select ONE coherent deletion/replacement milestone from that map, with the work and
storage removed, affected report/JSON/CSV/text scripts and displaced implementation.
Preserve order/duplicate/equal-key identity, mutable views, protocol hashing/equality,
mutation/reentrancy, callbacks/async, closures/generators as applicable, cancellation,
first refusal, independent records, aliases and admitted replacement peaks. Reuse
existing owners; no key-count heuristic, pool, second parser or generic framework.

B139 source findings: OrderedPyDictStorage already centralizes Python ordering,
key retention and capacity preflight through IPyDictStorage; replacing it broadly
would touch protocol scans, mutation views and aliases, not remove a gratuitous
wrapper alone. B138 necessarily adds a separate content index. A new property name
currently hashes UTF-8 for lookup, again for insertion, then lazily in PyString for
the guest dictionary. Shared hits reuse the stored hash. Capacity refinement removed
per-entry index admission but not those passes. This is demonstrated source work;
its precise fraction of the unique-input CPU regression is still a hypothesis.

Assess a unified native UTF-8 construction/hash handoff: compute a content hash once
for the probe, retain it in the index and initialize owned PyString hashing from
the same kernel. A prehashed key can enlarge index entries; measure that explicit
tradeoff rather than assuming a win. No comparer mutable-last-key tricks, borrowing
input as stored keys, extra backend, string interning framework or callback factory
state. Preserve independent owned bytes, hash equality on all strings and stable
per-process hashing. If this cannot simplify a real path without disproportionate
storage/API machinery, choose a documented tradeoff or withdrawal and move on.

Stop after the source/cost map and a concrete design decision. If a justified
consolidation is selected, define its completion milestone before code, then one
bounded warm/fresh/compile/allocation before-after comparison with unchanged and
unique controls. Temporary regression debt must receive a retention/withdrawal
judgment at completion. If the source cannot justify more machinery, explicitly
choose the documented tradeoff or withdraw the intermediate index, then proceed to
another substantial roadmap area. No automatic new CPU-capture/retiming campaign.

CPython refresh stays at the next meaningful retained redesign checkpoint; current
fd7bc7e4 tables remain accurately labeled. Publication remains postponed.

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



## B139 selected implementation — one native key/hash handoff

Replace Utf8PropertyNameComparer/ROM cache keys with a small PyString.Utf8Key value
carrying immutable bytes and the existing native content hash. One shared hash
kernel serves ordinary lazy PyString hashing and probes. A probe copies into an
admitted owned PyString and initializes that same hash; stored index keys borrow
only the resulting owned string. Escaped decoded strings compute their hash once.
Delete the separate comparer and two hash passes on ordinary misses. No mutable
comparer state, caller/token capture, generic interner, borrowed-input retention,
new owner or independent policy. Keep the one parser and existing index admission.

Tradeoff: the value key grows from16 to24 bytes on64-bit; index entries can grow
from32 to40 bytes plus buckets. Existing48-byte/capacity headroom covers those
major slots and growth overlap; verify on supported targets. No identical tiny
threshold promise. Native strings must keep exactly the same hash/equality and
UTF-8 content, including empty, sliced, escaped, Unicode and long names; independent
owned bytes, mutable records, reentrant hooks, refusal/cancellation and retained
results remain funded. Add only hash-handoff/ownership coverage missing from the
existing JSON/dict/string/storage regressions.

One controlled comparison: current802fb318 versus candidate AND pre-B138fd7bc7e4
with unchanged controls, complete report/JSON-lines and short/long unique-name
workloads, report compile-to-result separate. Separate allocation diagnostic tests
the larger-key tradeoff. Existing tools, ordinary GC/tiering, full goldens, actual
cleanup/joins and VM lease. No external CPython ratio from this source pair.

Completion decision: remove the old comparer, retain only a justified single
implementation, and explicitly decide remaining unique CPU/allocation debt. A
negative/inconclusive observation is preserved; justified bounded correction is
allowed, no automatic new campaign. Batch full checks only if retained. CI802
continues independently; no publication.
