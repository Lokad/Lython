# Benchmark experimental history

Historical records preserved on 2026-10-12. Revision-specific findings below
must not be read as measurements of current HEAD. See [BENCHMARKS.md](../BENCHMARKS.md)
for the authoritative comparison and [COMPARISON.md](COMPARISON.md) for methods.
The original narratives and evidence links follow unchanged. Collector-first
ownership experiments below are historical and superseded by governed memory;
their reproduction sources are evidence and are outside the supported runtime.

The [2026-10-16 eager-comprehension delivery](results/2026-10-16-inline-comprehensions/README.md)
uses ordinary instructions in the caller frame with the existing binding scope.
One declared per-item helper-call diagnostic observed 0.6107 -> 0.5851 ms,
CPython 0.1253 ms; Lython spread 1.1%/0.9%, CPython spread 38.3%. The 4.2%
lower Lython median is unqualified and the external ratio is inconclusive. No
retuning, repeat or retired arithmetic producer. Lazy bodies/nested lambda
capture remain dynamic; duplicate generator-family dispatch was removed.

The [2026-10-15 lambda delivery](results/2026-10-15-executable-lambdas/README.md)
deletes the separate callable, reuses shared binding/metadata and then compiles
eligible bodies through the named-function builder/cells. One unchanged stable-sort
case observed 1.778 -> 1.595 ms, CPython 0.3020 ms, spread 2.4%/3.5%/0.7%:
10.2% lower median, unqualified. Both roles already contain runtime consolidation;
the class deletion is unmeasured. No new fixture or repeat capture.

The [2026-10-15 expanded-call delivery](results/2026-10-15-executable-expanded-calls/README.md)
shares generator call lowering and first fixes argument evaluation for non-callable
targets. One fixed forwarding-helper diagnostic observed 2.412 -> 2.077 ms,
CPython 0.2443 ms, spread 0.6%/2.2%/0.9%; observed 13.9% lower median, unqualified.
No new binder or owner; an obsolete starred-call fallback assertion was removed.

The [2026-10-15 f-string delivery](results/2026-10-15-executable-fstrings/README.md)
reuses existing generator field/specifier/join operations. One fixed helper
diagnostic observed 2.454 -> 2.125 ms, CPython 0.4201 ms, with 1.3%/2.5%/2.5%
spread. The observed 13.4% lower median is unqualified. Shared async formatting
helpers remain; this closes a syntax fallback without a new runtime subsystem.

The [2026-10-15 expanded set/dictionary delivery](results/2026-10-15-expanded-set-dict-executable/README.md)
shares existing generator operations and corrects own-value-before-key-validation
order first. One helper diagnostic observed5.444 ->4.963ms, CPython0.6326ms;
spread0.7%/1.3%/1.4%, without a qualified multiplier or retuning. Small hash-trace
differences were reclassified using primary compiler source and a16-entry probe;
no CPython-threshold staging system was added.

The [2026-10-15 ordinary starred-tuple lowering](results/2026-10-15-starred-tuple-executable/README.md)
shares existing generator display/immutable-finish operations. Qualification fixed
empty tuple identity in both roles first. One helper diagnostic gave2.659 ->2.629ms,
CPython0.3477ms, within1.1%/5.6%/0.6% spread: no elapsed benefit or retuning.
Conservative working-list/result overlap and fuel changes are explicit contracts.

The [2026-10-15 ordinary starred-list lowering](results/2026-10-15-starred-list-executable/README.md)
reuses existing generator display operations, removing one real expression
fallback and associated local mirroring without runtime changes. One shared
helper diagnostic observed2.664 ->2.179ms, CPython0.3541ms; spread1.3%/0.7%/0.3%.
No qualified multiplier, complete-job claim or rerun of rejected comprehensions.

The [2026-10-15 fresh configuration job](results/2026-10-15-config-fresh/README.md)
compiles once and edits/publishes one JSON file per invocation in an already-running
host. Two identical654b4f5a Lython controls gave2.817-3.142ms, CPython1.572ms;
their11.5% discrepancy and4.7%/5.7%/9.5% spread remain visible. No qualified ratio,
earlier/later optimization claim or retuning. Same-path fixture replay preserves
fresh inputs and independently checked output bytes.

The [2026-10-15 async exact-integer operator family](results/2026-10-15-async-integer-operators/README.md)
extends the existing synchronous handler gate beyond addition. Yielding controls
first exposed and fixed synchronous invocation of other augmented user slots.
One unchanged branch-loop diagnostic observed 9.846 ->6.321ms, CPython0.9599ms;
spread3.0%/10.4%/0.5%. No qualified multiplier, complete-job claim or retuning.

The [2026-10-15 exact-byte accumulator](results/2026-10-15-bytes-sum/README.md)
removes byte iteration adapters, per-item integer boxes and repeated arithmetic
under existing native ownership. Final magnitude admission precedes allocation;
dynamic starts still suspend. One complete manifest job is6.394 ->6.080ms,
CPython0.4382ms, within6.7%/11.7% spread; no qualified elapsed claim or retuning.

The [2026-10-15 JSON value-dispatch cleanup](results/2026-10-15-json-value-dispatch/README.md)
removes one generated async forwarding state machine without another parser.
One complete JSON file job is 9.425 ->9.692ms, CPython4.260ms; the change is within
14.3%/7.3% spread. No elapsed benefit is established; no repeat or retuning.

The [2026-10-15 UTF-8 uppercase delivery](results/2026-10-15-utf8-upper/README.md)
removes ordinary per-scalar CLR strings and shares existing expansions with legacy
casing helpers. One new complete report job is identical in both roles:
2.610 ->2.651ms, CPython0.2444ms, spread9.5%/10.8%. No elapsed benefit established;
keep allocation removal and independent report/legacy casing checks, without retuning.

The [2026-10-15 UTF-8 lowercase delivery](results/2026-10-15-utf8-lower/README.md)
removes ordinary per-scalar CLR strings while preserving expansions, final sigma,
governed capacity and cancellation cleanup. One complete file-job diagnostic is
7.107 ->6.553ms, CPython1.231ms; the 7.8% difference is near baseline spread7.5%,
so timing remains inconclusive. Keep allocation removal; no retry/retuning.

The [2026-10-15 compiled-layout prototype](results/2026-10-15-parameter-layout-rejected/README.md)
is rejected and removed: fresh default-helper compile/run is 185.643 ->293.721us,
CPython37.302us. Preserve the single capture and source patch, without retuning
or attribution to a particular component. Only an unused binding map/field deletion
and public isolation/admission regressions remain; that final cleanup is unmeasured.

The [2026-10-15 async startup reuse](results/2026-10-15-async-worker-reuse/README.md)
removes per-invocation thread construction and the async-void relay. One empty
setup diagnostic predates a required structural ambient correctness fix; preserve
its exact prototype identity and noise. No final-source or useful-script timing
claim follows. Async ambient scopes are narrowed to synchronous work after pulls.

The [2026-10-15 executable call-cache deletion](results/2026-10-15-call-cache-deletion/README.md)
removes an identity-only record, unused async frame arrays and compiler cache
metadata. One synchronous keyword-call diagnostic is within spread; no elapsed
claim or retry. General invocation and member resolution remain intact.

The [2026-10-15 positional-call delivery](results/2026-10-15-positional-calls/README.md)
removes packaged arguments and argument-factory closures from eligible compiled
calls while sharing canonical frame/body cleanup. One async helper diagnostic
observes a 13.7% lower median; baseline spread is 9.4%, so no qualified multiplier.
Bound-values/frame copying and receiver-independent method dispatch remain open.

