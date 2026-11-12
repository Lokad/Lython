# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 5,120 records / 68,625 B | 4.682–4.685 ms | 4.438–4.446 ms | 1.05–1.06× | `2ec9d67f` | Diagnostic; L0.05%, P0.18% / 0.93% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.401–3.470 ms | 3.368 ms | 1.01–1.03× | `6f76` | Variable; 2.02% / 0.74% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.292–1.298 ms | 1.261–1.286 ms | 1.00–1.03× | `2ec9d67f` | Diagnostic; L0.43%, P1.91% / 0.60% |
| Text / captured helper + counter | 3,072 lines / 35,840 B | 1.633–1.686 ms | 1.035–1.040 ms | 1.57–1.63× | `82ed92d4` | Variable; L3.30%, P0.50% / 1.72% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.615–3.633 ms | 4.208–4.249 ms | 0.85–0.86× | `2ec9d67f` | Diagnostic; L0.50%, P0.98% / 0.77% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.125–3.205 ms | 2.477–2.490 ms | 1.26–1.29× | `2ec9d67f` | Variable; L2.48%, P0.51% / 4.02% |
| Config edit | 181 B / one JSON file | 140.3–140.8 µs | 124.2–127.7 µs | 1.10–1.13× | `ee44` | Variable; L0.32%, P2.67% / 3.45% |
| Protected file search | 2,049 lines / 42,010 B; stop after 1,025 | 0.290–0.297 ms | 0.209–0.211 ms | 1.37–1.42× | `ee44` | Variable; L2.50%, P0.81% / 6.87% |
| File search / keyword-default helper | 2,049 lines / 42,010 B; stop after 1,025 | 321.74–349.61 µs | 217.05–217.07 µs | 1.48–1.61× | `82ed92d4` | Variable; L8.66%, P0.01% / 2.35% |
| File search / guarded helper | 2,049 lines / 42,010 B; stop after 1,025 | 454.23–463.01 µs | 224.05–224.46 µs | 2.02–2.07× | `82ed92d4` | Diagnostic; L1.90%, P0.18% / 2.16% |
| Copy/checksum/manifest | two files /4,107 B | 0.384–0.384 ms | 0.260 ms | 1.47–1.48× | `4598` | Diagnostic; 0.14% /1.39% |
| Patch generation | Two 1,024-line UTF-8 files / 67,597 B | 0.556–0.558 ms | 0.647–0.651 ms | 0.85–0.86× | `4aa` | Diagnostic; L0.20%, P0.65% / 1.40% |

Timed rows measure **compilation plus one execution in an already-running host**.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** It includes
mediated RAM-file I/O, full output validation and prior-result cleanup. Startup,
fixture staging/reset, protocol and final cache teardown are excluded.

**Measured sources:** `82ed92d4` for captured text and the two helper-search
variants, collected2026-11-12 with frozen core `0106a711`; `2ec9d67f` for the other
four refreshed scripts, frozen core `c9829194` (2026-11-12, began2026-11-11UTC).
Both have precommit assembly metadata and exact matching production source hashes.
Stock CPython3.13.16 runs on the same VM. Full Windows/Ubuntu .NET11/10, original
stress and installed Release consumers pass for both runtime revisions;
[latest exact-source delivery](../benchmarks/results/2026-11-12-receiver-operations/delivery/README.md).
Earlier rows remain historical: `4aa` = qualified `4aa2d46d` (patch, 2026-11-11),
`ee44` = `ee448715` (config/original search, 2026-11-10), `6f76` = `6f76f99d`
(CSV summary, 2026-11-08), `4598` = `459813a3` (manifest, 2026-11-09).
They do not describe current HEAD.

The later [cell-assignment source pair](../benchmarks/results/2026-11-12-cell-write-authority/README.md)
reduces captured-text elapsed by3.2–8.9% (`Run`) and4.8–7.0% (`RunAsync`), with
supporting CPU and flat allocation. A small ordinary-text CPU increase is accepted;
report elapsed remains variable. It has no new CPython rows.

Later completed-call lifecycle changes (`7114021e`) have a
[separate source-pair comparison](../benchmarks/results/2026-11-12-completed-call-lifecycle/README.md):
guarded search `RunAsync` elapsed is6.7–8.6% lower with supporting CPU, allocation
flat. Synchronous guarded elapsed and report/CSV remain uncertain. They have no
new CPython rows; the tables retain their measured revisions.

The [affected-script checkpoint](../benchmarks/results/2026-11-12-receiver-cpython-checkpoint/README.md)
finishes9jobs/247.974s with all validation/ownership/quiet/join/archive gates.
Guarded search remains2.30–2.33×CPython warm despite a
[separate source-pair improvement](../benchmarks/results/2026-11-12-receiver-operations/refined/README.md)
of23–30%elapsed and71%allocation. Existing native-search/captured CPU debt remains
explicit. Different collections cannot isolate change attribution.

