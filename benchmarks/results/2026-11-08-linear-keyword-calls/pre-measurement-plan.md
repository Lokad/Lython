# Lokad.Lython 0.11.0

Updated2026-11-08. Git-ignored; never force-add. Publication/tagging postponed.
Objective: coherent simplification and substantial complete-script performance.
An isolated experiment does not complete this objective. Histories/raw evidence
remain in benchmarks/EXPERIMENTS.md/results, not current guides or this task list.

## Actual delivery status

| Milestone | Authoritative status |
| --- | --- |
| R62 /70267889 | Fully qualified CI42412531882, cached terminal green. |
| R65/R66 /824fc6bb | Fully qualified CI42418518578: Windows/Ubuntu Release actual11/10, original Stress both, installed core consumers. |
| R67 /5076359c | Current CPython guide describes measured824fc6bb/core15c04e2a,23jobs/478.32s; old55e510ff tables historical. All source/golden/quiet/cleanup/archive gates pass. |
| R68 /5076359c | Immutable dev55/d6067925 compatibility,86internal/514public per actual11/10, unchanged bridge. Normal optional0.4pin/core unchanged; final released/safety/performance qualification separate. |
| R69 /fd5ab1c1 | Complete returned-record CSV workload and bounded current attribution committed/pushed; runtime unchanged. |
| R70 /7a81a629 | FULLY QUALIFIED CI42422556519: Windows/Ubuntu Release actual11/10, original Stress both, installed consumers. Terminal receipt cached; never query again. |
| R71 initial | Uncommitted prototype81fba80c/aa4ae796,1,366internal/3,118public per target, matching probes;20public defaults checks passed on baseline first. B424 complete/archived/verified114.295s. |
| R71 refinement | Active final-source prototype40a4959e/a00c2ce7. Direct completed frame contexts and direct sync binding.1,366internal/239targeted public per actual11/10, matching probes. Prior context-only variant separately passes1,366/3,118 per target. No final performance/delivery qualification yet. |

R70 borrows immutable tuples, retiring duplicate iterator/list/backing/reservation
work. Returned-record allocation−13.57%sync/−14.40%async, controlled elapsed
−6.45–7.45%sync/−7.01–9.34%async; ordinary controls near baseline. Desktop text
adverse and coarseCPUcontrolCSV+4.07% preserved. No universalCPU/CPython/debt claim.
[Evidence](benchmarks/results/2026-11-07-tuple-unpack/README.md).

## Active R71 — finish ordinary executable activation consolidation

Hypothesis/affected work: ordinary keyword/variadic helpers, including returned-record
CSV, should share positional calls' linear runner rather than recursively enter
InvokeFromOperands/InvokeBound/ExecuteBody drivers. Existing final workspace and
shared binder remain; eligible module contexts defer independently of signature.
General mirrored/lowered callbacks retain real callers. R69 inclusive431samples
include useful guest work and do not predict saved CPU.

Implemented deletion/replacement:
- One preparation/bound-body transition; actual pending defaults own one await
  continuation and existing call admission. No new persistent binding fields.
- Remove redundant local-storage bool and impossible generic positional selector.
- Remove generic operand native borrow/InvokeBound flags: only executable functions
  override it, and frontend excludes NativeBody for mirrored code. Runner native
  borrows and callbacks' native execution after binding remain.75internal cleanup
  regressions pass, preserving origin/identity/retained inputs/depths.
- Final refinement removes completed-frame ValueTask packaging and the new bool-
  selected binding wrapper. Completed contexts/ordinary sync bindings return directly;
  async binding keeps its existing ValueTask. No new binder, arena, policy or engine.

Ownership/semantics: call admission transfers exactly once through binding/body/
child completion. Caller operands and owned workspace stay funded through actual
default callbacks and cleanup. Binding failure returns its workspace/admission
without inventing an entered-function frame. Preserve live replaced defaults,
receivers, closures, kwargs/varargs, exception state, recursion/managed stack safety,
current cancellation, first terminal refusal and awaited cleanup. No preliminary
"no protocol defaults" routing guard. Deep keyword recursion retains301frames;
actual default suspension/cancellation/refusal/argument aliases/budget reuse pass.

Initial B424: allocation−13.5%sync/−12.6%async; VM affected sync+0.43–2.76%,
async−0.33–4.51%. CSV/text controls vary (baseline spreads3.86/4.63%); desktop
CSV+8.47–9.98% retained. All gates/raw/source/cores preserved. This uncertainty
and the newly added completed-value packaging justify ONE coherent refinement.
It is not a fixed-percentage veto or a prediction that packaging caused the timing.

Next: freeze final unchanged refinement, run the existing five-script allocation
diagnostic and one same-shaped four-job source pair: affected warm/warm-async,
ordinary CSV/text warm, two baseline/two current processes,5s warmup/role,
7rotated150ms rounds. Baseline R70d6445fc5 is now fully qualified.150sowner+
30stop+30margin; observe210s, retain adverse/partial/inconclusive results and never
restart an observation timeout. Complete at a retained/restored source and explicit
regression-debt decision. Batch full platform/Stress/installed consumers for the
retained source, then continue independent roadmap work while CI runs. No primary
CPython refresh or repeated old qualification for this local refinement.

## Following runtime deliverables