The [2026-10-15 live-defaults delivery](results/2026-10-15-function-defaults/README.md)
removes disconnected binding metadata and persistent default-map fees, admits
layout/default capacity and awaits custom-key lookup. One async default-call
diagnostic has a 4.2% higher median; no speedup or qualified regression is claimed.
The CPython dictionary-lifetime edge and original-scale accounting boundaries are
documented explicitly. Direct calls and immutable layout sharing remain open.

The [2026-10-15 method-exposure delivery](results/2026-10-15-method-exposure/README.md)
corrects exposed builtin method identity while keeping immediate-call caching.
One dictionary-update diagnostic is within spread; no timing claim or retry.
Receiver-independent dispatch and direct argument binding remain separate work.

The [2026-10-15 async integer-addition delivery](results/2026-10-15-async-integer-dispatch/README.md)
shares the synchronous observed-value handler for exact integers. One async-loop
diagnostic observes a 14.4% lower median with 6.0% candidate spread. A genuinely
yielding in-place addition witness also exposed and fixes synchronous user-slot
dispatch. Other augmented operators remain separate work.

The [2026-10-15 async-function delivery](results/2026-10-15-async-compiled-functions/README.md)
shares compiled function construction and real async bound-slot/cell execution.
One completed-host async diagnostic observes a 40.3% lower median; the sync
control is inconclusive. The import refresh bug and an initial client-validator
failure are preserved separately from the valid timing samples. Broader async
dispatch and mutable function metadata remain open.

The [2026-10-15 list-comprehension investigation](results/2026-10-15-list-comprehensions-rejected/README.md)
rejects two executable-list producers despite passing semantic/containment gates.
A direct iterator branch removes added boolean/dispatch work but does not show
a benefit over the existing evaluator. Public regressions and raw evidence remain;
no dormant runtime implementation or architecture-specific tests are retained.

The [2026-10-15 protected-loop checkpoint](results/2026-10-15-protected-loop-jumps/README.md)
removes ordinary protected-loop compilation rejection and preserves abrupt-target
regions during jump normalization. Separate fresh compile/run and warm file-search
diagnostics remain inconclusive; structural and cleanup correctness justify the
smaller compiler. Async function and comprehension parity remain separate work.

The [2026-10-14 complete file comparison](results/2026-10-14-representative-file/README.md)
records identical Lython controls against CPython. Background JIT and fixture
costs limit CPU attribution; no speculative runtime branch or timing repeat.

The [2026-10-14 complete text comparison](results/2026-10-14-representative-text/README.md)
records one final-sigma lookup candidate and rejects its inconclusive timing.
The exhaustive ASCII context regression remains; the runtime shortcut was removed.

The [2026-10-14 list and enumeration checkpoint](results/2026-10-14-list-and-enumeration/README.md)
records published list construction and explicit sequence wrapper state. Separate
list+sum and generator/drain diagnostics remain inconclusive; the combined
correctness gates do not establish GC-independent reclamation or host latency.

The [2026-10-14 native state checkpoint](results/2026-10-14-native-working-state/README.md)
combines native buffer descriptors, active caller links and stored sum state.
It preserves bounded streaming and rejection controls; complete ownership and
forced-GC independence remain open. One short generated-sum comparison is
inconclusive and is not a qualified speed or embedding-latency claim.

The [2026-10-13 native activation array](results/2026-10-13-native-activation-rejected/README.md)
passes its focused root/streaming gates but remains incomplete and adds recurring
bookkeeping before a GC-independent rule exists. Its one short call measurement
is inconclusive (+6.79% median,6.76% candidate spread). The standalone foundation
is rejected and reverted; neither current-HEAD timing nor proven regression is claimed.

The [2026-10-13 ownership inventory](results/2026-10-13-ownership-inventory/README.md)
identifies native state, delegate and frontend-capture obligations beyond the
34 snapshot-owner types. Its finite static audit and36 public root checks are
correctness/design evidence; no collector, logical refunds or performance claim
is introduced. Forced-GC replacement remains open.

The [2026-10-13 ordinary-call transfer](results/2026-10-13-call-transfer/README.md)
preserves inputs and results in the existing operand stack at call boundaries,
without universal instruction retention. Nine before/after probes validate this
prerequisite; forced-GC independence remains open. The one short keyword-call
diagnostic has uncertain recurring cost and supplies no qualified performance claim.

The [2026-10-13 frame-transfer experiment](results/2026-10-13-frame-transfer-rejected/README.md)
passed candidate correctness gates but added about 10% to the short integer-loop
diagnostic. The universal retained extent/completion rule was rejected and
reverted. These candidate timings are historical; they do not describe HEAD.

# Lython and CPython comparison contract

Specification version **6**, used by the declared milestone collections. The catalog
and explicit correctness check are available. Persistent worker primitives are
available, including supervised correctness/timer smokes, paired collection,
machine checks and raw-evidence report rendering.
The [explicit body-bytecode round](results/2026-10-11-method-body-bytecode/README.md)
rejects a flat instruction-loop compiler: method time changes -2.12%/+5.99%,
so its target gain does not repeat. Both producers pass 9,526 matching Debug and
Release tests and seven canonical CPython cases each. Fourteen paired groups
take about eight seconds, separate allocation/native groups about four seconds,
all under 30-second caps. All controls and native tiers remain visible. Eleven
independent control-flow/host boundary cases are added; an additional post-gate
lifetime check passes separately on both producers. Production remains unchanged,
and cold compiler costs and broader design work stay open. No full timing lane
or favorable recollection ran.

The [typed body-plan round](results/2026-10-11-method-body-plan/README.md)
rejects the isolated compiler prototype: method time changes +1.36%/-8.68%,
while keyword calls regress in both passes. Both producers pass 9,503 matching
Debug and Release tests and seven canonical CPython cases each. Fourteen paired
groups take about eight seconds; separate allocation/native groups take about
four seconds, all under 30-second caps. Every control and native tier is retained.
Production stays unchanged; broader compiler/design work remains open. No full
timing lane or favorable recollection ran.

The [lowered operation-selection round](results/2026-10-11-lowered-kind-dispatch/README.md)
rejects preselected node dispatch: method time changes +3.2%/-3.4%, with the
second gain inside its 3.9% spread. Fourteen paired micros take 8.04–8.05s
including cleanup; separate allocation/native groups take about four seconds.
Both producers pass 9,466 matching Windows Debug and Linux Release tests plus
seven complete canonical CPython cases each. Eight existing fixed checkpoint
fixtures now exercise both shared and direct function dispatch. Every control
and native tier remains visible; production stays unchanged and the broader
method-body compiler remains pending. No full timing lane or recollection ran.

The [prepared augmented-target round](results/2026-10-11-prepared-augmented-target/README.md)
rejected; the prototype remains isolated. It compares identical slice-protocol/governance corrections
on both producers, with 42 independent pre-prototype checks. Fourteen paired
micros take 8.04–8.05s including cleanup; separate allocation/native
checks and all controls remain visible. No full lane or favorable recollection
is added; the next milestone remains deferred.
Initial delivery CI exposes a Linux `/proc` read race in an existing test helper.
Two actual-worker checks reproduce it; the corrected supervisor suite passes
all 31 cases on both platforms without changing production or timing evidence.

