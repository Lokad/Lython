# Lokad.Lython 0.11.0 — active delivery plan

Updated2026-11-02 (Paris). Git-ignored; never force-add. Publication/tagging remain
postponed. Whole-script performance remains open beyond any isolated trial.
Detailed completed milestones and experimental decisions are preserved separately
in benchmarks/results/ and private PLAN-history-before-b125-checkpoint.md; this
active plan records deliverables, evidence, blockers and acceptance.

## Actual status

| Delivery | State and limits |
| --- | --- |
| Shared compiler facts 61237f58 | COMMITTED/FULLY QUALIFIED. Iterative shared queries remove an exponential cliff. Evidence:2026-11-02-shared-compiler-facts. No unchanged qualification replay. |
| Native helper returns f9ae2d65 | COMMITTED/FULLY QUALIFIED. CSV/JSON allocation–19.92/–19.31%; elapsed diagnostic. Later composed expressions remove interpreter storage but retain documented CSV/JSON CPU debt. Evidence:2026-11-02-native-function-returns and native-return-expressions. |
| Direct UTF-8 formatting6e1ca104/df548ddf | COMMITTED/FULLY QUALIFIED. Original allocation–8.67%; B102 source/control confirmation about5–8% lower elapsed/CPU. Historical15.125ms adverse result remains unexplained/preserved; no current correction indicated. Evidence:2026-11-02-formatting-confirmation. |
| Later casing/storage/CSV/formatting | COMMITTED/FULLY QUALIFIED throughaa6cf3cb/runtimec3fa26cc. Useful bulk CSV reconsideration delivered082179c4 (allocation–13.7..15.1%, CPU–7.2..11.1%). Separate B110 direct CSV fusion withdrawn, as was B108 mutable dispatcher; all rejected code absent. Reports/data preserve remaining Unicode/CSV debt and adverse controls. |
| B123 JSON090574d1 | COMMITTED/pushed/focused-qualified,170 internal/736 public per actual.NET11/10. Plain tokens use existing scan facts; redundant parse/unused scratch gone. Cancellation crossing-check correction included. Report/JSON-lines lower elapsed~9–11%/~6–8%, allocation–4.80/–5.97%. FULLY QUALIFIED41519435155. |
| B124 contexts90785737 | COMMITTED/pushed/FULLY QUALIFIED41519435155; focused332 public/58 internal per actual.NET11/10. Closed module native bodies omit unobservable ExecutionContext; binding/state/entry checks/evaluator shared. Allocation CSV/JSON/text/search–10.42/–11.14/–12.95/–36.58%. CPU/elapsed mixed, adverse text/search retained; no general CPU gain. Source census also records unmeasured local-assignment coverage gap. Evidence:2026-11-02-native-call-contexts. |
| External CPython table | Named older sourcesc3fa26cc/609e5de1/082179c4, not today's HEAD. Report fresh3.28x/warm3.31x, CSV2.02x/2.13x, text3.95x/4.08x; diagnostic and variable controls explicit. B125 COMPLETE: five affected scripts refreshed at90785737; search inconclusive. Evidence:2026-11-02-current-cpython-checkpoint. |
| Optional producer dev.10 | Development qualification COMPLETE/cached.84 internal/424 public per target, six real compile/match/cursor cancellation probes; adapter pin0.4.0 unchanged/nonpackable. Core runtime dependency count0. Released adapter qualification alone remains deferred. |

**Delivery FULLY QUALIFIED:** cached terminal41519435155, exact90785737.
Windows/Ubuntu ordinary Release.NET11/10, original Stress both and installed
package consumers PASS. Receipt:benchmarks/results/2026-11-02-native-call-contexts/
delivery-ci-status.json. B123 and B124 fully covered. NEVER query this terminal
run again, or older cached41504778956/41501504578/etc. Original failed formatting
delivery and correction remain preserved. Docs/evidence19fc4324 committed/pushed.
B127/7da7335a is COMMITTED/pushed/focused-qualified outside that full proof.
Its affected comparison is complete; no VM job remains. Receipt metadata4ded4a91
is also committed/pushed. Full qualification batches subsequent retained work. VM is free, no active worker remains.

