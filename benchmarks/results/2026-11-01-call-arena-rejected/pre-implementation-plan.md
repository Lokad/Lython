# Lokad.Lython 0.11.0 — active deliverables

Updated **2026-11-01** (Paris). Two independent tracks: missing producer APIs
block only final optional-adapter qualification. They do not block core work,
correctness checks or independent performance development. No publication/tag.
PLAN.md intentionally remains Git-ignored; never force-add it.

## Current checkpoint

Production **9003d045** is DELIVERED. Terminal green **41335606172** covers
Windows/Ubuntu ordinary.NET11/10, original Stress on both targets and explicit
Release installed-package consumers. Terminal proof is cached; never query again.
Local full ordinary3,966 native/15,607 public per actual.NET11RC3/10.0.8 and both
installed consumers pass. Canonical unpublished0.11 packages identify9003d045.
Published0.10 package/symbol hashes and peeled v0.10.0 are unchanged. No publication/tag.

B61/B63/B64/B66 prototypes are absent. B67's independent native-call packet
admission/cancellation correction and representation-independent regressions remain.
Earlier checkpoints/experiments remain in the history/evidence below; do not repeat
unchanged full platform/package or adapter qualification.

## Lasting constraints

Fully managed/memory-safe C#, managed stack admission and host-mediated effects.
Preserve current CancellationToken, first-origin terminal admission refusal,
capacity/replacement preflights, retained aliases/results/caches and awaited actual
users/cleanup. Borrow the host allowance through existing owners; scope creation
reserves nothing. Conservative retention/earlier rejection are acceptable when
explicit; no cumulative allocation substitute for bounded host-driven batches.
No unsafe/Unsafe/pointers/PInvoke/native probes, fuel/quotas/watchdogs, hidden caps
or default allowances, guest-heap tracing, forced GC or new owner infrastructure.
No guest work after refused admission; explicit guest MemoryError remains catchable.
AGENTS.md and docs/RUNTIME.md specify current public contracts.

## Track A — core corrections and final optional-adapter qualification

Core provider boundary and B59/B60 corrections are DELIVERED: Python-specific native UTF-8 contract, byte ranges/
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

## Track B — B69: eliminate actual per-call working-storage allocations

B67 delivery is complete above. B68 stops after its single combined assessment:
phase goldens/accounting pass, but the CPU capture exceeds6s (11.992s), so the
combined run is UNQUALIFIED. Worker/profiler stop/join; no postflight/profile budget
witness. Do not rerun it. Context38.4us is small relative to observed1.4–1.5ms
public jobs; no current evidence justifies a few more shared builtin wrappers.
Compiler/guest origins are small distributed samples, not removable fractions.
[All phases, limits and offline recovery](benchmarks/results/2026-11-01-fresh-config-attribution/README.md).

B69 bounded source/design task BEFORE code: one execution-owned value arena and
saved activation-record stack in the EXISTING executable engine, with executable
frame views created lazily when real consumers observe them. Hypothesis: ordinary
helper calls can stop allocating an interpreter, operand array and separate bound-
local array while retaining a real lexical context. B58's same-bits complete CSV
source effect16–18%, B65's distributed whole-call cost and B34's18.85%/26.13%
frame/context allocation origins justify investigating this path; none predicts
an accepted gain. This is substantially different from B61's records around
unchanged per-call views/operands, and B66's linear instruction/layout trial.

Expected deletions if feasible: ordinary child CreateExecutableInterpreter/new
operand arrays; separate bound-local buffer/copy for arena-backed compiled calls;
per-frame working-storage admissions/refunds; caller object links. Keep one actual
root/generator/protocol driver, existing opcode semantics, real lowered consumers,
Python values and measured simple-return/native paths. No pool, parallel backend,
universal tagged values, JIT, new public options, tracing or owner infrastructure.

Required source proof: map a complete helper call and nested loop, wide/default/
keyword binding, a captured parent while a child runs, generator yield/send/close,
and true native/protocol I/O suspension. Show which state lives in the active driver,
saved records, existing contexts and independently retained cells/snapshots. Views
must preserve stable observable identity during an invocation and correct locals/
class/nonlocal behavior across arena growth; they must not force allocation on
every ordinary call or retain refunded storage. Borrowed ReadOnlyMemory used by
an awaited operation cannot be invalidated by arena resizing/reentry. Preserve
traceback attribution and exception/context/iterator cleanup exactly.