The [corrected function-scoped dispatch round](results/2026-10-11-scoped-dispatch-corrected/README.md)
accepts direct lowered dispatch only inside active synchronous function bodies.
Method jobs take 4.4% / 10.1% less time and allocate 1.3% / 4.4% less in two passes,
with no repeated control regression beyond the frozen bounds. Fourteen paired
micros take about eight seconds each; separate allocation and native-code groups
take about four seconds. Both producers pass 9,399 Windows Debug tests and 9,399
Linux Release tests, plus seven complete canonical CPython cases each. All controls and emitted native tiers
remain visible. No full lane, recollection or milestone-qualified ratio is added.
The original delivery failed Linux CI; both new producers include the same stack
publication and exception-propagation corrections. Its original measurements,
failed qualification and diagnostic trials remain visible in the reports.

The [current class-method design assessment](results/2026-10-11-class-method-design/README.md)
adds two ten-second CPU traces (14–17s including cleanup) and 23 independent
checks on unchanged production. Attribute lookup, assignment and method invocation
remain substantial sampled costs. Real class-factory checks pin fuel/funding/cell
boundaries and genuine async suspension with same-run reentry. Executable
class-method bodies remain pending; no new timing ratio or full lane is added.
Initial Windows CI exposed an existing concurrency test's dependency on caller
ThreadPool scheduling. A 10.5s isolated diagnostic reproduces that dependence;
dedicated test callers preserve the deadlines and pass under a one-thread pool.
Production and original capture evidence remain unchanged.

The [2026-10-09 bounded run](results/2026-10-09-quick/README.md) completed all four
lanes within ten minutes each. Its raw-evidence review found no qualified
workload ratios; its exclusions remain unchanged.
The [focused basic-loop investigation](results/2026-10-09-core-loop/README.md)
completed in 2m13s. Its separate CPU profile identifies interpreter dispatch
as the leading design investigation; strict stability checks still withhold
qualified comparative multipliers.

The [first synchronous dispatch improvement](results/2026-10-09-sync-dispatch/README.md)
used two short collections of about seven seconds each. Their diagnostic medians
show 13.4–14.0% less time on the 16,384-iteration loop, with correctness checks
passing and milestone qualification deferred.

The [subsequent short experiments](results/2026-10-09-integer-addition/README.md)
accepted guarded exact integer addition after 10.7–14.1% less loop time in two
seven-second collections, and rejected direct loop-name storage after inconsistent
results. Every collection is retained. A separate operator-fallback correctness
fix preserves the measured integer guard.

The [bound loop-name compiler change](results/2026-10-09-bound-loop-names/README.md)
showed 20.2–22.4% less loop time in two collections of about seven seconds each
against a baseline with the same independently tested function-scope fix.
Simple targets reuse existing bound stores; complete outputs and checkpoints
remain checked. Both receipts are retained, with milestone qualification pending.

Ordinary improvement rounds use the short `micro` command below. Full lanes
are reserved for occasional declared milestones, with their existing ten-minute
cap and qualification rules. Microbenchmarks provide diagnostic feedback in
seconds; they do not run idle gates, establish a qualified speedup or replace
correctness checks.

The [first consolidated milestone](results/2026-10-09-first-milestone/README.md)
collected the original runtime and current runtime with an identical v6 harness
and fixed 14-case warm profile. Its lanes lasted 5m58s and 6m28s. Both controls
qualified, but no workload qualified on both producers. All original exclusions
remain visible. The current ASCII pipeline and zlib application qualified;
the basic-loop comparison still withholds a certified multiplier.

The [second consolidated milestone](results/2026-10-10-second-milestone/README.md)
completed two warm lanes in 6m00s and 6m17s. The current basic loop, keyword
calls, stable sort, JSON and zlib qualify against CPython. The basic loop takes
about 2.6–2.8 times CPython's time. The reference tiny control failed spread,
so no old/new improvement ratio qualifies. The current ASCII pipeline falls
below the unchanged invocation floor. Every exclusion remains final; ordinary
rounds keep using seconds-long micros.

The [string reclamation round](results/2026-10-09-string-reclamation/README.md)
used five short decomposition jobs and a separate 15-second CPU trace, followed
by two repetitions each of the pipeline and split/count. Direct string ownership
registrations reduced pipeline medians by 65–67% and split/count by 63–66% in
7–8-second collections. These remain diagnostic results; the next full milestone
is deferred.

The [module completion experiment](results/2026-10-09-module-completion/README.md)
used six collections of about seven seconds each. Direct synchronous completion
helped empty and tiny controls modestly, but the integer loop took slightly
longer in both repetitions, so that candidate remains isolated. An independent
writer publication cleanup correction is retained; no full lane was collected.

The [lazy string cache experiment](results/2026-10-10-string-cache-metadata/README.md)
used ten micros of 6.4–7.0 seconds and separate fixed-count allocation checks.
It reduced pipeline managed allocation by 5.11% but gave no repeatable target
latency improvement, so the candidate remains isolated. No full lane ran.

The [string ownership investigation](results/2026-10-10-string-ownership/README.md)
uses two boundary diagnostics of about 6.5 seconds and six public micros.
It identifies adoption as a target and rejects a direct string guard after
slower pipeline results. A structural ownership proposal remains pending.

## Short improvement rounds

The [call-frame investigation](results/2026-10-10-call-frames/README.md) uses two
fourteen-second CPU captures on existing positional/keyword jobs. A lazy function
namespace candidate remains isolated after a full Debug streaming-memory failure;
its timing micros are held. The report retains the failure and focused negative
controls. Production is unchanged, and the existing streaming test gains
failure-only allocation/window diagnostics for further investigation.

The [text-emission rollback correction](results/2026-10-10-streaming-emission-ownership/README.md)
fixes charges stranded when an unpublished text result's reclamation registration
is denied. Eight deterministic baseline probes fail; the fix passes all ten new
checks and the full Debug suite. Retained values stay charged. This separate
correctness prerequisite does not establish the original streaming failure's
cause or a speed improvement; it runs no timing micros or full lanes.

The [stable-sort decomposition](results/2026-10-10-stable-sort-decomposition/README.md)
uses twelve baseline variant micros, a separate bounded CPU capture and eight
old/new micros. Tuple-owned reclamation metadata reduces construction time by
about 25% in both replicas and isolated constructor/registration allocation by
18.7%. The change is integrated; high-variance sort results and the remaining
CPython gap stay visible. All micros take 6.4–7.5 seconds; no full lanes run.

The [exact integer sort-key round](results/2026-10-10-integer-sort-keys/README.md)
refreshes current construction/sort CPU leads with two bounded captures, then
accepts a guarded integer comparison after ten 6.4–7.3-second micros. Scalar-sort
time falls about 28% in both replicas; full sort/output falls 22–26%, retaining
its spread. Mixed keys retain Python dispatch, and all 9,140 Debug checks pass.
Full lanes remain deferred to a declared consolidated milestone.

The [small immutable tuple round](results/2026-10-10-small-tuple-adoption/README.md)
removes temporary adoption collections for tuples of up to two items while
preserving logical charges and rollback. Eight 6.4–7.3-second micros show 24–26%
less construction time; complete sort observations remain noisy. Separate retained
constructor/registration diagnostics show about 67% less allocation for scalar
pairs, with the larger-tuple control essentially unchanged. All 9,152 Debug checks
pass. No full benchmark lanes run.

