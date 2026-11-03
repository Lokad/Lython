# Lokad.Lython 0.11.0 — active delivery plan

Updated2026-11-03 (Paris). Git-ignored; never force-add. Publication/tagging remain
postponed. Whole-script performance is OPEN beyond any individual trial. Current
HEADc31d639d contains fully qualified runtimefd7bc7e4 plus documentation/evidence.
No active prototype, local test/build, VM worker or qualification run remains.
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

## Priority1 — B136 source-backed construction redesign

One bounded CURRENT source/storage/lifetime map of JSON container/token construction,
remaining text conversions and complete call activation. Existing plain scalar/string
JSON paths, lazy namespaces, final bound locals, tiny cleared argument pools, compact
instructions and closed native contexts are real delivered work. Do not invent
missing copies or infer dominance from async method names/boxed BigInteger alone.

Assess whether a single resumable container parser or consolidation using existing
capacity admission can eliminate meaningful recursion/transport/conversions without
duplicate parsers or new memory infrastructure. Do not remove required budget locks.
Preserve Python numeric/object hooks and real suspension, duplicate/error/offset
behavior, depth/managed-stack safety, retained callback aliases, first refusal and
actual awaited cleanup. No implementation is selected from a name or sample alone.

Stop this gate after one design map: select a coherent displaced implementation,
affected complete scripts and semantic/cancellation/ownership acceptance, then one
bounded controlled warm/fresh comparison with compilation/allocation separate.
If evidence is insufficient, record uncertainty and select another roadmap area;
no automatic new capture or sequence of builtin tweaks. Temporary regressions need
an explicit completion milestone and retention/debt decision, never stranded modes.

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

## B136 source gate complete; B137 selected — one resumable JSON container reader

Current source map: scalar/string decoding already returns completed ValueTasks,
uses native UTF-8 and preserves scalar frame admission across actual callbacks.
Default options are immutable/shared. No missing final-locals copy or eager function
namespace exists. Object reading duplicates its full grammar for pairs-hook/default
collectors; arrays/objects recurse through async methods for every container even
when no operation suspends. Native async completion need not allocate heap state;
removing those call/state transfers is a CPU hypothesis, not an allocation claim.
Per-parse key memoization adds storage/reentrancy complexity and may penalize flat
JSON-lines; it is not selected without a stronger lifetime/construction design.

Replace both recursive container readers and duplicated object loops with ONE
resumable JSON reader, shared by Run/RunAsync and loads/decode/raw_decode. Reuse
existing scalar token decoding, string errors, numeric frames and hook invocation.
A value-type current frame handles flat documents without new heap storage; only
nested containers need an explicitly admitted parent-frame array, initially small
and reused through the parse. Actual suspension transfers this existing state to
one awaited continuation; no second parser, dialect subset, cache, pool or option.
The existing64-container compatibility/depth boundary remains in this milestone.

Delete ReadJsonObjectAsync/ReadJsonArrayAsync and recursive value dispatch between
containers. Expected work removed: nested async method/builder transitions and
repeated grammar/state transport, not useful UTF-8 scanning/dictionary work. Raw
positions remain original-subject byte offsets, with existing scalar conversion.
Private parent capacity is funded before allocation/replacement overlap through an
existing temporary reservation; clear/release it only after every callback/user
and cleanup finishes. Output containers/keys/callback aliases retain batch ownership,
including failure; no speculative refunds of constructed guest values.

Acceptance: default and pairs/object/numeric/constant hooks (including genuine I/O,
reentrancy and callbacks retaining arguments), mixed nesting, duplicates, raw prefix
indices/end positions, strict/lenient/invalid UTF-8/control/error order and current
64-depth behavior. Preserve terminal first refusal, cancellation during scans and
pending hooks, source aliases, ownership/cleanup and bounded host-driven batches.
Use existing focused JSON/managed-stack/admission tests; add meaningful regressions
only for gaps introduced by resumable state or workspace ownership.

Decision boundary: one complete report/JSON-lines/config controlled comparison with
unchanged text/CSV controls; warm/fresh separated and allocation diagnostic separate.
Hypothesis: substantial repeated-container reports improve without flat JSON-lines
regression. Keep all adverse data; bounded confirmation/refinement is allowed if
controls or a concrete defect justify it. Explicitly decide remaining debt before
retention; no mandatory percentage veto and no stranded alternate implementation.
Batch full platform/stress/installed consumers only if retained at this coherent
boundary. Prior full41540517622 remains cached; no publication.
