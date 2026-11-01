# Lokad.Lython 0.11.0 — active deliverables

Updated **2026-11-01** (Paris). Two independent tracks: missing producer APIs
block only final optional-adapter qualification. They do not block core work,
correctness checks or independent performance development. No publication/tag.
PLAN.md intentionally remains Git-ignored; never force-add it.

## Current checkpoint

Production **d703767c** is DELIVERED (core source unchanged from9003d045;
ordinary optional-adapter error-source correction retained). Terminal green
**41343479748** proves Windows/Ubuntu ordinary Release.NET11/10, original Stress
on both targets and explicit Release installed-package consumers. Cached terminal
proof in .git/agent-notes/review-20261031/adapter06 and the A06 evidence is final;
never query again. The prior terminal41335606172 remains historical and cached.
Local focused adapter checks84 native/424 public per target pass on ordinary0.4
and isolated dev.6, with six actual dev.6 cancellation probes. Local full9003
checks3,966 native/15,607 public per target and package proof remain historical;
no unchanged local full run is repeated. Later commits contain evidence and independent regressions only.
B72 rejection/regressions committed as081a2280; no runtime prototype remains.
Canonical local unpublished0.11 packages still identify9003d045; they were not
regenerated for this adapter-only change. Published0.10/tag remain unchanged.
No publication/tag or adapter package is authorized.

B61/B63/B64/B66/B69 prototypes are absent. B67's independent native-call packet
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

A06 development qualification COMPLETE against exact0.5.0-dev.6, producer
825ed1f7c4b4eb54f0e092da495396d93b435a48. PythonRe SHA256
`ffbfdcd07f85b44388b2d4352e36b0de1eafe641042c04c13c00c4a37ed928c4`;
base`6ed5a8071923966e59315883d5e7cc870488c82f1422902b837cf6a5f32f30dc`.
424 public/84 native per actual runtime pass for ordinary0.4 and private dev.6.
Six actual isolated dev.6 compile/match/cursor probes prove in-flight cancellation,
original token, joined cleanup and zero allowance. Immutable API review confirms
native UTF-8 name lookup/pattern copy, but NOT name enumeration or one-shot ranges.

The new producer exposed an adapter conversion: B60 eagerly accessed Pattern on
successful calls, decoding UTF-8 solely for potential error metadata. Pass a handle
and read its text only in the pattern-error catch. No delegate/cache/new policy,
core change, ordinary pin change or publication. The compatible ordinary adapter
correction is DELIVERED atd703767c; terminal green41343479748 covers both platforms/
targets, original Stress and explicit Release consumers. Never requery it.
One fixed dev.6 eager/fixed/identical-eager allocation comparison passes: split
-5.32%, sub-8.91%, expand-72B, text unchanged. This is allocation evidence, not a
latency gain; ordinary0.4 already stores Pattern text. The separate supplied-pin
comparison records+72B per regex job for dev.6, unchanged text. No retiming.
[All source, precise gaps, tests, actual probes and both diagnostics](benchmarks/results/2026-11-01-development-producer-dev6/README.md).
Do not repeat unchanged development artifacts. Only final released pin/qualification
waits for a new immutable released package:

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

## Track B — substantial non-regex performance work

B72 REJECTED and fully removed: one52.877s controlled comparison improves the
actual integer loop13.45–13.94%, but complete text regresses1.25–2.78% and fails
the required5% workload gain. All controls/goldens/ownership/cleanup pass. No
retiming, narrower acceptance or instruction-count gain claim. Fresh nonincremental
restoration and706 native/2,977 public checks pass per actual runtime. Independent
local admission and suspended operand/cancellation regressions remain.
[Complete source/results](benchmarks/results/2026-11-01-local-updates-rejected/README.md).
This closes instruction fusion as the next whole-workload answer.

B73 CLOSED: ordinary explicit named calls do use a nested body runner, but one
6.019s/20.098s capture finds only+1.77% CSV/+0.56% text guest CPU/job, below the
frozen15% selection threshold. Full Python/Lython goldens, quiet gates, final0
budgets, joined users/profilers and fixture removal pass. No runtime prototype or
qualified timing claim; no recapture. [Evidence](benchmarks/results/2026-11-01-named-call-assessment/README.md).

B74 IN PROGRESS bounded bulk-record redesign: native matched-width string-header CSV
DictReader construction. Current complete CSV profiles record this region under
83/194 positional guest chains (parser descendants overlap32). These are NOT
removable fractions. Source shows per-record generic field-name cursor/iteration,
repeated async field access/boxed length, per-key ambient entry and incremental
table construction even for ordinary exact list-of-string headers and equal-width
native rows. Hypothesis: keep that case bulk, cutting real record scaffolding
and repeated capacity growth across complete CSV scripts; do not change Python
call frames, scalar representation, parser dialect or ownership infrastructure.