The [streaming reservation-progress correction](results/2026-10-10-streaming-reservation-progress/README.md)
reproduces the unchanged 64 KiB streaming fixture under a controlled collection
schedule, including the original frozen namespace source. A successful scratch
reservation now lets the following allocation collect remaining garbage. Three
new regressions and all 9,155 Debug checks pass; pinned denials remain bounded.
Every untraced diagnostic process takes less than a second. There are no normal
timing samples, full lanes or speed claims; namespace qualification remains open.

The [new combined lazy-namespace round](results/2026-10-10-function-frame-namespaces/README.md)
passes its own full 9,161-check Debug gate before ten 6.4–7.3-second micros.
Complete-call managed allocation falls about 11%; the change is integrated as an
allocation improvement. Call timing changes remain below spread or mixed, so no
reliable throughput gain is claimed. The original failed namespace receipt stays
preserved. No full lanes run; future milestones keep their ten-minute cap.

The [direct instruction-array round](results/2026-10-10-instruction-arrays/README.md)
uses compiler-owned arrays directly in dispatch, with readonly synchronous reads.
Twelve 6.4–7.2-second micros show 13–15% less loop time, 7–8% less positional-call
time and 9–12% less keyword-call time. Empty/pipeline controls have small mixed
changes; sort remains too noisy for a reliable claim. All 9,161 Debug checks pass
before timing. No full lanes run; qualification waits for a declared milestone.

The [execution-check cold-path round](results/2026-10-10-checkpoint-cold-paths/README.md)
keeps sweep/exception work separate from an inlined checkpoint body. Twelve
6.4–7.1-second micros show 11% less loop time, 6–9% less positional-call time and
3–4% less keyword-call time. Sort medians rise with substantial spread and remain
visible as a limitation; empty/pipeline changes are small and mixed. All 9,164
Debug checks pass before timing. No full lanes run.

The [embedded operand-stack round](results/2026-10-10-frame-stack-storage/README.md)
places stack state inside each interpreter and borrows it by reference. Separate
fixed-count diagnostics show about 3.7% less managed call allocation; the change
is integrated as a storage improvement. Twelve 6.4–7.1-second micros retain mixed
call timings, so no reliable throughput gain is established. All 9,164 Debug checks
pass before collection. Retained helper-launcher/declaration errors precede any
allocation worker; the successful helper build is rehashed and reused. No full lanes run.

The [shared frame-owner round](results/2026-10-10-shared-frame-owner/README.md)
combines interpreter and slot state, removing a separate object and duplicate
references. Two fixed-count passes show about 6.3–6.4% less managed call allocation.
The change is integrated for storage savings; timing varies between replicas, so
no reliable throughput gain is established. Twelve paired micros stop in about
eight seconds each, and separate allocation groups in about four seconds, all
under 30-second caps. All 9,164 Debug checks pass before collection. The empty
control's small allocation increase and first-sort variation remain visible.
No full lanes run; milestone qualification stays deferred.

The [context/namespace storage round](results/2026-10-10-context-namespace-owner/README.md)
places namespace-frame storage inside its execution context. Both fixed-count
passes show about 4.1% less managed call allocation; the change is integrated for
storage savings. Call timings are small or mixed, with no reliable throughput
gain. Twelve paired micros stop in about eight seconds each; separate allocation
groups in about four seconds, all under 30-second caps. All 9,165 Debug checks
pass before collection, including concurrent namespace aliases through both views.
Every observation is retained. No full lanes run; qualification remains deferred.

The [shared locals/stack-buffer experiment](results/2026-10-10-shared-frame-buffer/README.md)
remains isolated. It reduces complete-call managed allocation by 4.18–4.29%,
but both replicas show slower positional calls (2.64%/1.99%), keyword calls
(3.55%/1.46%) and integer loops (3.74%/1.35%). Fourteen paired micros include a
prospective stack-growth control and stop in about eight seconds each. Separate
allocation groups stop in about four seconds; all have 30-second caps.
All 9,169 candidate Debug checks and complete CPython outputs pass before timing.
The runtime, tests and harness retain their preceding delivered versions.
No full lanes run; all observations and the rejection remain visible.

The [direct binder-array experiment](results/2026-10-10-binding-plan-arrays/README.md)
remains isolated despite smaller compiled binder methods. Twelve paired micros
show slower positional calls in both replicas and mixed keyword results, with
effectively unchanged call allocation. All 9,165 frozen Debug checks pass first;
normal groups take about eight seconds and allocation groups four, under 30-second
caps. A separate ten-second current-loop CPU capture then identifies dispatch,
stack transfer and integer operations as broader leads; its group stops in
14.08 seconds. Sampling is diagnostic, with no qualified CPU or speed ratio.
Production, tests and harness remain unchanged. No full lanes run.

The [direct local-dispatch round](results/2026-10-10-direct-local-dispatch/README.md)
integrates LoadLocal/StoreLocal into the synchronous main switch with their
original helpers and instruction boundaries. Both basic-loop replicas improve
7.07%/8.40%; call changes are small, other controls mixed and allocation effectively
unchanged. Generated Tier1 main-method code grows from 1,774 to 2,675 bytes;
all native bodies and tiers remain visible. Twelve paired micros take about
eight seconds each, with separate allocation/native groups about four seconds,
all under 30-second caps. All 9,165 frozen Debug checks pass before collection.
No full lanes run; milestone qualification remains pending.

The [plain-jump dispatch experiment](results/2026-10-10-direct-jump-dispatch/README.md)
remains isolated: loop changes -0.76%/-2.38% are inconclusive against their spreads,
positional calls reverse -5.21%/+5.04%, and emitted Tier1 main code grows another
185 bytes. All 9,165 Debug checks pass; twelve paired micros take about eight
seconds each, with separate allocation/native groups about four seconds under
30-second caps. A separate ten-second accepted-runtime CPU capture then records
dispatch, iteration and scalar-value/observation leads; its group stops in 14.08
seconds. No qualified CPU/speed claim, full lane or production change is made.

The [integer-observation round](results/2026-10-10-integer-observation/README.md)
keeps the exact signed-byte memory preflight while routing boxed integers before
generic value checks. Loop time falls 6.53%/5.37% and positional calls 8.59%/9.26%
in two short repeats; other controls are mixed and allocations effectively
unchanged. Thirteen budget regressions pass on the original runtime first, and
all 9,178 Debug checks pass before collection. Twelve micros take about eight
seconds each, with separate allocation/native groups about four seconds under
30-second caps. The final interpreter body stays 2,675 bytes; observation code
grows 103 bytes. Every result remains visible. No full lanes run; qualification
waits for a declared milestone.

The [checked-range iteration round](results/2026-10-10-checked-range-iteration/README.md)
combines range advancement and checked pulls in one active enumerator, preserving
the original checkpoint/ownership boundaries and materializer source shape.
Loop time falls 3.53%/4.50% in two repeats against roughly 1% spreads; call decreases
are small, sort is mixed and pipeline changes are small. Loop managed allocation
increases 8–22 bytes per complete job; final interpreter code grows 9 native bytes.
Eighteen exact boundary regressions pass the unchanged runtime first; all 9,196
Debug checks pass before collection. Twelve micros take about eight seconds each,
with separate allocation/native groups about four seconds under 30-second caps.
Every result and native body/tier remains visible. No full lanes run.

The [direct iterator-dispatch round](results/2026-10-10-direct-for-next-dispatch/README.md)
routes synchronous ForNext through its original operations from the main switch.
Loop time falls 3.50%/3.73% in two repeats against 0.5–0.9% spreads; calls and
pipeline reverse, and full-sort increases remain within their retained spreads.
Allocation is effectively unchanged. Final Tier1 main code grows 24 bytes;
early OSR grows 1,231 bytes. Every body/tier remains visible. A separate
ten-second baseline CPU capture supplies fresh leads, without a before/after
CPU claim. All 9,196 Debug checks pass before twelve micros of about eight
seconds, with separate allocation/native groups about four seconds, each under
a 30-second cap. No full lanes run; milestone qualification remains pending.

