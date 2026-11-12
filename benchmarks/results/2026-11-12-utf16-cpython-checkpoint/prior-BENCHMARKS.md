# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 5,120 records / 68,625 B | 4.718–4.854 ms | 4.439–4.500 ms | 1.05–1.09× | `da9e5066` | Variable; L2.88%, P1.35% / 1.12% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.401–3.470 ms | 3.368 ms | 1.01–1.03× | `6f76` | Variable; 2.02% / 0.74% |
| Text summary/helpers | 3,072 lines /35,840 B | 1.295–1.323 ms | 1.290–1.308 ms | 0.99–1.03× | `983bab96` | Variable; L2.14%, P1.37% / 2.70% |
| Text / captured helper + counter | 3,072 lines /35,840 B | 1.595–1.612 ms | 1.034–1.045 ms | 1.53–1.56× | `983bab96` | Variable; L1.08%, P1.14% / 3.56% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.613–3.627 ms | 4.183–4.214 ms | 0.86–0.87× | `8ec7bfab` | Diagnostic; L0.37%, P0.72% / 0.63% |
| JSON-to-Markdown report | 3,072 records /189,441 B | 3.142–3.188 ms | 2.463–2.491 ms | 1.26–1.29× | `983bab96` | Variable; L1.46%, P1.14% / 3.61% |
| JSON-to-Markdown / UTF-16 output | 3,072 records /189,441 B | 3.655–3.690 ms | 2.630–2.650 ms | 1.38–1.40× | `983bab96` | Diagnostic; L0.94%, P0.76% / 1.94% |
| Config edit | 181 B / one JSON file | 140.3–140.8 µs | 124.2–127.7 µs | 1.10–1.13× | `ee44` | Variable; L0.32%, P2.67% / 3.45% |
| Protected file search | 2,049 lines / 42,010 B; stop after 1,025 | 0.318–0.320 ms | 0.216–0.219 ms | 1.45–1.48× | `8ec7bfab` | Variable; L0.56%, P1.41% / 2.65% |
| File search / keyword-default helper | 2,049 lines / 42,010 B; stop after 1,025 | 321.74–349.61 µs | 217.05–217.07 µs | 1.48–1.61× | `82ed92d4` | Variable; L8.66%, P0.01% / 2.35% |
| File search / guarded helper | 2,049 lines / 42,010 B; stop after 1,025 | 362.03–393.61 µs | 221.41–224.67 µs | 1.61–1.78× | `da9e5066` | Variable; L8.02%, P1.45% / 1.39% |
| Copy/checksum/manifest | two files /4,107 B | 0.384–0.384 ms | 0.260 ms | 1.47–1.48× | `4598` | Diagnostic; 0.14% /1.39% |
| Patch generation | Two 1,024-line UTF-8 files / 67,597 B | 0.556–0.558 ms | 0.647–0.651 ms | 0.85–0.86× | `4aa` | Diagnostic; L0.20%, P0.65% / 1.40% |

Timed rows measure **compilation plus one execution in an already-running host**.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** It includes
mediated RAM-file I/O, full output validation and prior-result cleanup. Startup,
fixture staging/reset, protocol and final cache teardown are excluded.

**Measured sources:** collected 2026-11-12: retained `8ec7bfab` for file search
and JSON-lines (all three phases), captured-text/report warm execution. Test-only
correction `beeb828e` leaves all 568 production sources and measured bits unchanged.
[Exact source proof and phase evidence](../benchmarks/results/2026-11-12-reader-cpython-checkpoint/README.md).
`da9e5066` remains CSV validation and guarded search; `983bab96` remains ordinary
text, captured-text/report compilation and compile-to-result, and UTF-16 report.
`82ed92d4` remains keyword-default search. All use stock CPython 3.13.16 on the same
quiet VM. Sources are labelled per row; they do not describe later HEAD.
Earlier rows remain historical: `4aa` = qualified `4aa2d46d` (patch, 2026-11-11),
`ee44` = `ee448715` (config/original search, 2026-11-10), `6f76` = `6f76f99d`
(CSV summary, 2026-11-08), `4598` = `459813a3` (manifest, 2026-11-09).
They do not describe current HEAD.

