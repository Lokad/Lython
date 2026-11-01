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

## Track B — B64: immutable UTF-8 windows

B62 closed from current source: native operators/indexing/truthiness and JSON UTF-8
already exist. B63 stopped before timing: its aggregate capacity proposal needs a
broad compatibility/coverage migration (100/803 native failures); original restored,
803 checks pass per actual target, no test changes or qualification replay. Neither
is evidence of a speedup or a demonstrated funding bug. Do not tune/reopen them.

B64 source hypothesis: PyString owns a whole byte[] and SliceByByteRange always
allocates/copies. Split/strip/partition/slicing and native line processing multiply
these copies. Existing text allocation/native origins and real source confirm the
mechanism, not a predicted removable fraction. Replace contiguous text storage
with native ReadOnlyMemory<byte> windows while preserving whole-buffer admission.
This differs from rejected casing, whitespace, JSON-decoder and call-record trials.

Concrete replacement/deletion BEFORE CODE:
- Change PyString's internal payload to ReadOnlyMemory<byte>; update native byte
  access/copy/encoding to respect window offsets. Remove the byte allocation/copy
  from governed contiguous slicing. Retain copies for unowned/foreign memory,
  unknown external memory, noncontiguous slices and existing ASCII interning.
  No new public type/provider dependency, storage framework, owner API or switch.
- A first governed view preflights its128-byte shell PLUS32 bytes/backing-byte
  capacity as an independently batch-retained shared-storage estimate through the
  EXISTING governor. One intrinsic flag records that backing admission; descendant
  views borrow that admitted capacity and preflight only their own shells/caches.
  No object identity tables, scalar coupons/refcounts, tracing, weak references or GC.
- Owned strings retain their existing refundable construction charge; a view's
  refund contains only its shell/caches. The backing grant belongs to the existing
  batch owner independently of the private source. Existing explicit window/cache/
  unpublished-source refunds can therefore finish without refunding live view data.
  Do not rely on keeping a source scope alive accidentally or disabling rollback.
- Intentional tradeoff: first slicing may deny earlier because a tiny view retains
  and pre-funds an entire immutable source buffer. Shared backing is conservatively
  funded until batch/result owner cleanup, including after view publication fails.
  This may temporarily overlap original funding. Host-driven batches bound this
  retention; no cumulative allocation counter or exact reference-withdrawal promise.
  A refused admission happens before view allocation/publication/source progress;
  no retry, fallback after refusal, skipped storage or new hidden allowance.

Source/contract gate: verify immutable owned input, relative byte/scalar boundaries,
original/refundable charge separation and the three existing direct PyString charge
consumers (private text windows, warnings, cache-key names). If funding needs a
broad owner inventory/ledger or semantics removal, stop instead of expanding scope.
Keep the existing core/provider UTF-8 boundary and unknown-memory copy contract.

Acceptance: Python slice/split/strip/partition values, supplementary Unicode/NUL,
relative rune caches/offsets, hashing/equality/order and nested/noncontiguous views;
private-source cleanup while a view survives, refused view before source mutation,
retained aliases/results, actual held async users/cancellation, first refusal and
shared-budget/batch reuse. Existing file/CSV/text/JSON/regex semantics and original
stress remain. Add only missing value/lifetime regressions, not private flag tests.
Focused Release both actual runtimes before timing; full platform/original stress/
package consumers only for retained code at delivery.

ONE<=60s same-process before/candidate/identical-before service. Complete maintained
text helper AND ordinary-text scripts are affected; CSV helper and JSON contrast
workloads guard representation regressions. Existing hosts/fixtures/full stdout/file
goldens,128 interleaved warmups/image,2s settle/case, seven rotating32-job batches,
shared CoreLib/actual hashes, identical auxiliaries, ordinary GC/tiering. Warm public
execution includes reset/mediated I/O/results/disposal/balances; excludes compile/
startup. Require>=7.5% elapsed improvement for BOTH text shapes against BOTH originals,
<=3% valid CSV/JSON regression, original drift<=3%, IQR<=10%, all batches>100ms and
all quiet/ownership/cleanup gates. Keep every adverse/inconclusive sample. No timing
retry, threshold change, phase subtraction, new CPython ratio or allocation-only
speedup. On failed source/semantic/performance gates restore, fresh-build/test and
stop. A newer regex producer never gates this independent work.

## Completed milestones and evidence

| Milestone | Outcome |
| --- | --- |
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
remain in .git/agent-notes/PLAN-history-through-B63.md and previous ledgers, with
immutable source/measurement evidence under benchmarks/results/. Do not reopen
B51/B53/B56/B57/B61 prototypes, poll historical CI or repeat unchanged adapter
qualification. Keep current docs concise; experimental narratives stay outside docs/.