The [six-script checkpoint](../benchmarks/results/2026-11-12-native-inputs-cpython-checkpoint/README.md)
refreshes compilation, warm execution and compile-to-result with existing workers
and unchanged sources/fixtures. All identity/output/ownership/quiet/join/archive
gates pass. Its [prior guide](../benchmarks/results/2026-11-12-native-inputs-cpython-checkpoint/prior-BENCHMARKS.md)
preserves the replaced `8f8628a4` rows, detailed historical improvements, adverse
results and uncertainty. Separate checkpoints cannot attribute a gain to one change.

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
| CSV validation/helper calls | 4.634–4.679 ms | 4.279–4.291 ms | 1.08–1.09× | `2ec9d67f` | Diagnostic; L0.96%, P0.29% / 1.79% |
| CSV summary/helpers | 3.282–3.302 ms | 3.201 ms | 1.03–1.03× | `6f76` | Diagnostic; 0.60% / 0.68% |
| Text summary/helpers | 1.188–1.267 ms | 1.195–1.204 ms | 0.99–1.06× | `2ec9d67f` | Variable; L6.69%, P0.73% / 2.84% |
| Text / captured helper + counter | 1.563–1.592 ms | 0.892–0.905 ms | 1.73–1.79× | `82ed92d4` | Diagnostic; L1.86%, P1.51% / 1.11% |
| JSON-lines/helpers | 3.476–3.609 ms | 4.076–4.096 ms | 0.85–0.89× | `2ec9d67f` | Variable; L3.68%, P0.51% / 1.70% |
| JSON-to-Markdown report | 3.025–3.085 ms | 2.352–2.408 ms | 1.26–1.31× | `2ec9d67f` | Variable; L1.99%, P2.42% / 4.90% |
| Config edit | 78.3–78.6 µs | 61.8–62.1 µs | 1.26–1.27× | `ee44` | Diagnostic; L0.41%, P0.43% / 2.18% |
| Protected file search | 0.217–0.218 ms | 137.8–138.0 µs | 1.58–1.58× | `ee44` | Diagnostic; L0.36%, P0.11% / 1.22% |
| File search / keyword-default helper | 259.11–262.53 µs | 160.56–161.12 µs | 1.61–1.64× | `82ed92d4` | Diagnostic; L1.32%, P0.35% / 1.40% |
| File search / guarded helper | 376.96–379.90 µs | 163.40–163.86 µs | 2.30–2.33× | `82ed92d4` | Diagnostic; L0.78%, P0.28% / 1.15% |
| Copy/checksum/manifest | 0.243–0.247 ms | 0.155 ms | 1.56–1.59× | `4598` | Diagnostic; 1.72% /2.72% |
| Patch generation | 0.497–0.520 ms | 0.576–0.580 ms | 0.86–0.90× | `4aa` | Variable; L4.44%, P0.74% /1.16% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 70.02–71.06 µs | 65.53–65.68 µs | 1.07–1.08× | `2ec9d67f` | Diagnostic; L1.47%, P0.24% / 1.56% |
| CSV summary/helpers | 61.3–62.2 µs | 59.1 µs | 1.04–1.05× | `6f76` | Diagnostic; 1.46% / 0.89% |
| Text summary/helpers | 43.89–43.99 µs | 41.50–41.67 µs | 1.05–1.06× | `2ec9d67f` | Diagnostic; L0.22%, P0.40% / 1.89% |
| Text / captured helper + counter | 0.045–0.045 ms | 0.054–0.054 ms | 0.82–0.83× | `82ed92d4` | Diagnostic; L0.15%, P0.53% / 1.27% |
| JSON-lines/helpers | 51.37–52.32 µs | 50.81–51.18 µs | 1.00–1.03× | `2ec9d67f` | Diagnostic; L1.85%, P0.73% / 0.77% |
| JSON-to-Markdown report | 40.77–41.36 µs | 37.83–37.91 µs | 1.08–1.09× | `2ec9d67f` | Diagnostic; L1.44%, P0.23% / 0.79% |
| Config edit | 36.5–37.2 µs | 34.2–34.4 µs | 1.06–1.09× | `ee44` | Diagnostic; L1.93%, P0.51% / 1.11% |
| Protected file search | 29.0–29.3 µs | 30.8–30.9 µs | 0.94–0.95× | `ee44` | Diagnostic; L0.89%, P0.44% / 1.30% |
| File search / keyword-default helper | 29.52–29.89 µs | 32.33–32.58 µs | 0.91–0.92× | `82ed92d4` | Diagnostic; L1.24%, P0.77% / 2.04% |
| File search / guarded helper | 32.09–32.16 µs | 34.41–34.44 µs | 0.93–0.93× | `82ed92d4` | Diagnostic; L0.24%, P0.09% / 1.49% |
| Copy/checksum/manifest | 70.9–71.9 µs | 56.7 µs | 1.25–1.27× | `4598` | Diagnostic; 1.33% /0.62% |
| Patch generation | 33.46–33.91 µs | 33.86–34.21 µs | 0.98–1.00× | `4aa` | Diagnostic; L1.34%, P1.06% /0.62% |

Startup, RSS/heap peaks, genuinely pending I/O and impact on other embedding work
are unmeasured. Small file-backed scripts do not establish large-file throughput.

## Measurement boundary and fairness

Dedicated Azure F4as_v7, Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap.
Actual .NET 11 RC3 and isolated CPython 3.13.16 use ordinary GC/tiering;
five-second warmups per role/job and seven rotated150ms rounds. The three affected
scripts use frozen0106a711/82ed92d4; four unaffected scripts keepc9829194/2ec9d67f.
All reuse common workerfec79afd and adapter0.4. Other rows keep recorded images.
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

Captured text retains a substantial warm gap at1.74×CPython; keyword-default
search is1.52–1.60×. Ordinary text is0.99–1.06×, CSV1.08–1.09×, JSON-lines0.85–0.89×,
report1.26–1.31×. Ordinary/JSON/search/report retain variable cases. Search complete
compile-to-result is1.51–1.55×. Current source-pair text gains do not translate into
the same external ratio change: both engines have new observations. No isolated
cause follows from comparison with an earlier checkpoint.

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
