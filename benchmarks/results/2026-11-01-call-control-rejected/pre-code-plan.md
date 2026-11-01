# Lokad.Lython 0.11.0 — active plan

Updated **2026-11-01** (Paris). Two independent tracks. Missing producer APIs gate
only final optional-adapter qualification; they never stop core development,
correctness tests or performance investigation. No publication/tag is authorized.
PLAN.md intentionally remains untracked.

## Current checkpoint

Runtime **95b5955c** is DELIVERED: terminal green **41296168049**, Windows/Ubuntu,
ordinary and original stress scales on both .NET targets, plus Release installed
package consumers. Terminal result cached; never query again. Local full ordinary
3,962 native/15,527 public per actual.NET11RC3/10.0.8 and package consumers pass.
Canonical unpublished packages identify95b5955c. Published0.10 hashes/tag unchanged.
B59/B60 now change frontend/runtime production source and retain45 new public regressions,
in addition to35 post-delivery JSON/annotation controls. Fresh nonincremental
core/worker/test/probe copies agreed at the frozen comparison;219 native/1,294 public
focused checks pass on actual.NET11RC3/10.0.8. B59/B60 are locally retained; their new
full Windows/Ubuntu ordinary/original stress/package checkpoint is next.
Historical rejected prototypes are absent. Do not repeat unchanged terminal runs
or imply canonical95b5955c packages describe the newer frontend source.

## Lasting constraints

Fully managed/memory-safe C# and managed stack admission. Preserve host mediation,
current CancellationToken, terminal first-origin admission refusal, substantial
capacity/replacement preflights, retained aliases and awaited actual users/cleanup.
Borrow the host allowance through existing owners; scope creation reserves nothing.
No unsafe/Unsafe/pointers/PInvoke/native probes, fuel/quotas/watchdogs, hidden caps
or allowances, heap tracing, forced GC, per-object ledgers or new owner infrastructure.
AGENTS.md and docs/RUNTIME.md specify the current contracts. Guest work never resumes
after refused admission; guest-raised MemoryError remains catchable.

## Track A — core corrections and final optional-adapter qualification

Core provider boundary is DELIVERED (B59/B60 corrections below await the new full checkpoint): Python-specific native UTF-8 contract, byte ranges/
scalar positions, original windows, shared replacement/split/expand, explicit
funding and current tokens across frontend analysis, runtime, imports and helpers.
One borrowed provider; no backend types/dependency/dialect substitution in core.
Contract tests, Python semantics and actual backend performance remain distinct.

Ordinary non-packable adapter stays on PythonRe0.4.0. Completed A04/A05 development
qualification uses exact private0.5.0-dev.1, producer
0e24cecebbb9093a18f5cb962dae3b6f6426742c, PythonRe SHA256
`e4136eddffdca5da0b9e5733cd9534053d7d99880714669b3695be5305ed7019`.
Do not requalify unchanged artifacts or poll package indices. No sibling edits.

Only final pin/qualification waits for a new immutable released package:

| Capability still missing/unqualified | Acceptance and temporary boundary |
| --- | --- |
| Native capture names | Remove adapter-only GetGroupNames encoding bridge; preserve effective/reported flags and metadata. |
| One-shot ranges | Direct match/search/fullmatch ranges and working-funding receipt. Current API lacks overloads; conservative snapshots stay outside core. |
| Reversed windows | Qualify pos>endpos zero-width semantics with original anchors/prefix/lookbehind. Core/test provider supports them; current adapter rejects explicitly. |
| Programs/cursors | Qualify funded construction/current-token moves, independent borrows, disposal after users and actual in-flight interruption. |

When supplied: existing dependency/semantic/lifetime/refusal/reuse checks, isolated
real-cancellation probes, producer managed-safety assessment and one focused backend
comparison against the new pin. Temporary development adapter/test-provider bridges
are acceptable, never core plumbing or backend-performance evidence. No new memory
framework. A newer release cannot block Track B.

