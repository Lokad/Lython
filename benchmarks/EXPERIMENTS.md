# Benchmark experimental history

Historical records preserved on 2026-10-12. Revision-specific findings below
must not be read as measurements of current HEAD. See [BENCHMARKS.md](../BENCHMARKS.md)
for the authoritative comparison and [COMPARISON.md](COMPARISON.md) for methods.
The original narratives and evidence links follow unchanged.

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