## Priority1 — close this coherent checkpoint

- B125 COMPLETE: twelve jobs229.568s, all full goldens/identities/quiet/cleanup
  PASS, owned unit exit0/inactive/lease free. Fresh CSV/JSON/text/report2.00x/1.68x/
  3.76x/3.21x, warm2.05x/1.74x/4.08x/3.43x. Search fresh270% IQR/218% unchanged
  control drift, warm138%/77%: inconclusive, no reliable multiplier. Original
  adverse clocks retained, no confirmation retiming. CSV/report compile1.54x/1.52x;
  startup unmeasured, I/O/staging/check/cleanup explicit. docs/BENCHMARKS.md current.
- Full delivery COMPLETE/cached41519435155 for90785737. Reuse it; do not rerun
  unchanged qualification. Next full delivery batches later retained runtime work.
  Publication/tagging remain postponed.

## Priority2 — substantial next redesign, from current whole-script evidence

B126 current CPU source gate COMPLETE/read-only90785737: text/search13.855s
capture total,791/304 guest samples, all goldens/quiet/joined cleanup PASS; only
perf-map diagnostic override. Dispatch self~15-16%, meaningful checked iteration/
interface layers; native-body inclusive cost includes useful work. Unknown
leaves~6-7%, partial chains explicit. No exact removable CPU fraction, boxed-value
dominance or elapsed-waiting attribution established. Evidence:
benchmarks/results/2026-11-02-current-iteration-cpu/. No further capture needed
before the selected construction experiment. Existing argument/fact optimizations
and compact two-slot instructions are real, not hypothetical missing work.

### B127 retained - shared owned iteration

COMMITTED/pushed7da7335a, focused-qualified; full delivery batched later. CheckedEnumerator, CheckedRange,
range.IterateChecked and PeekIterator removed;58 net production lines gone. One
existing cursor drives executable loops and checked enumerable consumers, with
sync protocol selected once. User wrapper advertises its existing async interface.
No new framework/pool/owner/configuration. Terminal disposal cannot restart; original
subject/lazy source/context survive actual mixed cleanup, then detach.

Focused252 internal/1084 public per actual.NET11/10 PASS. Two new regressions fail
on frozen137b563a before source and pass now. Preserve initial21 async-precedence
and6 mixed-source-liveness failures; original cleanup assertions unchanged. Active
cancellation no longer asserts exact checkpoint placement. All measured goldens,
identity/quiet checks, balances and joined cleanup PASS; services inactive/leasefree.

Final sourcea3a53e2e: text elapsed about2.4-4.5% below unchanged controls, CPU2.6-7.3%
with drift; allocation-1.4176%. CSV/search flat or inconclusive. Range elapsed2-3%
above controls remains ACCEPTED DEBT for the next coherent operand milestone.
Initial b7e66c78 range+4.7-5.9%/all adverse data and refinement rationale preserved.
Do not compare independent medians as a causal cache-only speedup. Evidence:
benchmarks/results/2026-11-02-shared-iteration/. No new CPython ratio or full-source
qualification claim. Batch platform/stress/consumers with subsequent retained work.

### B128 next - ordinary executable operand/frame construction

ACTIVE bounded source/design gate, no runtime prototype. Current source snapshots
and compile-only census use frozena3a53e2e (7da7335a), not older profile assumptions. Current CPU capture still shows
material ExecuteUntilCall/value transport; boxing or one constructor alone does
not establish dominance. Census actual emitted instructions for current numeric
loops, CSV/JSON/text/report and equivalent local-assignment helpers. Inspect existing
local/operand/argument/cell storage and which loads/pushes/pops merely transport a
value versus capture Python evaluation order. Final-slot binding and compact
instruction operands already exist; do not invent missing copies.

Assess a coherent normal-engine design eliminating redundant operand transport/
frame storage and eventually the separate native-return graph, rather than adding
more syntax-shaped shortcuts. One executable model must preserve original spans,
unbound reads, callable/effect order, locals/closures/cells, generators and pending
async operands. No new backend/mode, arena/dispatcher revival or universal tagged
value commitment. Name the old implementation to remove and the complete adoption
boundary before coding; do not wrap it in a permanent alternate route.

