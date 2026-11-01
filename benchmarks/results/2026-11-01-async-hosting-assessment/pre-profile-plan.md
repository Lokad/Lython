# Lokad.Lython 0.11.0 — active plan

Updated **2026-11-01** (Paris). Two independent development tracks. An unpublished
producer API gates only final optional-adapter qualification, never core work.
No publication or tag is authorized. PLAN.md remains intentionally untracked.

## Lasting constraints

Fully managed and memory-safe C#; managed stack admission, host mediation,
cooperative CancellationToken control, terminal admission refusal, major-capacity
and replacement preflights, retained aliases and awaited cleanup. No unsafe /
Unsafe / pointers / PInvoke / native probes, quotas / fuel / watchdogs, hidden
allowances, heap tracing, forced GC or per-object ledgers. Borrow existing host
budget scopes; scope creation reserves nothing. AGENTS.md and docs/RUNTIME.md
specify the current contracts. No guest work resumes after refused admission.

## Current delivery — B48 executable-expression route

Implementation complete, source frozen for delivery. Compile starred variadic
annotations through existing one-value unpacking; preserve true lowered header
consumers. Delete the generic expression opcode/factory/accessor, fallback
storage/interning/mirroring trigger and sync/async dispatch. Delete unknown-node
wrapper; compiler defects fail explicitly. No new opcode, framework, owner or
public option. [Criteria, source proof and evidence](benchmarks/results/2026-11-01-expression-route/README.md).

- Original runtime fails13/16 initial independent goldens. Final22 Python goldens
  pass both APIs; six real async module/class cases preserve cancellation,
  refusal, aliases, funded pending users/retained results and budget/token reuse.
  Standard one-item iterable/type/arity semantics replace the old type-only case.
  Bounded scratch may deny tighter budgets earlier. Captured headers and genuine
  class/annotation/lazy metadata/statement consumers remain.
- Fresh Release build zero warnings/errors; core/worker/test/probe copies equal.
  Focused415 native /608 public, full ordinary3,962 native /15,527 public per
  actual.NET11RC3 /10.0.8 pass. Removed only obsolete empty-array assertions;
  independent semantic/opcode/ownership checks remain.
- One26.112s CSV diagnostic: CPU +2.38%/+3.34%, elapsed -0.51%/-0.05%, allocation
  essentially unchanged. Baseline/control/quiet/golden/cleanup gates pass. Possible
  CPU cost preserved, no causal/latency/speedup claim, no retiming. No new framework.
- Committed/pushed95b5955c. Explicit Release pack and installed consumers pass
  actual.NET11RC3 /10.0.8. Published0.10 hashes/tag unchanged. Full Windows/Ubuntu
  ordinary/original-stress/package41296168049 is running; terminal status remains
  to be recorded once. No publication/tag.

B47 cancellation082dd130 is DELIVERED: terminal green41291403075 on both
platforms, ordinary/original-stress/package and local installed consumers. Never
requery it. B44 a0eae244/41288123299 and B40/41283441395 also terminal green.
Later compiler passes can still finish their current work before phase checks;
CLR conversions/copies are not preemptible. Do not reopen a broad compiler audit.

## Track A — complete Python-specific regex architecture

**Delivered:** Lython-owned native UTF-8 metadata, byte ranges/scalar positions,
original-subject windows, shared split/sub/expand, ownership/current tokens across
frontend validation/constant analysis, runtime, imports, fnmatch/glob. One borrowed
provider. No backend types/dependencies/dialect fallback in core. Contract tests,
Python semantic goldens and actual backend performance are separate evidence.

Ordinary optional adapter pin remains PythonRe 0.4.0. A04/A05 cursor/funding
implementation qualified exact private 0.5.0-dev.1 only, producer source
0e24cecebbb9093a18f5cb962dae3b6f6426742c, PythonRe package SHA256
`e4136eddffdca5da0b9e5733cd9534053d7d99880714669b3695be5305ed7019`.
Development bridges stay outside core and never affect ordinary restore/CI.
No sibling source is modified; do not requalify unchanged private artifacts.

**Final adapter gate only:** wait for a new immutable released package, then:

| Missing/unqualified capability | Acceptance and temporary development boundary |
| --- | --- |
| Native names | Replace adapter-only GetGroupNames encoding when supplied; retain effective/reported flags and capture metadata. |
| One-shot ranges | Direct match/search/fullmatch ranges plus working-funding receipt. Current inspected API lacks overloads; conservative detailed snapshots stay in the adapter. |
| Reversed windows | Qualify pos > endpos zero-width matches with original anchors/prefix/lookbehind. Core/test provider supports it; current real adapter rejects explicitly. |
| Programs/cursors | Requalify funded compilation/cursors, current-token moves, independent borrows and disposal after users finish. Demonstrate actual in-flight interruption. |

Use existing dependency/semantic/lifetime/refusal/reuse controls, isolated real
cancellation probes, producer managed-safety assessment and one focused backend
comparison against the new pin. Temporary adapter/test-provider conversions are
acceptable for development, never core plumbing or a backend performance claim.
No package-index polling; missing producer capabilities stop only this final gate.

## Track B — substantial non-regex performance redesign

The current CPython guide measures **830c6251**, not HEAD: diagnostic warm gaps
2.08x CSV / 4.93x text / 1.84x JSON; fresh text inconclusive. CSV compilation
94.64 us vs Python 54.71 us / earlier Lython 330.30 us. Warm text's adverse
+8.06% historical old/new observation remains visible. These are diagnostics;
phase medians cannot be subtracted, nor allocation called a latency improvement.

**B43 CLOSED:** current text capture6.980s /13.105s collection, full goldens,
quiet and joined cleanup pass. 1,043guest samples;165 in main loop. Unique Tier1
code range3,857bytes matches the dump and maps all165 IPs with no gaps.85 samples
are in fetch/guard block,67 in guard-related subrange. These are NOT causal or
removable CPU fractions. Source shows context -> services -> guards -> state ->
governor/limits; both facades are one-to-one with the existing shared state.
Sigma-property102samples remains visible; no closed casing prototype is reopened.
[Evidence](benchmarks/results/2026-11-01-main-loop-attribution/README.md).

**B44 DELIVERED:** shared-state consolidation meets source/semantic criteria
and its fixed >=5% CPU improvement criterion on CSV against both controls, with
text below regression limits. Source acceptance is independent of performance;
this is diagnostic process CPU, not repeatable latency or a new CPython ratio.
The [frozen criteria and complete observations](benchmarks/results/2026-11-01-execution-state-consolidation/README.md)
retain the hypothesis, exact deletions/tradeoffs, affected scripts, semantic
acceptance, untouched compilation control and stopping rules. Qualification is
at the current delivery boundary above; no favorable retiming or further tuning.

**B45 CLOSED:** one current compiler capture3.084s /10.628s collection,
32,768 fresh CSV compilations,613 compiler-thread samples. Static analysis182,
parser161, executable construction91, tokenizer12 inclusive-chain observations;
groups overlap,105 unknown leaves remain. Both quiet/full-golden gates and normal
compiled-owner/worker cleanup pass. No recapture or timing claim. Current CSV
already uses UTF-8 and scope facts are already shared; do not reopen obsolete
conversion/cache proposals. [Evidence and source decision](benchmarks/results/2026-11-01-current-frontend-cpu/README.md).

**B46 REJECTED:** precedence climbing removes30 net parser lines, but only
one of three compilation cases meets >=10% CPU target against both controls:
CSV -6.87%/-7.61%, text -6.08%/-6.76%, config -13.54%/-13.05%. Fresh-config
control drift -19.05% also invalidates that phase. One78.98s collection, no retry,
tuning or relaxed acceptance. [All controls/criteria/evidence](benchmarks/results/2026-11-01-precedence-parser-rejected/README.md).
Original parser restored with fresh mtime/nonincremental build; exact baseline
core SHA restored across worker/test/probe. All713 focused cases per actual target
pass on restored source, including23 independent new semantic/diagnostic/protocol
cases. No runtime parser change is retained; do not reopen from these samples.

**B47 DELIVERED:** demonstrated ordinary-parser and public post-parse
cancellation gaps corrected. [Frozen criteria, original failures, final-source
checks, measured/final patches and explicit limits](benchmarks/results/2026-11-01-compiler-cancellation/README.md).
Terminal Windows/Ubuntu ordinary/original-stress/package41291403075 is cached.
No automatic compiler-wide audit.

**B48 IMPLEMENTED:** generic executable expression route removed; genuine starred
annotation consumer uses existing one-value unpacking and correct Python iterable
semantics. [Complete frozen criteria, deletion, tradeoffs and controlled evidence](benchmarks/results/2026-11-01-expression-route/README.md).
Qualification is at the current delivery boundary. No speedup claim; possible
warm CPU cost stays visible. No further capture or opcode tuning for acceptance.