The [earlier record/search checkpoint](../benchmarks/results/2026-11-12-record-cpython-checkpoint/README.md)
and [earlier report/text checkpoint](../benchmarks/results/2026-11-12-output-cpython-checkpoint/README.md)
separate compilation, warm execution and compile-to-result. Guarded search,
captured text and reports retain material warm gaps. CSV is close and JSON-lines
is faster in these observations, with variability shown. All output, ownership,
quiet, joined-user and archival checks pass. Original/adverse controls and
replaced rows remain linked; cross-collection differences do not isolate a change.

The captured-helper variant wraps execution in a function and adds an observable
nonlocal counter. It is a distinct script shape, not an isolated counter-cost
comparison. Each engine runs the identical source; later HEAD keeps separate status.

Current ranges span two independent process medians **per engine**; ratio ranges
include all four pairings, not confidence intervals. Controls report the absolute
process-median difference for Lython (`L`) and CPython (`P`); max IQR covers both.
Older rows report Lython control spread. Diagnostic/Variable describe observed
stability, not design acceptance thresholds. Adverse and inconclusive cases remain
visible. Separate checkpoints cannot attribute a gain to one optimization.

## Warm execution, compilation and startup

Warm execution reuses the compiled script and excludes compilation:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 4.487–4.500 ms | 4.318–4.354 ms | 1.03–1.04× | `da9e5066` | Variable; L0.29%, P0.83% / 3.28% |
| CSV summary/helpers | 3.282–3.302 ms | 3.201 ms | 1.03–1.03× | `6f76` | Diagnostic; 0.60% / 0.68% |
| Text summary/helpers | 1.143–1.305 ms | 1.179–1.200 ms | 0.95–1.11× | `983bab96` | Variable; L12.40%, P1.74% / 1.10% |
| Text / captured helper + counter | 1.457–1.494 ms | 0.889–0.910 ms | 1.60–1.68× | `8ec7bfab` | Variable; L2.53%, P2.24% / 3.56% |
| JSON-lines/helpers | 3.512 ms | 4.082–4.101 ms | 0.86–0.86× | `8ec7bfab` | Variable; L0.01%, P0.47% / 2.69% |
| JSON-to-Markdown report | 3.102–3.130 ms | 2.540–2.594 ms | 1.20–1.23× | `8ec7bfab` | Variable; L0.90%, P2.05% / 3.37% |
| JSON-to-Markdown / UTF-16 output | 3.577–3.673 ms | 2.434–2.488 ms | 1.44–1.51× | `983bab96` | Variable; L2.60%, P2.21% / 4.49% |
| Config edit | 78.3–78.6 µs | 61.8–62.1 µs | 1.26–1.27× | `ee44` | Diagnostic; L0.41%, P0.43% / 2.18% |
| Protected file search | 0.246–0.248 ms | 0.159–0.160 ms | 1.54–1.56× | `8ec7bfab` | Variable; L0.78%, P0.36% / 3.07% |
| File search / keyword-default helper | 259.11–262.53 µs | 160.56–161.12 µs | 1.61–1.64× | `82ed92d4` | Diagnostic; L1.32%, P0.35% / 1.40% |
| File search / guarded helper | 303.22–320.86 µs | 161.25–167.20 µs | 1.81–1.99× | `da9e5066` | Variable; L5.82%, P3.55% / 1.81% |
| Copy/checksum/manifest | 0.243–0.247 ms | 0.155 ms | 1.56–1.59× | `4598` | Diagnostic; 1.72% /2.72% |
| Patch generation | 0.497–0.520 ms | 0.576–0.580 ms | 0.86–0.90× | `4aa` | Variable; L4.44%, P0.74% /1.16% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 68.86–69.87 µs | 65.53–65.68 µs | 1.05–1.07× | `da9e5066` | Diagnostic; L1.45%, P0.22% / 1.06% |
| CSV summary/helpers | 61.3–62.2 µs | 59.1 µs | 1.04–1.05× | `6f76` | Diagnostic; 1.46% / 0.89% |
| Text summary/helpers | 0.044–0.044 ms | 0.041–0.041 ms | 1.07–1.07× | `983bab96` | Diagnostic; L0.29%, P0.40% / 1.71% |
| Text / captured helper + counter | 0.045–0.045 ms | 0.054–0.054 ms | 0.83–0.84× | `983bab96` | Diagnostic; L0.09%, P0.93% / 1.97% |
| JSON-lines/helpers | 51.02–52.49 µs | 50.80–51.18 µs | 1.00–1.03× | `8ec7bfab` | Variable; L2.80%, P0.74% / 1.27% |
| JSON-to-Markdown report | 0.041–0.041 ms | 0.038–0.038 ms | 1.07–1.09× | `983bab96` | Diagnostic; L0.92%, P0.50% / 2.53% |
| JSON-to-Markdown / UTF-16 output | 0.041–0.042 ms | 0.039–0.039 ms | 1.07–1.08× | `983bab96` | Variable; L0.69%, P0.43% / 5.75% |
| Config edit | 36.5–37.2 µs | 34.2–34.4 µs | 1.06–1.09× | `ee44` | Diagnostic; L1.93%, P0.51% / 1.11% |
| Protected file search | 29.53–29.99 µs | 32.19–32.34 µs | 0.91–0.93× | `8ec7bfab` | Variable; L1.51%, P0.45% / 2.11% |
| File search / keyword-default helper | 29.52–29.89 µs | 32.33–32.58 µs | 0.91–0.92× | `82ed92d4` | Diagnostic; L1.24%, P0.77% / 2.04% |
| File search / guarded helper | 32.67–32.78 µs | 34.30–34.64 µs | 0.94–0.96× | `da9e5066` | Variable; L0.35%, P0.99% / 5.44% |
| Copy/checksum/manifest | 70.9–71.9 µs | 56.7 µs | 1.25–1.27× | `4598` | Diagnostic; 1.33% /0.62% |
| Patch generation | 33.46–33.91 µs | 33.86–34.21 µs | 0.98–1.00× | `4aa` | Diagnostic; L1.34%, P1.06% /0.62% |