End the gate with one feasible design/deletion milestone plus a bounded complete
source-pair/control comparison, or an explicit insufficiency decision and next
substantial area. Use existing B126 evidence first; no new broad profiling campaign.
Current source facts (private operand-frame-source-b128/census.json): basic range
loop repeats ForNext/StoreLocal/Jump then LoadLocal/LoadLocal/Augmented/StoreLocal/
Jump:8 instructions, two jumps. Named-intermediate helper bodies remain general
executable (6/6/10/7 instructions versus canonical native4/4/6/5); no semantic
fallback. Their actual style-dependent latency/allocation remains unmeasured.
Frame factory confirms a separate operand array, final bound locals reused, cell/
cache/handler arrays admitted before construction. Merging buffers alone would
not eliminate transports and could lose tiny bound-array pooling; no blind arena.
Before choosing a broad IR migration, run one short current-core source-style
comparison of complete canonical and named-intermediate CSV/text helpers with
unchanged-worker controls, warm and compile-to-result separate. This fills the
material call/frame question; not a new measurement framework or source speedup.

I/O side check: local and actual quiet-VM managed IL for File.WriteAllBytes passes
zero preallocation into OpenHandle. The suspected asymmetric fallocate staging
cost is unsupported/ruled out for this actual call. No fixture change or retiming
is justified from that hypothesis. CPU-vs-elapsed waiting and B125 search instability
remain unresolved; do not blame them on a constructor or silently correct ratios.
Read-only tools/data/VM exit0 recorded privately, no live process or lease remains.

Include range control/debt, compile-to-result and warm boundaries, refusal,
cancellation and retained ownership. Temporary setbacks need an explicit completion
milestone and disposition of regression debt. This does not complete the objective.

| Candidate area | Hypothesis / displaced implementation | Semantic acceptance and decision boundary |
| --- | --- | --- |
| Call/frame and operand construction | Eliminate remaining duplicated state/transport across complete ordinary calls; assess local-assignment helper styles before broadening native expression coverage. Do not build a second statement VM or resurrect B61/B69 arenas/B108 dispatcher without materially new evidence. | Defaults/keywords, locals/closures/cells, identity, globals/imports, evaluation/error/traceback order, generators, real suspension, depth/cancellation, terminal refusal and funded pending users. One source-backed construction/deletion design plus bounded complete script/control comparison; stop speculation if evidence insufficient. |
| Per-record collection construction | Native CSV R1 is delivered. Only consolidate cursor/copy/growth/admission where current complete workloads show material work. B110 parse-time dictionary fusion measured worse and remains removed. | Equal/short/extra rows, custom/duplicate headers/keys, aliases, suspension and retained rows; one invariant/capacity estimate. Compare CSV/text/JSON whole jobs and unaffected controls; explicitly decide remaining regression debt. |
| Scalar/operand transport | Seek a contained exact machine-integer path only if current accumulation dominates; exact overflow promotion rather than universal tagged-value migration. | Numeric equality/hash/identity/aliases, arbitrary precision, public projection/admission/cancellation. Name actual work/storage and displaced boxing/dispatch path before coding. |
| Text/frontend | Remove remaining intermediate representations or repeated valid analyses where current source/profile exposes them. Shared compiler facts/UTF-8 hashing/casing/formatting/plain JSON are already delivered. | Unicode/context/error order, diagnostics/scoping, final storage/overlap/aliases; compile-only and compile-to-result separate. Current compile~60–90us: prioritize demonstrated cliffs or meaningful costs, not a blind pass merge. |

For EACH selected redesign: write hypothesis, affected complete scripts, removed
work/storage/implementation, semantic/refusal/cancellation/ownership acceptance,
bounded before/after with unchanged controls, completion milestone and disposition
of remaining regressions. Temporary setbacks may be justified within that milestone;
do not strand extra mechanisms or let another narrow trial complete the objective.

## Independent regex deliverable