**B59 IMPLEMENTED, DELIVERY WITH B60:** remove267-line legacy regex binding
pass and duplicate flag-expression/syntactic checks; use existing typed flow and
small integer bit-mask facts. Unrelated `re` receivers work; raw syntax belongs to
the provider, LA3044 removed, non-raw string escapes unchanged.12 new public controls
(10 fail original), eight isolated Python goldens.130 native/756 public focused
checks pass per actual target. One36.752s valid comparison: compilation-2.05%/-3.45%
CSV,-3.60%/-3.35% text; warm CSV-0.16%/+0.18%. No qualified speedup. PatternError metadata correction is implemented in B60 below, independent of a producer. [Exact source, failures,
criteria and results](benchmarks/results/2026-11-01-typed-regex-analysis/README.md).

## B60 — implemented; coherent B59/B60 delivery next

Python-compatible regex error fields/args/constructor and original pattern aliases
are implemented through existing governed storage; optional0.4 adapter normalizes
UTF-16 error positions on failure. No backend dependency or new error/owner framework.
Twenty-one isolated CPython goldens and12 actual provider/suspension lifetime checks
pass, including cancellation/terminal refusal, funded retained results, simultaneous
shared-budget executions, awaited users and reuse. Focused Release219 native/1,294
public per actual.NET11RC3/10.0.8 pass. The existing native invalid-compile test now
requires retained metadata funding until owner cleanup, zero temporary reservations
and final zero balance; it no longer requires refunding a live error payload.

One18s bounded comparison: unaffected CSV-2.58%/-0.95%, valid controls/no material
regression. Error construction raw+14.72%/+11.08% is INCONCLUSIVE:24.91–29ms batches
fail>100ms and original drift3.27% exceeds3%. Keep every adverse sample; no retiming
or speedup claim. [Exact criteria, source, failures and outcomes](benchmarks/results/2026-11-01-pattern-error-metadata/README.md).

Delivery next: fresh Release solution; full ordinary Windows both actual targets;
manual Windows/Ubuntu ordinary/original stress both targets and explicit installed
package consumers. Do not repeat95b5955c's unchanged terminal qualification. Update
current contracts/changelog, archive focused evidence, commit/push and keep working
on independent Track B while the new CI runs. No publication/tag. B60 closes after
this delivery; no general exception/enum/regex redesign expansion.

## Track B — next independent performance deliverable

**B58 CLOSED, MATERIAL CSV SOURCE EFFECT:** same frozen current-production bits,
one23.108s service; CSV expanded helper expression-16.31%/-17.88%, controls+1.92%;
text-13.95%/-13.59%, controls-0.41%. All prospective gates pass, four isolated
Python/full-file goldens, zero balances/quiet/cleanup. No runtime speedup or removable
fraction. Existing binder already avoids a second package; lexical slot identity
prevents blindly reusing interpreters. A saved-call-record design would need stable
slot views, full control/cleanup state and funded generator lifetimes; not yet proven.
No automatic pool/shared-buffer/value rewrite. [Source forms, all evidence and decision](benchmarks/results/2026-11-01-helper-cost-assessment/README.md).

### B61 — bounded call-control redesign, before code

Hypothesis: B58's16–18% CSV/13–14% text source effect includes avoidable per-call
control-object construction. Existing direct binding already consumes final
local storage; do not add a binder or combine/replay local/operand buffers. Keep
stable per-invocation lexical contexts, local/cell views and generator state.
Allocation percentages alone are not CPU attribution; this is one falsifiable
runtime trial, not a predicted speedup.

Concrete replacement: retain one instruction driver per public/native-callback
entry or generator. Ordinary executable calls save caller control into one
capacity-admitted value-record array instead of allocating another large driver
and linking _caller. Existing small ExecutableFrameState views keep paused lexical
locals/cells stable. Remove child-driver construction/object-switching and the
_caller chain. Keep direct driver fields for active dispatch, existing sync/async
loops, binding, stacks, function contexts, slot/cell semantics and protocol routes.
Save ALL pending calls, exceptions, block/instruction indices, returns, generator
and async-disposal state; no pool, shared locals buffer, lazy lexical context,
shared-loop replay, interface framework or alternate backend.