Startup, RSS/heap peaks, genuinely pending I/O and impact on other embedding work
are unmeasured. Small file-backed scripts do not establish large-file throughput.

## Measurement boundary and fairness

Dedicated Azure F4as_v7, Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap.
Actual .NET 11 RC3 and isolated CPython 3.13.16 use ordinary GC/tiering;
five-second warmups per role/job and seven rotated 250 ms rounds for `8ec7bfab`
(frozen `641c3f75`); captured-text warmup is twelve seconds. Earlier CSV/guarded
search use 150 ms rounds and `a55202fb`/`da9e5066`; earlier report/text phases
retain `b2cbfdb6`/`983bab96` as labelled. Keyword-default search keeps
`0106a711`/`82ed92d4`. All reuse common worker `fec79afd` and adapter 0.4.
Other rows retain their recorded images.
These observations do not qualify a newer regex producer.

Identical scripts and fixtures run on both engines, with complete stdout/file
digests checked every invocation. Lython uses efficient mediated filesystem
overrides; CPython uses builtin open. Lython opens include capability and path
component checks. CPython retains imported modules; Lython isolates execution
imports. Warm includes invocation and prior-result cleanup; compilation includes
construction/disposal. Independent phase medians need not add up. External process
CPU also includes reset/protocol and cannot establish an internal CPU cost.
Tables use synchronous Run; completed-I/O async diagnostics do not establish
pending-I/O latency. This is an embedding comparison, not an isolated CPU kernel.

## Coverage and uncertainty

The [patch-generation checkpoint](../benchmarks/results/2026-11-11-patch-workflow/README.md)
completes four controlled jobs in 114.763 seconds against CPython 3.13.16, using
qualified `4aa` source and frozen `9ee7e700`. Identity, output, ownership, quiet,
joined-worker, cleanup and archive gates pass. Its single-central-edit fixture
does not establish general diff throughput. Warm Lython controls differ by 4.44%;
compile-to-result controls are tighter. The unchanged config warm control is
1.26–1.27× CPython; this does not qualify config compilation at the newer source.
Earlier CPython 3.13.2 correctness smoke and all its unqualified clocks survive.

The source-labelled tables above are authoritative. Captured text, helper search
and reports retain material warm gaps; CSV is close to CPython and JSON-lines is
faster in these observations. Variable controls remain visible. Source-pair gains
cannot be multiplied into external ratios, and comparisons across collections do
not isolate the cause of a change.