Lython-owned Python UTF-8/byte/scalar/original-subject-window contract, consistent
provider across compilation/runtime/imports, flag/capture/replacement/error semantics,
current tokens and explicit lifetime ownership are delivered. Continue concrete
contract/semantic work independently. Adapter-only bridges or pinned development
producers may support development; no backend types/dependencies in core.

Dev.10/3aa29900 qualification is cached; its actual APIs add no new simplification.
Precisely deferred final RELEASED adapter checks: native UTF-8 capture-name
enumeration, one-shot ranges with working-funding receipt, actual reversed-window
support/qualification, corresponding performance and managed-safety verification.
Missing published capabilities defer only these affected adapter checks. Ordinary
adapter pin0.4.0/nonpackability remain. No rerun of unchanged qualification.

## Measurement and lasting contracts

Timeout, unstable controls and inconclusive observations establish uncertainty,
not failed design. Allow bounded evidence-led confirmation/refinement/measurement
correction; retain original conditions and ALL adverse data. Percentages inform
judgment rather than veto coherent simplification/allocation gains. Distinguish
those from demonstrated CPU improvement. Failed B61/B69/B108 call designs and B110
fusion remain lessons, not automatic permanent bans or automatic promotions.

Fully managed memory-safe C#; no unsafe/Unsafe/pointers/PInvoke/native probes.
Managed stack admission, host mediation and host-token cooperative cancellation
remain. No fuel/quotas/watchdogs, hidden caps/default allowances, tracing/weak
ledgers/forcedGC or new memory infrastructure. Borrowed scopes admit conservative
major capacity/metadata/replacement overlap before allocation, fund aliases/results/
caches/pending users through actual awaited cleanup. First admission refusal is
terminal with original origin; no retry/fallback. Guest MemoryError stays catchable.
Long streaming uses bounded host-driven executions, not cumulative allocation.

Focused Release semantic/cancellation/ownership checks during iteration; batch full
Windows/Ubuntu.NET11/10, original Stress and installed consumers at meaningful
checkpoints. Root README navigates concise current docs. History/procedures/evidence
stay outside docs/. Publication remains postponed.


B129 ACTIVE bounded helper-style comparison at7da7335a: identical core/auxiliary
bits, CSV/text canonical versus equivalent named intermediates, independent
unchanged source control, warm/fresh separate;4 existing-protocol jobs plus short
allocation diagnostic. This is source-style evidence, not a runtime optimization
or refreshed CPython ratio. Decide next construction design from its results.

## B130 selected correction - forward unobservable local values in existing plan

B129 same-core evidence: named CSV helpers add5.48% warm elapsed vs-0.37% control;
named text adds13.51% vs-0.12%. Fresh clocks add7.04%/+1.22% CSV and8.09%/-3.81%
text; fresh CPU controls are unstable, so do not attribute a CPU fraction. Full
stdout/files and hashes pass; no source optimization or new CPython ratio claimed.
Allocation diagnostic pending. This demonstrated ordinary helper-style cost
justifies a bounded existing-plan correction before a speculative register rewrite.

Hypothesis/design: during existing native expression-plan preparation only, treat
adjacent StoreLocal/LoadLocal of the same noncaptured local as forwarding its
already-built operand. Record forwarded slots and reject the plan if a later load
or another forwarding store needs their binding. This is linear, with lazy compiler
bookkeeping; no new opcodes, evaluator, runtime scratch, statement VM, owner or
public option. Original executable instructions and local-name metadata stay intact.
Whole-expression native proof must still finish before work. If a dynamic callback
could inspect the binding, the proof fails and the original executable path keeps
that binding; never rewrite ordinary bytecode or retry after native work/refusal.

Remove the unconditional style-specific loss of existing native lowering; runtime
guard/evaluator remain shared and unchanged. Preserve namespaces (including indirect
locals), captures/mirroring, alias identity, parameter reuse/reassignment, metadata,
error/effect order and spans, real async protocols, cancellation and all funding.
Add observable regressions, reuse existing focused families. Compare complete named
CSV/text scripts before/after with compact source and range controls; include warm
and fresh affected jobs. One bounded source-pair/control decision, all adverse data
kept; no percentage veto or full-backend redesign claim. This does not close range
debt or the substantial performance objective. Batch full checks with retainedB127.
