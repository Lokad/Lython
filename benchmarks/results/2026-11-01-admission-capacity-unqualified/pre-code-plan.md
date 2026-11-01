# Lokad.Lython 0.11.0 — active deliverables

Updated **2026-11-01** (Paris). Two independent tracks: missing producer APIs
block only final optional-adapter qualification. They do not block core work,
correctness checks or independent performance development. No publication/tag.
PLAN.md intentionally remains Git-ignored; never force-add it.

## Current checkpoint

Production **35d8942c** is DELIVERED. Terminal green **41319281716** covers
Windows/Ubuntu ordinary and original stress on both .NET targets and explicit
Release installed-package consumers. Terminal proof is cached; never query again.
Local full ordinary3,962 native/15,607 public per actual.NET11RC3/10.0.8 and both
installed-package consumers pass. Canonical unpublished0.11 packages identify
35d8942c. Published0.10 hashes and peeled v0.10.0 are unchanged.

B61's call-control prototype is REJECTED and entirely removed. Five original
production files are restored byte-for-byte, fresh nonincremental core/worker/
test/probe copies equal the original frozena067c8a3 hash.849 native/4,073 public
focused checks pass on both actual runtimes after restoration. No tests weakened
or added; do not repeat unchanged full qualification for evidence-only commits.

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

## Track B — B63: coarse shared-reservation capacity

B62's source gate closes WITHOUT a candidate or timing: Executable.Interpreter
already routes exact binary/augmented operands, unary arithmetic and common native
indexing through synchronous kernels before async helpers. JumpIfFalse does the
same for non-PyInstance values; IsTruthyAsync already has a non-async native wrapper.
JSON strings/numbers also already use native UTF-8 paths. Do not duplicate these
routes, reopen B51/B53/B61 or infer a new redesign from stale helper signatures.

B63 hypothesis: the existing aggregate governor resizes its host reservation on
EVERY nonzero reserve/refund, including repeated token/working-storage changes.
Saved JSON/CSV native origins show material monitor traffic across these real
paths (B39/B43), not proof of a removable fraction. The governor's resize contract
and host-budget implementation remain source-equivalent. Test one coherent
capacity invariant instead of another per-method accounting tweak.

Before code, concrete replacement:
- Keep existing reserved/committed balances, allocation preflights and one owned
  aggregate reservation. Track its admitted capacity, rounded UP in4KiB units.
  Call the parent only when that capacity changes. Replace exact per-change parent
  resizes in Reserve/Release/ReleaseReserved; no second budget, cache, pool, ledger,
  mode/configuration, collector, identity tracking, weak reference or forced GC.
- Capacity always funds logical reserved+committed storage. Round without overflow;
  zero logical storage returns capacity to zero. Refund every whole unused unit
  as storage is relinquished. No cumulative counting or peak-only retained grant.
  Scope creation/zero reservation still fund nothing; no supplied budget still
  means unbounded standalone allowance, not a hidden replacement limit.
- Intentional compatibility tradeoff: at most4,095 extra bytes per governor owner
  remain charged to THE SAME borrowed root. Tiny thresholds can reject earlier;
  sub-unit refunds may wait until the whole unit is unused. Whole owner cleanup
  returns its balance independently of CLR GC. Retained aliases/cache owners stay
  funded. Report actual admitted peak including headroom; preserve first structured
  refusal/span and the original storage estimate. Never retry a refused rounded
  request at a smaller size, skip storage, or resume guest work after refusal.
- Keep useful logical rollback/overcommit/overrelease tests. Replace only assertions
  requiring root bytes to equal unrounded logical totals or tiny-budget setup;
  retain meaningful funding, first-origin, no-mutation, cleanup and reuse checks.
  If correctness requires broad changes or real funding/cleanup contracts fail,
  restore and stop rather than grow a new ownership framework.

Acceptance: two executions sharing the host root, actual preflight refusal before
allocation/effects, cancellation while users are held, terminal first failure even
with cached headroom, retained results/caches and complete cleanup to baseline.
Exercise repeated temporary storage so reservation stays proportional to bounded
live working storage, not row/allocation count. Existing CSV/text/JSON host-batch
coverage remains. Focused Release both actual runtimes; add only missing externally
meaningful coverage, not resize-call counters or exact quantum/checkpoint tests.

ONE<=60s same-process before/candidate/identical-before service using existing
complete CSV-helper/text-helper/JSON workloads, hosts, fixtures and driver.
128 interleaved warmups/image,2s settle/case, seven rotating32-job batches; ordinary
GC/tiering, shared CoreLib/actual hashes, byte-identical auxiliaries. Warm boundary
includes fixture reset, mediated I/O, full stdout/file goldens, result disposal and
balance/cleanup checks; excludes compile/startup. Text is a contrasting workload,
not falsely described as unaffected by this system-wide change. Require>=7.5%
elapsed improvement for BOTH CSV and JSON against BOTH originals, <=3% valid text
regression, original drift<=3%, IQR<=10%, batches>100ms and all quiet/golden/ownership
checks. Preserve all adverse/inconclusive samples; no phase subtraction, CPython
ratio, favorable retiming or allocation-only speedup. If fixed gates fail, restore,
fresh-build/test and stop this candidate. Full platform/original stress/package
qualification only for retained code at a coherent boundary. Producer APIs do not
gate this independent work.

## Completed milestones and evidence

| Milestone | Outcome |
| --- | --- |
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
remain in .git/agent-notes/PLAN-history-through-B61.md and previous ledgers, with
immutable source/measurement evidence under benchmarks/results/. Do not reopen
B51/B53/B56/B57/B61 prototypes, poll historical CI or repeat unchanged adapter
qualification. Keep current docs concise; experimental narratives stay outside docs/.