**B49 CLOSED — ordinary keyword calls.** Existing operands already feed shared
binding/local storage directly; there is no argument array/name dictionary to
remove on these signatures. Same frozen B48 core across all six processes. One
47.336s complete-job comparison: named CSV CPU -4.67%/-5.71%, text +2.53%/+4.28%;
text possible elapsed cost +4.96%/+3.84% retained. Quiet/full goldens/balances,
resolved CPU and5%/0.2% drift gates pass; CPython validates both named scripts.
No case meets predeclared >=10% gap, so no runtime candidate, binder framework,
retiming or speedup claim. [Source decision, criteria and full evidence](benchmarks/results/2026-11-01-keyword-call-assessment/README.md).

**B50 — asynchronous hosting CPU-path assessment (next bounded decision).**
The published CPython comparisons use Run; they do not establish RunAsync's cost.
Existing compiled functions/direct calls and synchronous exact builtin operations
are already shared. Inspect remaining per-opcode async transport, not obsolete
lowered-function/member-cache claims. Hypothesis: CPU-only work invoked through
RunAsync pays substantial generic async dispatch despite a synchronously completing
file host. Measure magnitude before selecting a shared sync-until-suspend redesign.

Reuse maintained CSV/text sources/fixtures/goldens and frozen B48 runtime. Same
worker for Run/RunAsync/identical Run; only API changes. One <=60s collection,
<=30s/child,4warmups/7batches64 complete jobs; ordinary GC/tiering, quiet gates,
all outputs/budget balances/joins, CPU batches>100ms, <=5% CPU/0.2% allocation control
drift. Warm execution includes real I/O/cleanup; startup/compilation excluded and
not attributed by subtraction. Driver awaits public Task to actual completion;
record the additional reflection getter separately as diagnostic plumbing.
Select only a >=15% complete CPU gap against both controls on at least one script.
No retiming or local tweaks if materiality fails. If met, one <=10s focused CPU
capture (<=40s owned collection) may resolve which remaining async mechanism is
material; stop once attribution/source decision is made. Source-only totals do
not establish a removable CPU fraction.

First API diagnostic complete28.010s service: CSV RunAsync CPU10.392ms vs
Run8.433/8.262ms, approximately+23%/+26%; CSV controls pass. Text Run control
CPU drifts about14%, so text is inconclusive and will not be retimed. CSV alone
meets the prospective materiality/control criterion. Source still has a distinct
async loop with await-capable iteration/member/operation helpers; many native
operations already execute synchronously. No candidate selected from total alone.

Resolve that attribution gap with the allowed ONE CSV warm-async CPU capture:
512 complete jobs,199Hz cpu-clock/fp/perf maps, actual named guest thread, <=10s
capture/40s owned collection. Reuse existing checked worker/perf protocol, ordinary
GC/tiering, quiet/full-golden gates and joined worker/profiler cleanup. Preserve
all threads, unresolved leaves and malformed map records. No native diagnostic
code enters production. Stop after source/attribution decision; no recapture.

Before a candidate, freeze exact duplicated work/state to delete, affected
scripts, tradeoffs and one controlled before/candidate/identical-before bound.
Preserve real protocol/callback/host suspension, current tokens/terminal refusal,
funding until actual users and awaited cleanup finish, aliases/defaults/closures,
Python evaluation/exception order and managed stack admission. No universal dispatch
framework, alternate backend, new policy/configuration, pooling or per-object ledger.

Next deliverable: a coherent redesign removing demonstrated dominant transport,
state construction or conversions in complete agent scripts, rather than a new
series of local probe/scan/reservation tweaks. Cover configuration editing, file
search, CSV/JSON aggregation and report generation. Reuse B28 phase boundaries,
B29 CPU origins and B34 allocation/lifetime evidence. Current data shows main
instruction dispatch, distributed synchronization and native text work; frame
allocations alone do not establish a removable CPU fraction.

Before new code, record:
- **Hypothesis and deleted work:** name the transport/state/conversion that goes
  away, its measured importance, and affected complete scripts. Inspect call /
  argument / frame costs, boxed dispatch, text boundaries and frontend costs as
  alternatives; do not select a universal value/frame rewrite from object counts.