Use the existing governor for conservative record-array capacity/replacement
admission (512 bytes/record plus array metadata). Clear popped records; keep old
capacity funded through replacement and actual users. Ordinary entry cleanup
releases records after unwind; generator capacity stays funded through suspension
until actual completion. Frame views are admitted before allocation. Operand
stacks retain the active driver's source location without aliasing a paused
parent's locals. No new budget, owner, tracing, counters or configuration.
Earlier rejection from admitted capacity is an intentional documented tradeoff.
No refund of retained storage, cancellation shortcut or caught-admission recovery.

Acceptance before timing: existing direct/named/bound calls, defaults, recursion,
tracebacks, nested callbacks, super/locals/globals/nonlocals/closures and inline
comprehensions; generators send/throw/yield-from and pending async iterator cleanup;
synchronous and genuinely suspended asynchronous host protocols; cancellation,
first-origin refusal before effects, retained aliases/results, shared budget reuse
and zero cleanup balances. Preserve managed stack admissions, operand copies and
awaited users. Add a regression only for a real observable coverage gap. Focused
Release on both actual runtimes; no private field/count tests or artificial fuel.

One<=60s before/candidate/original-control service, normal GC/tiering and identical
auxiliaries/shared CoreLib/hash witnesses. Complete warm maintained CSV helpers,
text helpers and unaffected JSON processing;128 interleaved warmups/image,2s
settle/case, seven rotating32-job batches. Full fixture/I/O/output/file goldens,
result disposal, zero balances, quiet gates and owned cleanup remain inside their
existing boundaries. Both original medians within3%, IQR<=10%, batches>100ms.
Retain for performance only if BOTH affected scripts improve>=7.5% against BOTH
originals and unaffected JSON regresses<=3%; no phase subtraction/CPython ratio.
Preserve every adverse/inconclusive sample. One candidate, no favorable retiming,
threshold edits, profiling campaign or marginal follow-up tuning. If semantics or
these fixed gates fail, restore original production source, fresh-build/test it,
retain independent useful regressions/evidence and stop this redesign.

Full Windows/Ubuntu ordinary/original stress/package checks apply only if retained
at the next coherent delivery. B59/B60's current full checkpoint continues
independently; no repeated historical qualification or producer polling.

## Recent decisions

| Item | Outcome and immutable evidence |
| --- | --- |
| B49 CLOSED | Ordinary named calls already consume operands into shared binding/local storage. One47.336s comparison: CSV CPU-4.67%/-5.71%, text+2.53%/+4.28%; no >=10% gap. CPython semantics and controls pass. No second binder. [Evidence](benchmarks/results/2026-11-01-keyword-call-assessment/README.md). |
| B50 CLOSED | CSV RunAsync CPU+23.23%/+25.77%, elapsed+15.41%/+16.11%; text inconclusive (14.29% CPU control drift). One permitted4.951s CSV capture:803 guest samples/64 unknown, async main loop86self, remaining work distributed. Not causal fractions. [Evidence](benchmarks/results/2026-11-01-async-hosting-assessment/README.md). |
| B51 REJECTED | One43.017s service, valid controls. Shared loop removes79 net lines but async CSV CPU+1.04%/+2.02%, elapsed-2.15%/-1.53%; misses frozen >=7.5%CPU AND >=3%elapsed targets. Prototype removed, original source restored/tested. No tuning or speedup claim. [Criteria, source and all results](benchmarks/results/2026-11-01-shared-dispatch-rejected/README.md). |
| B52 CLOSED | Numeric-report attribution sparse; native UTF-8 numbers/accumulators already present. No scalar representation rewrite from these samples. [Evidence](benchmarks/results/2026-11-01-numeric-report-attribution/README.md). |
| B53 INCONCLUSIVE |60s deadline, missing controls and large CPU drift; iterative JSON prototype removed.18 independent continuation/ownership regressions retained. [Evidence](benchmarks/results/2026-11-01-iterative-json-inconclusive/README.md). |
| B54/B55 CLOSED | Identical separate-worker medians differ9.78%; private shared-CoreLib identical-image control differs0.81%. No broad repeatability or causal claim. [Failed control](benchmarks/results/2026-11-01-identical-worker-control/README.md), [private control](benchmarks/results/2026-11-01-shared-process-control/README.md). |
| B56 INCONCLUSIVE | Text controls+4.00% exceed3%; gain-5.94%/-9.56% misses7.5% against both. Casing prototype removed/restored/tested, no retiming. [Evidence](benchmarks/results/2026-11-01-casing-context-inconclusive/README.md). |
| B57 REJECTED | CSV compile-5.53%/-4.85% misses5% against both; warm controls+3.55% exceed3%. Analyzer restored/tested;17 external regressions retained, seven runnable Python goldens. [Evidence](benchmarks/results/2026-11-01-annotation-frontier-rejected/README.md). |
| B58 CLOSED | Same-bits source control: expanding per-record helper expression changes complete CSV-16.31%/-17.88%, text-13.95%/-13.59%; valid controls. Material net call-shape effect, no removable fraction/runtime gain. [Evidence](benchmarks/results/2026-11-01-helper-cost-assessment/README.md). |