The [simple compiled-return improvement](results/2026-10-10-simple-return-functions/README.md)
removes transient interpreter and array allocations from guarded return-only
functions, while retaining their actual context, binder, checkpoints and funding.
Two paired short replicas show 16.6–18.1% less positional-call time and 13.0–20.1%
less keyword-call time, with about 53% less managed job allocation. All controls
remain visible, including an unexplained first-loop increase of 7.61% that does
not repeat in replica two (-0.29%). Native code grows with different emitted
profiles; every body and tier is retained. Twelve collections stop in about eight
seconds each; separate allocation/native groups take about four seconds, all
under 30-second process-group caps including cleanup. No full lane or recollection.
Full frozen Debug passes 9,232 checks and VM Release 204 white/296 public, with
six complete CPython goldens before timing. Milestone qualification remains pending.

The [inline operand-stack experiment](results/2026-10-10-inline-operand-stack/README.md)
remains isolated. It saves about 9.86–10.01% of managed call allocation, but both
repetitions show slower loops (11.99%/15.69%), positional calls (10.19%/6.93%)
and keyword calls (2.12%/8.33%), beyond their spreads. Fifteen independent
growth/reference/disposal checks pass on unchanged production first and are
delivered; all 9,211 candidate Debug checks pass before collection. Two separate
current call profiles supply frame/allocation leads without a savings forecast.
Twelve micros take about eight seconds each, allocation/native groups four,
under 30-second caps. All observations and native bodies/tiers remain visible.
No full lane runs; production keeps the preceding implementation.

The [context-parent consolidation](results/2026-10-10-context-parent-link/README.md)
derives ancestry from the existing frame link while retaining real function
contexts, namespaces, lexical services and containment checks. Nine independent
constructor/namespace/nonlocal checks pass original production first. Complete-call
managed allocation falls about 3.0–3.1%; keyword time falls 2.5–5.6% in both short
replicas, while positional time stays within its spreads. The loop +3.91%/-1.43%
reversal and all sort/pipeline controls remain visible without speedup claims.
The checked getter grows from 5 to 39 native bytes; every body/tier is retained.
Full Debug passes 9,241 checks and VM Release 213 white/296 public with six full
CPython goldens before timing. Twelve collections take about eight seconds each,
with separate allocation/native groups about four seconds under 30-second caps.
No full lanes run. The report also clarifies unresolved keyword allocation types
in the preceding CPU trace; complete-job byte measurements support its saving.

The [module-function discovery improvement](results/2026-10-10-module-super-discovery/README.md)
skips implicit-super receiver/class-cell discovery for unowned module-root functions,
retaining owner binding and nested/live class-cell barriers. Eighteen independent
checks pass unchanged production first. Keyword calls take 8.8–10.2% less time in
both short replicas, beyond their spreads; the first positional result overlaps
its spread, so no repeatable positional gain is claimed. All sort/control changes
and the first-pass sort allocation increase remain visible. Full Debug passes
9,259 tests; VM Release 228 white/329 public and six complete CPython cases pass
before timing. Twelve micros take about eight seconds each; separate allocation/
native groups about four, all under 30-second caps. No full lane or qualified
Python multiplier; all emitted native bodies/tiers stay retained.

The [unused super-anchor experiment](results/2026-10-10-super-anchor-state/README.md)
keeps its production change isolated. Positional/keyword allocation falls about
3.1%, but owned-method allocation falls only 0.46%, below the prospective 0.5%
floor required across all three call cases. Positional timing improves 2.8–3.3%;
keyword/method timing has no repeated benefit. Every control remains visible,
including opposite sort changes. Thirteen independent funding/parity checks are
retained; all 58 selected checks pass original production first. The candidate
passes 9,272 Debug tests, VM Release 239 white/331 public and seven canonical
CPython outputs before fourteen micros taking 8.04–8.06 seconds each. Separate
allocation/native groups take about four seconds, all under 30-second caps.
No full lanes run and no observations are recollected to chase acceptance.

The [method receiver lease experiment](results/2026-10-10-method-receiver-lease/README.md)
also stays isolated. Method allocation savings are only 35.76 / 180.16 bytes per
complete job, below the declared 0.5% floor; method timing changes +0.72% / -5.62%,
while positional calls regress +2.82% / +3.43% beyond both retained spreads.
Twenty-nine regression checks remain integrated: 87 selected checks pass original
production first, followed by 9,301 candidate Debug tests, VM Release 256 white /
343 public and seven complete CPython outputs. Fourteen micros take about eight
seconds each, with separate allocation/native groups and no full lanes. Follow-up
accepted-runtime method traces take about sixteen seconds each and identify
attribute handling and shared async lowered dispatch as further design leads.
Their overlapping inclusive stacks do not predict removable costs.

The [lowered-dispatch compatibility baseline](results/2026-10-10-lowered-dispatch-baseline/README.md)
fixes false static rejections of local lazy type aliases and later reads of
captured collections. It retains 34 regression checks, passes 9,335 Debug tests
and matches twenty trusted CPython snippets in both run modes.

The [direct synchronous lowered-dispatch experiment](results/2026-10-10-lowered-sync-dispatch/README.md)
passes 9,335 Debug tests and fresh Release/CPython gates for both producers.
Fourteen paired micros take about eight seconds each. Method jobs improve
4.8–6.6%, but the loop control repeatedly regresses 2.6–3.2%, failing the frozen
acceptance rule. The candidate stays isolated. Separate allocation/native
diagnostics retain all observations; no full lane or recollection runs.

The [default-object attribute compatibility baseline](results/2026-10-10-default-object-attribute-baseline/README.md)
corrects member-assignment order, lowered async target stores and descriptor
binding for mutation slots. Its 33 independent checks reproduce nine original
runtime failures. Fixed baseline and isolated direct-slot candidate each pass
9,368 Debug tests and 58 CPython comparisons. At that baseline's delivery, fresh
Release gates and separately declared short micros were pending; no timing or
full lanes ran.

The [exact default object-slot round](results/2026-10-10-default-object-attribute-slots/README.md)
integrates direct descriptor-aware handling after method jobs improve 31.0% /
28.3% and process-wide complete-job allocation drops 48.4% in both passes.
Both fresh Release producers pass 276 white/390 public checks and seven canonical
CPython comparisons before fourteen paired micros taking 8.04–8.06 seconds each.
All controls remain visible: positional calls change +4.39%/-3.67%, while full
sort/output changes +6.62%/+3.09% within 12.1%/18.9% retained maximum spreads.
The unchanged prospective repeated-regression rule passes. Separate allocation
and native-code groups take about four seconds; all collections have 30-second
external caps. No full lanes, recollection or milestone qualification.

The [current method CPU refresh and scoped-dispatch baseline](results/2026-10-11-function-scoped-dispatch-baseline/README.md)
uses two 10-second captures on accepted attribute-slot production, with whole
groups taking 14.07/16.08 seconds. Shared async statement dispatch remains in
33.5–35.6% of inclusive sampled stacks; these overlapping shares are design leads,
not predicted savings. Twenty-seven independent invocation checks precede an
isolated function-body strategy. Baseline and candidate each pass 9,395 Debug
tests, 181 selected checks and 58 CPython comparisons. Fresh Release gates and
short candidate micros remain pending; no gain or integration is claimed.