Ownership contract: existing governor funds full arena/record capacity and replacement
overlap before allocation; retain capacity until driver/generator cleanup and await
all users. Pop clears only relinquished references; capacity remains charged. Reuse
within one owner bounds repeated helper calls, not cumulative allocation accounting.
Escaping cells/results/caches retain their existing independent charges. Scope creation
still reserves nothing. Lazy-view admission failure is terminal, never optional-view
fallback. Preserve B67 native-call packet funding, current-token checkpoints, first
refusal priority, logical nesting and managed stack admission. Conservative peak
retention/earlier denial require explicit documentation; no hidden allowance.

Stop after one feasibility decision. If all real consumers require per-call views,
borrows cannot remain valid without tracing, or the design merely repackages the same
objects, select no prototype and record why. A successful design must specify exact
representation/signature changes and focused semantic tests before implementation.
Do not stop because a regex producer release is missing.

B69 source gate PASSES for a bounded prototype (2026-11-01). Actual ordinary
positional binding already targets final locals; do not claim a duplicate copy.
Keep ordinary keyword/default/protocol binding in the existing binder. Eligible
positional compiled calls will bind into a driver-owned arena instead, using the
same default/error helper and a real lexical ExecutionContext. One active driver
saves its actual caller state in admitted records; no per-child interpreter or
operand/local array. Existing root, generator and reentrant protocol entries still
own separate drivers. Member/region/cell arrays remain where genuinely needed.

Exact migration: an internal local-slot window (standalone array or arena+offset)
replaces object[] only in executable helpers; saved records retain the full active
state. ExecutionContext holds a driver/depth handle and materializes the existing
ExecutableFrameState on semantic inspection. Cheap mirroring/active-slot checks
must not force a view. An arena resize changes its backing array, not offsets or
view identity. Parent views and independently retained closure cells keep working.
locals() copies remain separately admitted. A native/protocol callback enters a
separate driver; while an awaited native borrow is live, its owning driver cannot
resume/grow. Generator reentry remains rejected. No stale spans cross growth or
await; consume ordinary source operands only after destination admission.

Reserve major arena/record capacity plus replacement overlap before allocation.
Clear popped references, retain reusable capacity until the root driver/generator
actually closes, and keep pending async disposals awaited. Fund frame-only cells/
caches/region tables separately. First admission refusal remains terminal. Root
and callback drivers use their existing standalone binding paths; no new public
options or alternate engine. Preserve measured simple-return selection.

Prototype stopping gate: at most one coherent migration; no semantic fixture
relaxation, new policy or general binder rewrite. Stop/revert if the first focused
suite exposes broad unrelated contract migration (more than eight independent
failures), if stable views require retention tracing, or if removal leaves the
original per-call interpreter/operand/bound-local allocation intact. Preserve
independent externally observable regressions. Timing only after focused semantic,
real suspension, cancellation and admission/cleanup controls pass on both TFMs.

Frozen runtime-candidate gate: retained
complete CSV/text helper scripts, unchanged source/inputs/full stdout/files. One
before/candidate/identical-before comparison, shared CoreLib/hash witness, ordinary
GC/tiering,128 interleaved warmups/image,2s settle/case,seven rotating32-job batches,
<=45s owned service. >=7.5% elapsed gain for BOTH affected scripts against both
originals; <=3% valid JSON control regression, original drift<=3%, IQR<=10%, all
batches>100ms. Capture each completed case before proceeding, preserving partial
records on failure; do not repeat B66's all-at-end loss. One short allocation
comparison can verify that intended frame/array objects disappear; allocation share
alone is not latency gain. No retiming or threshold changes after collection.

Semantic acceptance: evaluation order/rebinding; arguments/defaults/keywords and
mutable function metadata; closures/locals/class cells; generators and real host/
protocol suspension; cancellation in nonallocating loops; admission before effects,
first-origin refusal, retained aliases and awaited cleanup. Use existing focused
Release controls; add only independent regressions for actual gaps. Full platform/
original Stress/package qualification only for retained code at a coherent boundary.