Current CPython guide measures **830c6251**, not HEAD: diagnostic warm gaps2.08x
CSV/4.93x text/1.84x JSON; fresh text inconclusive. CSV compilation94.64us vs
Python54.71us/earlier Lython330.30us. Historical warm-text+8.06% adverse observation
stays visible. Short process-CPU observations are not qualified embedding latency
or a new external ratio; phase medians and different owner lifetimes cannot be
subtracted. docs/BENCHMARKS.md remains the maintained external comparison.

## Completed work and history

Completed prospective criteria/details are preserved in
.git/agent-notes/PLAN-history-through-B60.md and earlier ledgers, with immutable
experiment evidence under benchmarks/results/. Keep current docs about current
behavior, not experimental narratives. Terminal runs below are cached, never polled.

| Milestones | Retained result / stopping decision |
| --- | --- |
| B25/B26/B27 | Direct Python lexer, diagnostic correctness, last core runtime dependency removed; capacity-level dictionaries replace entry ledger. Compilation allocation-22.7–24.2%; full checkpoints41262561230/41266997253 green. |
| B28/B29 | [Representative phases](benchmarks/results/2026-11-01-representative-checkpoint/README.md) and [text/CSV CPU](benchmarks/results/2026-11-01-current-text-cpu/README.md). Fixed collections complete. |
| B30–B37 | Property/whitespace/cursor/sigma/write prototypes rejected or inconclusive and removed; independent regressions retained. Compiler/current-source identities and invalid restored-binary evidence preserved. Real whitespace-loop cancellation fixae22392d remains; terminal41269118889,41273799899,41274744720 green. No reopening from favorable isolated samples. |
| B34 | [Allocation/lifetime decision](benchmarks/results/2026-11-01-current-allocation-origins/README.md): frame allocations do not justify pooling/fusion/arena. |
| B38/B40 | Native/custom dictionary equality/mutation correctness and direct funded copies; original independent failures preserved.1c369000/9e99f640, terminal41279594583/41283441395 green. |
| B39 | [JSON/config attribution](benchmarks/results/2026-11-01-current-json-cpu/README.md): distributed costs/sparse configuration; no second parser or recapture. |
| B41/B42/B43 | Benchmark-boundary/handler comments clarified; [native-offset attribution](benchmarks/results/2026-11-01-main-loop-attribution/README.md) selects shared-state consolidation. |
| B44 | Shared state replaces service/guard/limit facades, preserving guards/lifetimes. a0eae244, terminal41288123299 green. [CPU diagnostics and limits](benchmarks/results/2026-11-01-execution-state-consolidation/README.md). |
| B45/B46 | [Frontend attribution](benchmarks/results/2026-11-01-current-frontend-cpu/README.md); [precedence parser rejected](benchmarks/results/2026-11-01-precedence-parser-rejected/README.md), original parser restored,23 independent tests retained. |
| B47 | Ordinary parser/public phase cancellation gaps fixed, later-pass/preemption limits documented.082dd130, terminal41291403075 green. [Evidence](benchmarks/results/2026-11-01-compiler-cancellation/README.md). |
| B48 | Starred annotations use ordinary one-value unpacking; generic expression fallback and unknown-node wrapper removed.95b5955c, terminal41296168049 green.22 Python goldens+six real async lifetime cases; possible+2.38%/+3.34% CSV CPU cost retained. [Evidence](benchmarks/results/2026-11-01-expression-route/README.md). |