The [stored-root and value-transfer checkpoint](results/2026-10-13-value-transfers/README.md)
records the combined frame/code descriptors and operand publication at native
callbacks, full Release/stress/package checks and one inconclusive short cost
comparison. No logical reclamation or forced-GC removal is enabled. The
[immutable scalar.44 assessment](results/2026-10-13-regex-scalar-44/README.md)
records the remaining managed-only backend blocker separately.

The [zip construction and row continuation](results/2026-10-14-zip-state/README.md)
records the demonstrated cursor admission gap, focused publication/denial/alias
controls, removal of a redundant row copy, and one inconclusive cost comparison.
The higher candidate median is retained; no gain or regression is established.
Full/platform/package qualification is deferred to the next coherent checkpoint.


## Governed worker ownership and complete CSV field runs (2026-10-14)

Worker lifetime cleanup is accepted at f95fb734: a shared unbounded allowance,
previous-result/per-call compilation disposal, final response borrowing and cached
compilation cleanup are part of the recorded boundary. 190 focused controls pass,
with 12 local and five VM representative cases matching isolated UTF-8 CPython.

One ten-second kernel CPU profile locates dictionary CSV iteration/parser work;
one 8.725s paired diagnostic tests unquoted field runs. The candidate preserves
capacity admission and Python results, but its nominal 2.2% lower median is smaller
than 15.4% baseline spread. It is rejected and its runtime branch is removed.
The useful funded-capacity denial/resumption regression remains. This bounded
experiment stops here, without replacement samples, a full lane or a speed claim.
[Complete evidence, caveats and rejected patch](results/2026-10-14-representative-csv/README.md)
keeps CPython as the primary external baseline.


## Complete JSON loads: remove temporary decoder machinery (2026-10-14)

The existing option binder now returns scanner options for both explicit decoders
and exact builtin load/loads. Module loads avoid temporary decoder attributes,
constant callable and bound method/argument binding; custom classes still construct
and dispatch. The same parser, validation, hooks and safety guards remain.

One CPU profile supplies the construction/binding lead. One short complete-job
comparison is inconclusive (3.7% nominal change, 11.8% candidate spread). A separate
reused managed-allocation helper measures 13.790MB versus9.923MB per exact job,
28.0% lower, with ordinary CLR GC and deterministic shared-budget cleanup. Retain
for measured allocation reduction and simpler runtime work; make no elapsed-time
or embedding-latency claim. No timing repeat or new parser/cache/framework.
[Identities, all results and qualification](results/2026-10-14-representative-json/README.md)
preserve the distinct CPython and earlier-Lython comparisons.


## Diagnostic comparisons by revision