Exact eligibility before any construction: fieldnames is exact PyList, same count
as parsed row, all keys exact PyString. Check cancellation/refusal during the
nonallocating header guard. Otherwise use the existing general implementation
without allocating or trying a candidate path. Qualified records use existing
PyDictStorage.Create(count, governor, span) and PyDict wrapper, one actual ambient
scope and direct key/value traversal, preserving duplicate-key insertion order.
Reuse existing capacity preflights/headroom. Preallocation retains the full field-
width upper bound, including duplicate names; values/order stay Python-correct,
but conservative unused capacity can cause earlier budget rejection. No tiny-
budget threshold identity is promised. No cache, new cap, retry after denial,
per-object ledger, parallel work or delayed user. Existing mismatched/custom-key/
iterator/suspending protocols remain untouched and never run twice.

Verified focused candidate checks pass337 native/1,352 public per actual target,
after fresh nonincremental Release solution build (zero warnings/errors). The new
header-type/retained-row transition case matches isolated CPython3.13.2 and is
explicitly present/passing in BOTH actual public TRX files. An earlier incremental
.NET10 canonical run omitted it after an edit overlapped the prior multi-target
build; those39/38 results are preserved and superseded. No assertion was relaxed.
Runtime source changed in one existing module; no timing has started.

Acceptance: default/explicit/duplicate/Unicode headers, retained rows/header
aliases, header mutation between records, mismatched widths/restkey/restval,
custom hash/eq/iteration and real asynchronous inputs. Preserve observed line_num,
first-origin terminal admission and allocation-before-mutation rules, cancellation,
reader closure, shared-budget cleanup/reuse. Existing focused CSV/native/storage/
refusal/cancellation checks on actual.NET11/10 before timing; add only uncovered
independent behavioral regressions. Earlier rejection on a tiny-budget threshold
alone is not a new contract, but no containment assertion is weakened.

Freeze before code: ONE <=60s owned process/service, original/candidate/identical
original images sharing CoreLib/hash witness and unchanged auxiliary bits, normal
GC/tiering. Full unchanged CSV plain and helper sources/inputs/stdout/files;
complete text helper control.256 interleaved warmups/image,2s settling/case,
seven rotating32-job batches. Every job's result disposal/fixture reset/mediated
I/O/full goldens and balance checks stay in the warm boundary; compile/startup
outside. Require >=7.5% elapsed improvement in BOTH complete CSV cases against
BOTH originals; text regression/original drift<=3%, IQR<=10%, all batches>100ms.
No retiming or rescue changes. If it misses, remove all new runtime machinery,
keep evidence/independent regressions and fresh nonincremental restoration proof.
Full ordinary/stress/platform/package qualification only for retained delivery.
Stop if supporting the simple bulk path needs a general protocol/ownership rewrite.

Frontend annotation and general abstract interpretation have different reachability,
refinement and directive contracts; do not drop a pass based only on pass counts.
Preserve B67 and all delivered semantic fixes. No replay of closed B51/B53/B56/
B57/B61/B63/B64/B66/B69/B72 prototypes. Missing producer APIs never stop this track.

## Completed milestones and evidence

| Milestone | Outcome |
| --- | --- |
| B73 CLOSED | Named-call routing differs, but fixed selection gate misses (+1.77% CSV/+0.56% text diagnostic CPU/job); no runtime candidate or retiming. Full goldens/ownership/cleanup pass. [Evidence](benchmarks/results/2026-11-01-named-call-assessment/README.md). |
| B72 REJECTED | Valid loop gain13.45–13.94% does not reach complete workloads; text regresses1.25–2.78%. All prototype code removed, independent regressions retained;706/2,977 focused cases pass per runtime after fresh restoration. [Evidence](benchmarks/results/2026-11-01-local-updates-rejected/README.md). |
| A06 DELIVERED / DEVELOPMENT | Ordinary0.4 correction delivered green41343479748 atd703767c; exact dev.6 supplied pair qualified privately. Ordinary adapter defers error-only pattern text.424 public/84 native per runtime and six actual probes pass. Split/sub allocation-5.32%/-8.91% at dev.6; no latency claim/final release qualification. [Evidence](benchmarks/results/2026-11-01-development-producer-dev6/README.md). |
| B71 COMPLETE | Actual loop/control capture6.006s in14.078s; full gates, final budgets0 and users joined. Source proves repeated local-update instruction transport; no universal scalar rewrite or latency claim. [Evidence](benchmarks/results/2026-11-01-scalar-dispatch-attribution/README.md). |
| B70 SOURCE GATE | Numeric report measures JSON/native sum rather than per-item guest arithmetic. Source unchanged since B52; no redundant capture or numeric rewrite. Native casing and rejected B37 write consolidation remain closed. |
| B69 REJECTED | Actual call-storage elimination passes1,131/3,821 focused cases per runtime, but valid affected jobs regress5.0–6.8%; JSON control inconclusive. Prototype removed; independent lifetime regression retained. [Evidence](benchmarks/results/2026-11-01-call-arena-rejected/README.md). |
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
remain in .git/agent-notes/PLAN-history-through-B73.md and previous ledgers, with
immutable source/measurement evidence under benchmarks/results/. Do not reopen
B51/B53/B56/B57/B61 prototypes, poll historical CI or repeat unchanged adapter
qualification. Keep current docs concise; experimental narratives stay outside docs/.