Frontend remains independent: scope facts/native UTF-8/native async operators and
small binding pools already exist. Annotation and general abstract interpretation
have different reachability, refinement and directive rules; their consolidation
would change diagnostics and needs an explicit contract, not dropping a pass or
adding another mode. No replay of closed parser/frontier/local-copy trials.
Track A's final immutable released-producer qualification remains independent.

## Completed milestones and evidence

| Milestone | Outcome |
| --- | --- |
| B67 DELIVERED | Native call-packet funding/cancellation, primed-capacity denial and cleanup. Full local3,966/15,607 per runtime; Windows/Ubuntu ordinary/original Stress/package consumers green41335606172 at9003d045. |
| B68 UNQUALIFIED | Phase diagnostic completes, subsequent capture exceeds6s; no retry or speedup. Current setup is small in these observed fresh jobs; saved compiler/guest samples remain diagnostic. [Evidence](benchmarks/results/2026-11-01-fresh-config-attribution/README.md). |
| B66 UNQUALIFIED | Compiler/static-layout and linear-engine candidate passes596/5,725 focused checks per target; single90s comparison times out with empty aggregate output. Entire prototype removed; independent call-packet admission fix retained. [Evidence](benchmarks/results/2026-11-01-static-operands-unqualified/README.md). |
| B65 CLOSED | One differential helper-source capture,9.153s within24s owned service; material whole-path effect, distributed attribution, no runtime candidate/speedup inferred. All output/ownership/cleanup controls pass. [Evidence](benchmarks/results/2026-11-01-helper-call-attribution/README.md). |
| B64 REJECTED | UTF-8 windows miss fixed text gain; helper timing additionally IQR73.64% inconclusive. All prototype code removed, four independent regressions retained;1,411/4,867 focused checks pass per target. [Evidence](benchmarks/results/2026-11-01-string-windows-rejected/README.md). |
| B63 UNQUALIFIED | Capacity proposal stopped at broad migration gate:100/803 initial native failures; no test changes/timing. Original restored,803 pass per target. [Evidence](benchmarks/results/2026-11-01-admission-capacity-unqualified/README.md). |
| B62 CLOSED | Source gate: native executable operators/indexing/truthiness and JSON UTF-8 paths already exist. No duplicate implementation, tests or timing. |
| B59 | Delete267-line legacy regex pass and duplicate flag checks; typed flow owns bindings, raw pattern syntax belongs to the provider.12 new controls/eight Python goldens. Modest compilation observations, no qualified speedup. [Evidence](benchmarks/results/2026-11-01-typed-regex-analysis/README.md). |
| B60 | Python PatternError constructor/mutable fields/stable args/original patterns/scalar positions; optional0.4 UTF-16 failure bridge.21 Python goldens/12 lifetime controls. CSV valid/no material regression; error timing inconclusive. Full delivery above. [Evidence](benchmarks/results/2026-11-01-pattern-error-metadata/README.md). |
| B61 REJECTED | One36s comparison: CSV+2.47%/+3.79%, text+3.61%/+2.12%, valid affected controls; JSON control IQR62.43% fails. Prototype removed/restored/tested; no replay or speedup claim. [All source, failures, gates and samples](benchmarks/results/2026-11-01-call-control-rejected/README.md). |
| B58 | Same-bits expanded helper expressions change complete CSV-16.31%/-17.88%, text-13.95%/-13.59%; valid controls. Material source effect, no runtime gain/removable fraction. [Evidence](benchmarks/results/2026-11-01-helper-cost-assessment/README.md). |

Current external guide measures830c6251, not HEAD: warm diagnostic gaps2.08x CSV/
4.93x text/1.84x JSON; fresh text is inconclusive. Keep its boundaries, compilation,
startup/I/O/cleanup limitations and historical adverse observations visible.
[Maintained guide](docs/BENCHMARKS.md); no new external ratio from internal trials.

Earlier completed work, experiments, rejected approaches and cached terminal runs
remain in .git/agent-notes/PLAN-history-through-B68.md and previous ledgers, with
immutable source/measurement evidence under benchmarks/results/. Do not reopen
B51/B53/B56/B57/B61 prototypes, poll historical CI or repeat unchanged adapter
qualification. Keep current docs concise; experimental narratives stay outside docs/.