Preserved from BENCHMARKS.md before reducing its leading table. Revision and
measurement-status labels retain their original meaning; see the
[measurement identities](../BENCHMARKS.md#which-code-and-machine-were-measured).

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Stable sort with lambda key [AL], warm `Run` | 2,048 tuples; build, stable-sort and print | 1.595 ms | 0.3020 ms | — | One diagnostic; observed 10.2% lower Lython median; executable lambda bodies; comprehension unchanged |
| Expanded-call forwarding helper [AK], warm `Run` | 2,048 calls; tuple and keyword mapping | 2.077 ms | 0.2443 ms | — | One diagnostic; observed 13.9% lower Lython median; fallback/mirroring removed; general binder remains |
| Formatted report helper [AJ], warm `Run` | 2,048 calls; padded row and computed next value | 2.125 ms | 0.4201 ms | — | One diagnostic; observed 13.4% lower Lython median; ordinary f-string fallback removed |
| Expanded set/dictionary helper [AI], warm `Run` | 2,048 calls; mixed literal/expanded entries | 4.963 ms | 0.6326 ms | — | One diagnostic; observed 8.8% lower Lython median; fallbacks/mirroring removed |
| Ordinary starred-tuple helper [AH], warm `Run` | 2,048 calls; six-element immutable results | 2.629 ms | 0.3477 ms | — | One diagnostic; elapsed change within spread; fallback removed, conservative overlap funded |
| Ordinary starred-list helper [AG], warm `Run` | 2,048 calls; six-element merged lists | 2.179 ms | 0.3541 ms | — | One diagnostic; observed 18.2% lower Lython median; fallback/mirroring removed |
| JSON configuration read/edit/write [AF], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.817-3.142 ms | 1.572 ms | — | Two identical Lython controls differ by 11.5%; one diagnostic; no qualified ratio |
| Branch/integer loop [AE], warm `RunAsync` | 16,384 iterations; modulo/comparison/add/subtract | 6.321 ms | 0.9599 ms | — | One diagnostic; observed 35.8% lower Lython median; remaining gap substantial |
| Complete copy/checksum/manifest job [AD], warm | Two files / 4,107 input bytes | 6.080 ms | 0.4382 ms | — | One diagnostic; difference within spread; byte-allocation removal only |
| Complete JSON-lines aggregation [AC], warm | 4,096 records / 110,592 UTF-8 bytes | 9.692 ms | 4.260 ms | — | One diagnostic; elapsed change within spread; redundant wrapper removed |
| Complete uppercase-report file job [AB], warm | 1,024 lines / 23,552 UTF-8 bytes | 2.651 ms | 0.2444 ms | — | One diagnostic; elapsed change within spread; allocation removal only |
| Complete text-summary file job [AA], warm | 3,072 lines / 35,840 UTF-8 bytes | 6.553 ms | 1.231 ms | — | One diagnostic; observed improvement near baseline spread; no qualified ratio |
| Default-argument helper [Z], fresh compile/run | 256 calls | 0.186 ms | 0.0373 ms | — | Historical diagnostic at 3b34fad2; sharing prototype was slower and removed; final cleanup unmeasured |
| Split lines, strip and sum lengths [X], warm `RunAsync` | 32,768 lines / 425,984 UTF-8 bytes | 23.464 ms | 3.132 ms | — | One default-strip diagnostic on 88e0992b; inconclusive, baseline/candidate spread 16.5%/23.3% |
| Unicode string-key word counts [W], warm `RunAsync` | 16,384 updates / four keys; completed host operations | 15.146 ms | 1.313 ms | — | One direct-get diagnostic on 7e2c1f27; observed 16.0% lower Lython median; remaining gap substantial |
| Append to distinct lists and total values [N], warm `RunAsync` | 16,384 lists/appends; completed host operations | 7.819 ms | 1.132 ms | — | One direct-append diagnostic on 99e8701f; observed 21.8% lower Lython median; no qualified speedup |
| Full-body parameter-update helper [B], warm `RunAsync` | 16,384 calls; completed host operations | 10.372 ms | 1.287 ms | — | One matching-local-storage diagnostic on f93e9258; observed 3.6% lower Lython median; no qualified speedup |
| Keyword helper [K], warm `Run` | 16,384 calls | 4.262 ms | 1.174 ms | — | One call-cache deletion diagnostic on 6262ea94; difference within spread; no timing claim |
| Helper using positional/keyword defaults [V], warm `RunAsync` | 16,384 calls; completed host operations | 9.555 ms | 1.258 ms | — | One correctness/admission diagnostic on 96596466; candidate median 4.2% higher; spreads 3.0%/3.9% |
| Tuple-key dictionary updates [D], warm `Run` | 16,384 updates | 30.573 ms | 2.418 ms | — | One method-identity diagnostic on 0eb587a9; difference from earlier Lython is within spread |
| Integer reduction loop [I], warm `RunAsync` | 16,384 iterations; completed host operations | 2.660 ms | 0.579 ms | — | One diagnostic on 88f78106; 14.4% lower median than earlier async Lython; candidate spread 6.0% |
| Integer helper calls [E], warm `Run` | 16,384 calls | 3.289 ms | 0.965 ms | — | One candidate diagnostic on 015184e6; sync change within spread |
| Same helper calls [R], warm `RunAsync` | Same input; completed host operations | 5.378 ms | 1.001 ms | — | One positional-transport diagnostic on 1f2ef306; 13.7% lower Lython median; baseline spread 9.4% |
| Helper with nested filtered list comprehension [H] | 1,024 inputs / 1,364 results | 0.816–0.825 ms | 0.0793–0.0990 ms | — | Warm diagnostics at 3cad4157; both executable candidates rejected; CPython spread 23.7–38.2% |
| File search with helper and protected early exit [P], warm | 2,049 lines / 42,010 UTF-8 bytes | 4.249 ms | 0.265 ms | — | One candidate diagnostic; inconclusive (56.4% spread) |
| Same file search [P], fresh compile and run | Same input; stop after line 1,025 | 6.041 ms | 0.395 ms | — | Fresh code per job; inconclusive (42.4% spread); startup excluded |
| Complete file copy/checksum manifest [F] | 2 files / 4,107 bytes | 6.103–6.254 ms | 0.442 ms | — | One warm diagnostic; two identical Lython controls, up to15.1% spread |
| Complete Unicode text summary [T] | 6,144 words / 35,840 UTF-8 bytes | 8.018 ms | 1.258 ms | — | One warm diagnostic at ca432c22; noisy ASCII candidate rejected |
| Complete JSON-lines summary [J] | 4,096 records / 110,592 UTF-8 bytes | 11.537 ms | 4.225 ms | — | One warm candidate diagnostic; timing inconclusive (11.8% spread), allocation reduction measured |
| Complete CSV summary [C] | 4,096 records / 57,361 UTF-8 bytes | 7.530 ms | 3.483 ms | — | One warm diagnostic at f95fb734; inconclusive baseline spread (15.4%) |
| CSV parse and discard [G] | 16 data rows | 0.0780 ms | 0.0285 ms | — | One short governed-memory diagnostic; uncommitted source, unqualified |
| Sum generated squares [A] | 20,000 items | 3.947 ms | 0.499 ms | — | One short diagnostic at 09d81bb1 source; no qualified multiplier |
| Integer reduction loop [M] | 16,384 iterations | 1.162 ms | 0.617 ms | — | Short diagnostic at b43664e0; one run, no qualified multiplier |
| Integer reduction loop [L] | 16,384 iterations | 3.58–3.72 ms | 0.578–0.666 ms | — | Historical diagnostic; failed control and cross-session stability |
| Keyword calls [Q] | 2,048 calls | 1.378–1.404 ms | 0.137–0.145 ms | — | Historical diagnostic; failed invocation control |
| Stable sort and output [Q] | 2,048 items | 5.150–7.889 ms | 0.306–0.313 ms | — | Historical diagnostic; spread/order checks also failed |
| ASCII text pipeline [Q] | 1,024 eleven-character blocks | 0.618 ms | 0.0415 ms | — | Historical diagnostic; only one session before idle gate failed |
| JSON transform and round trip [Q] | 128 records | 0.674–0.741 ms | 0.0610–0.0618 ms | — | Historical diagnostic; control floor/spread/interval checks failed |
| CSV parse and retain [Q] | 128 data rows | 1.037–1.185 ms | 0.102–0.108 ms | — | Historical diagnostic; control/spread/order/interval checks failed |
| Other CSV summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other Unicode text summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other JSON-lines summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other file copy/checksum manifest sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |


### Archived qualification notes

**Ratio convention:** Lython elapsed time divided by CPython elapsed time;
above 1 means Lython takes longer. A dash means unavailable or unqualified,
never zero. The historical generated reports use the inverse convention;
their ratios must not be copied into this column. Ranges above are the smallest
and largest retained **session medians**, rounded for readability, not confidence
intervals. The ASCII row has just one retained median.

The historical measurements show substantial performance gaps on these jobs,
including a loop whose cost grows with its iteration count. They support
investigating interpreter dispatch and application workloads. **No workload
multiplier qualified in either campaign. These timings do not describe today's
HEAD:** subsequent interpreter and containment changes have been integrated.
The newer AL, AK, AJ, AI, AH, AG, AF, AE, AD, AC, AB, AA, Z, X, W, N, B, K, R, V, D, I, E, H, P, F, T, J, C, G, A and M rows are separate short diagnostics at their named revisions; they do
not qualify a current overall or complete-script multiplier.
The F, T, J and C rows supply complete-script comparisons; their timing evidence
remains inconclusive. F is a range of medians from two identical Lython control
processes, not two revisions. No complete-script multiplier or overall speedup is qualified.


### Archived measurement identities

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
| AI | 2026-10-15 | Working expanded set/dict candidate on28c41f1d; two frozen compiler sources; both roles contain entry-order fix | [Executable displays, mapping suspension and order qualification](results/2026-10-15-expanded-set-dict-executable/README.md) |
| AJ | 2026-10-15 | Working f-string candidate on 0c367762; one frozen compiler file, shared report fixture | [Executable fields, async protocols and ownership qualification](results/2026-10-15-executable-fstrings/README.md) |
| AK | 2026-10-15 | Working expanded-call candidate on 42dfd774; two frozen compiler files; both roles contain argument-order fix | [Executable expansion, argument effects and ownership qualification](results/2026-10-15-executable-expanded-calls/README.md) |
| AL | 2026-10-15 | Working lambda-body candidate on 24e6442a; eleven frozen production files; both roles share function runtime | [Executable bodies, deleted callable and scope/lifetime qualification](results/2026-10-15-executable-lambdas/README.md) |
| AH | 2026-10-15 | Working tuple-lowering candidate on5cf03da9; one frozen compiler source; both roles include empty-tuple fix | [Immutable conversion, admission and identity checks](results/2026-10-15-starred-tuple-executable/README.md) |
| AG | 2026-10-15 | Working starred-list lowering candidate on8f290b57; two frozen compiler sources; identical helper/tooling | [Executable displays, ordered effects and ownership checks](results/2026-10-15-starred-list-executable/README.md) |
| AF | 2026-10-15 | 654b4f5a; two identical Lython controls, freshly compiled configuration job | [Compile-to-result boundary, controls and independent file goldens](results/2026-10-15-config-fresh/README.md) |
| AE | 2026-10-15 | Working exact-integer dispatch candidate on 60277f65; one frozen interpreter source; unchanged case/tooling | [Async integer operators, suspension and boundary checks](results/2026-10-15-async-integer-operators/README.md) |
| AD | 2026-10-15 | Working exact-bytes sum candidate on 64fecd53; one frozen source; unchanged script/tooling | [Complete manifest job, exact sums and ownership/preflight checks](results/2026-10-15-bytes-sum/README.md) |
| AC | 2026-10-15 | Working JSON value-forwarding candidate on 50583327; one frozen production source; unchanged script/tooling | [Complete JSON file job and callback/ownership checks](results/2026-10-15-json-value-dispatch/README.md) |
| AB | 2026-10-15 | Working UTF-8 upper candidate on 29a1862e; one frozen source; identical new report script/catalog in both roles | [Complete report, noise and expansion/ownership checks](results/2026-10-15-utf8-upper/README.md) |
| AA | 2026-10-15 | Working UTF-8 lower candidate on d083c8cf; two frozen production files; unchanged script/tooling | [Complete text job, noise and allocation/containment checks](results/2026-10-15-utf8-lower/README.md) |
| Z | 2026-10-15 | Baseline 3b34fad2; ten-source compiled-layout candidate rejected and removed | [Fresh helper timing, rejection and retained cleanup](results/2026-10-15-parameter-layout-rejected/README.md) |
| X | 2026-10-15 | Working direct-strip/trim-identity candidate on 88e0992b; seven performance files frozen; parser fix shared by both roles | [Inconclusive text timing, allocation removal and Unicode diagnostics](results/2026-10-15-direct-string-strip/README.md) |
| W | 2026-10-15 | Working direct-get candidate on 7e2c1f27; seven production files frozen by hashes; identical new word-count case for both roles | [Direct lookup, callback lifetimes and short diagnostic](results/2026-10-15-direct-dict-get/README.md) |
| N | 2026-10-15 | Working direct-list-append candidate on 99e8701f; nine production files frozen by hashes; identical new distinct-list case for both roles | [Direct call transport, keyword correction and validation](results/2026-10-15-direct-append/README.md) |
| B | 2026-10-15 | Working matching-local-storage candidate on f93e9258; three production files frozen by hashes; identical new full-body case for both roles | [Local-array removal and lifetime validation](results/2026-10-15-bound-local-reuse/README.md) |
| K | 2026-10-15 | Working call-cache deletion on 6262ea94; eight production files frozen by hashes; comparison tooling unchanged | [Mechanism removal and keyword-call diagnostic](results/2026-10-15-call-cache-deletion/README.md) |
| R | 2026-10-15 | Working direct-positional candidate on 1f2ef306; eight production files frozen by hashes; comparison tooling unchanged | [Operand transport, boundary tests and short diagnostic](results/2026-10-15-positional-calls/README.md) |
| V | 2026-10-15 | Working live-defaults candidate on 96596466; thirteen production files frozen by hashes; one catalog case shared by both roles | [Live defaults, ownership and CPython lifetime edge](results/2026-10-15-function-defaults/README.md) |
| D | 2026-10-15 | Working method-exposure candidate on 0eb587a9; seven production files frozen by hashes | [Correct method identity and retained immediate-call cache](results/2026-10-15-method-exposure/README.md) |
| I | 2026-10-15 | Working async integer-addition candidate on 88f78106; two production files frozen by hashes | [Async loop diagnostic and yielding-protocol correction](results/2026-10-15-async-integer-dispatch/README.md) |
| E | 2026-10-15 | Working async-function candidate on 015184e6; six production files frozen by hashes | [Separate `Run` / `RunAsync` diagnostics and qualification](results/2026-10-15-async-compiled-functions/README.md) |
| H | 2026-10-15 | 3cad4157 existing runtime; both uncommitted executable-list candidates rejected | [Helper comparison and retained prototypes](results/2026-10-15-list-comprehensions-rejected/README.md) |
| P | 2026-10-15 | Working protected-loop candidate on b2235259; two frontend source changes frozen by hashes | [Separate warm and compile/run diagnostics](results/2026-10-15-protected-loop-jumps/README.md), [source declaration](results/2026-10-15-protected-loop-jumps/comparison-declaration.json) |
| F | 2026-10-14 | ca432c22, two identical worker/binary controls; runtime tree matches7736b6c0 | [Complete file diagnostic](results/2026-10-14-representative-file/README.md), [loaded identities](results/2026-10-14-representative-file/micro.json) |
| T | 2026-10-14 | ca432c22, governed runtime and worker ownership; ASCII candidate rejected | [Complete text diagnostic](results/2026-10-14-representative-text/README.md), [loaded identities](results/2026-10-14-representative-text/micro.json) |
| J | 2026-10-14 | Working direct-decoder candidate on 64ec1fd4, with identical f95fb734 worker | [Complete JSON diagnostic](results/2026-10-14-representative-json/README.md), [timing identities](results/2026-10-14-representative-json/micro.json) |
| C | 2026-10-14 | f95fb734, governed runtime and worker ownership; parser candidate rejected | [Complete CSV diagnostic](results/2026-10-14-representative-csv/README.md), [loaded identities](results/2026-10-14-representative-csv/micro.json) |
| G | 2026-10-14 | Working migration on `9fc7e37e`, before local-import simplification | [Governed-memory diagnostic](results/2026-10-14-governed-memory/README.md), [loaded identities](results/2026-10-14-governed-memory/micro.json) |
| A | 2026-10-14 | `09d81bb153c006f3c248d8ba0dfdfe9cdbc70d63` source, built before commit | [Native state qualification](results/2026-10-14-native-working-state/README.md), [all attempts and loaded identities](results/2026-10-14-native-working-state/micro.json) |
| M | 2026-10-13 | `b43664e0f60aa78f3cbd638578e0ced28d926194` | [Shared frames and transfer cost](results/2026-10-13-shared-async-frames/README.md), [all attempts](results/2026-10-13-shared-async-frames/micro.json) |
| Q | 2026-10-09 | `9358c4e72f1a29e55703eee02b28ea404d01bc0a` | [Four-lane run](results/2026-10-09-quick/README.md), [warm medians and exclusions](results/2026-10-09-quick/warm.md) |
| L | 2026-10-09 | `56d4346d960b58f037da7d4590eb66c87ffec29e` | [Focused loop run](results/2026-10-09-core-loop/README.md), [all loop samples](results/2026-10-09-core-loop/warm.md) |
| S | 2026-10-12 | `87c1a94c347a88bc94927ea603a138cec4376170` (correctness, no timings) | [Complete-script validation](results/2026-10-12-script-negative-cases/README.md), [fixture and host contract](results/2026-10-12-containment-scope/README.md) |

### Additional machine and control identities

A ran on the shared Windows 11 x64 development workstation: i7-14700KF,
28 logical processors, 64 GiB RAM; SDK 10.0.300-preview.0.26177.108,
.NET 10.0.13 and CPython 3.13.2, ordinary GIL/GC, launched with `-I -S`.
Its 7.893-second owned group uses the existing generated-sum workload with
shared printed output. The candidate library's informational metadata names
the preceding commit; frozen hashes identify the added sum implementation.
Compilation and startup are excluded. This diagnostic does not qualify a
CPython multiplier or an improvement over earlier Lython.
M ran on that dedicated four-vCPU Ubuntu VM under .NET 10.0.12 with the same
CPython 3.13.16 reference. Its portable Windows Release libraries were built
using SDK **10.0.300-preview.0.26177.108**; its identical benchmark observer and
DLL hashes are frozen separately. Seven rounds in one 8.24-second owned group
provide diagnostic context, with compilation/startup outside the warm timer.

S verifies exact stdout and output-file bytes through Lython's public API in
both synchronous and genuinely yielded asynchronous execution, plus independent
CPython 3.13.2 checks on Windows. Its fixture repeats expand into different record/byte counts for
each script; they are not a shared row count. Correctness tests are not timing
measurements. The [original scripts](scripts) remain ordinary Python.

G uses the same shared Windows workstation/toolchain as A and CPython3.13.2.
Its warm CSV comparison excludes compilation/startup and is a single7.320-second
diagnostic; later local-import changes are unmeasured.