- **Semantics/lifetimes:** Python values/effects/order, sync/async calls,
  closures/generators where affected, real suspension, in-flight cancellation,
  admission before mutation, retained aliases and cleanup/budget reuse.
- **Bounded comparison:** one fresh-source before/candidate/identical-before
  collection, same inputs/outputs/runtime/tiering; distinct compilation, warm
  execution, I/O/cleanup boundaries. Freeze material-improvement and control-drift
  bounds before implementation. Include an unaffected script. No favorable
  retiming, phase subtraction, allocation-only speedup or invalid baseline.
- **Stopping criterion:** one bounded source/design decision using existing
  traces. Add a narrowly scoped profile only if a named missing attribution
  prevents that decision; freeze its duration and stop once answered. If no
  coherent deletion is justified, record why; do not force another small patch.

Fresh nonincremental builds are mandatory after timestamp-preserving restores.
Verify source markers and equal core/worker/test/probe copies before freezing;
a binary hash alone does not prove the declared source. Stop/remove a failed
prototype. Failed comparisons do not block independent correctness or Track A.

## Completed milestones and cached delivery

Full history/criteria are preserved in .git/agent-notes/PLAN-history-through-B48-selection.md
and prior ledgers; immutable experiment evidence is linked below. Do not rerun
unchanged terminal checks or old comparisons.

| Milestone | Result |
| --- | --- |
| B25/B26 | Direct Python lexer, correct tabs/EOF diagnostics, last core runtime dependency removed. Compilation allocation -22.7–24.2%; full checkpoint 41262561230 green. |
| B27 | Capacity-level dictionary ownership deletes entry ledger. Runtime830c6251; original 2,000-call fixture preserved with corrected allowance3fedf3f6. Full41266997253 green Windows/Ubuntu. |
| B28/B29 | [Representative phases](benchmarks/results/2026-11-01-representative-checkpoint/README.md), [current CPU](benchmarks/results/2026-11-01-current-text-cpu/README.md); fixed collections complete. |
| B30/B33 | [Property](benchmarks/results/2026-11-01-sigma-properties-rejected/README.md) / [whitespace](benchmarks/results/2026-11-01-bulk-whitespace-rejected/README.md) prototypes rejected; runtime restored, independent regressions retained. |
| B31/B32 | ae22392d fixes real cancellation gaps in seven whitespace loops. Full41269118889 green Windows/Ubuntu, ordinary/stress both targets and installed consumers. |
| B34 | [Allocation/lifetime assessment](benchmarks/results/2026-11-01-current-allocation-origins/README.md); no justified frame pool/fusion/arena from these samples. No runtime change. |
| B35 | [Cursor prototype rejected](benchmarks/results/2026-11-01-iteration-cursor-rejected/README.md); independent semantics retained. Fresh-checkout41273799899 green. |
| B36 | [Sigma-context comparison invalid](benchmarks/results/2026-11-01-sigma-on-demand-inconclusive/README.md): stale restored binary. Prototype removed; nonincremental rebuild/copy identities and goldens verified.8d099a1f /41274744720 terminal green Windows/Ubuntu. No retiming. |
| B37 | [Write consolidation rejected](benchmarks/results/2026-11-01-dictionary-writes-rejected/README.md): misses CPU target, control drift. Runtime restored exactly, two semantic cases retained. Committed c57f9752. |
| B38 | Mixed native/custom misses, defaultdict population consolidation and direct funded copies. **1c369000**, full **41279594583** terminal green Windows/Ubuntu, both ordinary/stress/package targets. [Evidence](benchmarks/results/2026-11-01-mixed-dictionary-keys/README.md). |
| B39 | [JSON/config CPU](benchmarks/results/2026-11-01-current-json-cpu/README.md): 6.093s capture, sparse configuration samples, distributed JSON costs; no parser rewrite or recapture. |
| B41/B42 | **5d16ead3** clarifies existing benchmark lifetimes and handler-depth comments. One source-only boundary comparison; no-region frames already omit depth storage. No runtime statements changed, prototype or capture. |

Older failed/cancelled CI and corrected restoration claims remain in history.
Canonical unpublished packages currently identify **95b5955c**; published 0.10
packages/tag are unchanged. Current docs describe supported behavior, not this
history. Focused Release development; full stress/platform/package checks only
at coherent retained-code boundaries. Original stress scales/timeouts stay intact.