Additional reportAsync source-pair widerCPU0..+6.25% versus8f8628a4 remains accepted
uncertainty; its elapsed overlaps zero. Earlier8f8628a4 report debt has a different
reference. Do not compound/equate those ranges; these synchronous external rows do
not settle either async issue. Allocation reduction alone does not establish CPU
gain, and independently measured compilation/warm medians need not sum.

Original literal-prefix search and config retain `ee44`; CSV summary retains
`6f76`; the small-file manifest retains `4598`. File search stops at line 1,025
of 2,049. Earlier text rows remain in the linked e543 evidence. Larger-tree throughput, pending I/O, RSS and embedding tail latency remain
unqualified. The [previous guide](../benchmarks/results/2026-11-10-construction-cpython-checkpoint/prior-BENCHMARKS.md)
preserves replaced measurements and their source-specific qualification.

## Improvements within Lython

CPython remains the primary external baseline. Separate source pairs below are
not additive or universal CPU gains:

| Change / affected work | Established result | Limits / evidence |
| --- | --- | --- |
| Bulk UTF16 output | Complete UTF16 report warm elapsed −1..−8% Run /−2..−5% RunAsync; allocation flat | Bulk encoder replaces manual scalar emission; three dormant helpers removed. Sync CPU uncertain; UTF8 control elapsed−0.36..+2.11% /CPU0..+1.25% accepted debt, no equivalence/CPython claim. Fully qualified41bd869b. [Both forms](../benchmarks/results/2026-11-12-bulk-utf16/README.md), [delivery](../benchmarks/results/2026-11-12-bulk-utf16/delivery/README.md) |
| Prepared cased-word ranges | Allocation flat; captured text warm elapsed −1..−5% Run /−3..−5% RunAsync | Removes the second whitespace scan. Wider CPU/control movement limits attribution; no substantial CPU or CPython claim. Focused/quiet and full platform/stress/consumer qualification pass on1011ed0b. [Source pair](../benchmarks/results/2026-11-12-prepared-word-ranges/README.md) |
| Native operation metadata | Node storage72→48B (−33%); affected whole-script compile allocation−0.06..−0.18%, warm flat | Ordinary instruction/opcode/third word removed. Quiet reference spreads6–8% leave CPU uncertain; no speedup claim or CPython update. [Source pair and qualification](../benchmarks/results/2026-11-12-native-operation-facts/README.md) |
| Immutable reader windows / search, text, JSON | Warm allocation −18% search / −5% text and JSON; controlled search elapsed −2..−6%, supporting CPU | Full-window command/result retention can deny earlier. Report elapsed +0.6..+2.8% is accepted debt; text/JSON CPU uncertain. [Source pair, confirmation and qualification](../benchmarks/results/2026-11-12-reader-windows/README.md) |
| Negated helper predicates / complete guarded search | Allocation flat; quiet Run elapsed −28.45..−22.30% / RunAsync −25.05..−14.58%, supporting CPU | Compiler-only form versus qualified `983bab96`; compile allocation +1.02%. Native-search Run CPU +0.46..+1.54% accepted; captured-text timing remains uncertain with candidate control drift. Windows/Ubuntu, both targets, original stress and installed consumers pass. [Source pair](../benchmarks/results/2026-11-12-conditional-helper-bodies/normalized/README.md) |
| Captured-cell assignment / captured text | Allocation flat; quiet elapsed−8.91..−3.17% Run / −6.96..−4.75% RunAsync, supporting CPU | Versus`7114021e`; ordinary control CPU+0.77..+1.54% accepted, report elapsed variable. Full delivery separate. [Source pair](../benchmarks/results/2026-11-12-cell-write-authority/README.md) |
| Completed ordinary helper calls / guarded search, RunAsync | Allocation flat; quiet elapsed−8.61..−6.75%, supporting wider CPU−7.20..−1.46% | `7114021e` versus`82ed92d4`; synchronous guarded elapsed/report/CSV uncertain. Windows/Ubuntu .NET11/10, original stress and installed consumers pass. [Source pair](../benchmarks/results/2026-11-12-completed-call-lifecycle/README.md) |
| Shared canonical receiver operations / guarded search | Allocation−71%; quiet elapsed−23..−30% with supporting CPU | Retained82ed92d4 versus2ec. Existing native-search/capturedRun CPU debt remains; Windows/Ubuntu .NET11/10, original stress and installed Release consumers pass. [Source pair](../benchmarks/results/2026-11-12-receiver-operations/refined/README.md) |
| Native storage and borrowed call inputs / complete text | Allocation−20.43–22.44%; quiet captured Run elapsed−8.74..−4.66%, ordinary Run−9.68..−8.54%/RunAsync−12.48..−9.49%, supporting CPU | Retained2ec9d67f versus8f8628a4. Captured async CPU uncertain; earlier search/CSV adversity not reproduced without a cause claim. Additional reportAsync CPU0..+6.25% uncertainty accepted; fully qualified on Windows/Ubuntu .NET 11/10, stress and package consumers. [Source pair](../benchmarks/results/2026-11-12-native-inputs/README.md) |
| Native dictionary integer storage / complete text, CSV and JSON-lines | CLR allocation −14.90–16.13% text, −3.13% CSV, −4.20% JSON-lines on both APIs; refined quiet text Run elapsed direction improves | Fully qualified `8f8628a4` production via corrected `458be840`. Captured Run −3.81–9.06%, ordinary −2.62–8.66%, supporting wider CPU with moving controls. CSV elapsed inconclusive; report wider CPU +1.91..+6.38% remains accepted debt. All desktop adversity and initial archival failure/recovery retained. [Source pair](../benchmarks/results/2026-11-11-dictionary-integer-storage/README.md) |
| Captured native helper sequences / complete text | Quiet elapsed −10.56–15.17% Run / −16.59–18.16% RunAsync, supporting wider CPU; runtime allocation flat | Fully qualified `6904093e` versus `5f9059fa`. Ordinary text inconclusive, CSV control +0.49..+0.89%, compile allocation +0.39–0.93%. Initial adverse implementation and refinement preserved. [R243](../benchmarks/results/2026-11-11-native-closure-sequences/README.md) |
| Shared compiled workspace / local-record CSV | CLR allocation −4.92% both APIs; quiet affected elapsed direction improves | Fully qualified `5f9059fa` versus `b584`. Original desktop slowdown does not recur, cause unknown; ordinary CSV control +0.97..+2.83% remains uncertain. [Source pair](../benchmarks/results/2026-11-11-mirrored-function-storage/README.md) |
| Private integer totals / complete text helpers | CLR allocation −9.04% captured / −9.71–9.72% ordinary text on both APIs; ordinary sync improves in two quiet collections | Fully qualified `b5849b04` versus `8314`. Captured/async CPU uncertain. Report adversity does not recur on confirmation, cause unexplained. [R240](../benchmarks/results/2026-11-11-private-counter-segments/README.md) |
| Native helper-return expressions | Allocation −19.92%sync/−19.31%async | Original elapsed improvements are diagnostic; later guarded helper source pairs are separate. [History](../benchmarks/EXPERIMENTS.md) |
| Direct UTF8 formatting/storage | Allocation −8.67%; controlled elapsed/CPU−5–8% | Original adverse15.125ms measurement remains unexplained, not erased by later controls. [History](../benchmarks/EXPERIMENTS.md) |

Detailed older source pairs remain in the [prior guide](../benchmarks/results/2026-11-12-native-inputs-cpython-checkpoint/prior-BENCHMARKS.md).
Other delivered source pairs, rejected designs, detailed qualification and original
failed conditions remain in [experimental history](../benchmarks/EXPERIMENTS.md),
the [previous guide](../benchmarks/results/2026-11-08-primitive-cpython-checkpoint/prior-BENCHMARKS.md)
and [benchmark tooling](../benchmarks/README.md). Sampled attribution is workload-specific:
[text](../benchmarks/results/2026-11-09-text-runtime-attribution/README.md) finds native
helper guards/evaluation small, while [protected search](../benchmarks/results/2026-11-09-search-runtime-attribution/README.md)
finds roughly 15% in those categories and little frame construction. Categories
include useful inlined work; these are not removable-cost estimates or predicted gains.