| Boundary | Selection / removable work / affected scripts / acceptance |
| --- | --- |
| Primitive construction and complete frames | After R71, inspect whether intrinsic list/tuple construction still forces an otherwise unused lexical context. Select an entire remaining construction phase, not a special benchmark kernel. Returned records/helper comprehensions affected. Preserve evaluated inputs, starred protocols, admission overlap, actual suspension, identities and errors; remove displaced construction/deferred-context routing. Source facts must select before code. |
| Operand/scalar dispatch | Existing indexed operands/native returns/final slots/deferred contexts already exist. Current text capture attributes useful arithmetic/dispatch, not boxing alone. Require a complete removable phase before a representation change; exact promotion, Python identity/aliases, publicBigInteger/CLR ingress remain. |
| Per-record storage | Shared keys/CSV array transfer/direct JSON slots and R70 tuple borrow delivered. Select a real remaining copy/capacity phase; preserve mutation/reentry/hooks/order/retained aliases and remove old construction work. |
| Bulk text/JSON | Direct UTF-8 casing/lower-split/formatting/native parsing, completed JSON loop and shared line scanner delivered. R61 finds no escaped tokens in measured ordinary fixtures. Require a newly demonstrated whole conversion/storage phase; preserve Unicode/context/hooks and async I/O. |
| Frontend facts | R55/R58/R62/R65 remove repeated lexical/graph/validity/module work. Remaining class/function demands have distinct scoping/annotation rules. Reuse prepared facts only with a concrete walk deletion and exact diagnostics; no speculative visitor/cache or parser migration. |

For each selected redesign: record hypothesis, eliminated work/storage, affected
complete scripts, semantic/token/admission checks and displaced implementation.
One bounded source/generated-code assessment; only one focused current attribution
when existing evidence cannot select the design. Stop investigation at a concrete
phase or explicit insufficient evidence, then choose another boundary. Temporary
setbacks require a completion milestone and debt decision. Allocation, architectural
simplification, controlled elapsed and CPU are distinct. Bounded confirmation/
refinement/correction is allowed with evidence and original adverse conditions
retained. No blanket one-attempt/no-retiming/permanent-closure rules or automatic
percentage veto. No broad ownership/profiling campaign or benchmark framework.

## Independent regex deliverable

Core owns Python/native UTF-8 semantics, byte ranges/scalar positions/original
windows, flags/captures/replacements/errors, cache ownership/current tokens and
one provider across frontend/runtime/imports; no Utf8Regex runtime dependency.
Optional adapter is nonpackable0.4.0. Dev55 compatibility is separate from final
released-adapter/managed-safety/backend-performance qualification; no speedup claim.
Producer gaps remain precise: DetailedData lacks distinct preparation/working-owner
input; native name lookup lacks enumeration; creation retains its token despite
current-move overloads. Existing adapter-local bridges remain. Development inputs
support testing; only affected final qualification waits for producer publication.
No sibling edits/builds or replay of producer Full. No dialect fallback, regex quota,
new memory layer or abandoned matching. Continue core/independent runtime work.

## Completed evidence and lasting constraints

Shared compiler-fact correction, direct UTF-8 formatting/storage and native helper
returns are qualified. Returns allocation−19.92/−19.31%; original elapsed diagnostic.
Formatting allocation−8.67%, controlled B102elapsed/CPU−5–8%; original adverse
15.125ms preserved/unexplained. Do not replay unchanged qualification. R20/R25
supersede the rigidly rejected CSV constructor; slower call-arena/control designs,
withdrawn R54/R60 text windows, R61/R64 source decisions remain in experimental
history, not dormant code. Current guide is docs/BENCHMARKS.md; tables name their
measured revision rather than implying today's HEAD.

AGENTS.md governs fully managed/memory-safe code and managed stack admission:
no unsafe/Unsafe/PInvoke/raw pointers/native probes. Preserve host capabilities,
effective cancellation, checked representation bounds, XML depth and bounded I/O.
Use the borrowed shared allowance/coarse preflights/replacement overlap/retained
aliases; no hidden allowance/caps/fuel/quotas/tracing/weak reclamation/forcedGC.
Scope creation reserves nothing. Initial bounded streams use host-driven batches.
First structured refusal is terminal through guest suppression/finally returns;
native refunds/cleanup remain awaited, no retry or optional-storage fallback.

Focused Release Category!=Stress during iteration, matching probes before checks
that use them. No overlapping local builds/tests/measurements. Force nonincremental
build after timestamp-preserving restores. Full Windows/Ubuntu actual11/10,
original Stress and installed core consumers at coherent retained checkpoints.
Use explicit UTF-8. Only core Release pack via tools/pack.ps1 to artifacts/nuget;
no adapter package, alternate release bundle, publication or tag promotion.

VM lokad@20.166.121.73, key ~/.ssh/id_lython-bench.pem. B385–B424 terminal,
archived/locally verified; never query again. No active collection yet. Persistent
workers/records separate from owned128MiB RAM fixtures. Keep256MiB staging floor;
only reclaim verified redundant extracts while retaining immutable evidence.
Join all users/unmount, archive/verify under lease and locally. No sole RAM evidence.
CI42422556519/R70 is terminal green, cached. Docs remain concise/current; plans,
handoffs/receipts/history stay outside docs. Publication remains postponed.
