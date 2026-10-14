# Lython performance against CPython

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Complete Unicode text summary [T] | 6,144 words / 35,840 UTF-8 bytes | 8.018 ms | 1.258 ms | — | One warm diagnostic at ca432c22; noisy ASCII candidate rejected |
| Complete JSON-lines summary [J] | 4,096 records / 110,592 UTF-8 bytes | 11.537 ms | 4.225 ms | — | One warm candidate diagnostic; timing inconclusive (11.8% spread), allocation reduction measured |
| Complete CSV summary [C] | 4,096 records / 57,361 UTF-8 bytes | 7.530 ms | 3.483 ms | — | One warm diagnostic at f95fb734; inconclusive baseline spread (15.4%) |
| CSV parse and discard [G] | 16 data rows | 0.0780 ms | 0.0285 ms | — | One short governed-memory diagnostic; uncommitted source, unqualified |
| Sum generated squares [A] | 20,000 items | 3.947 ms | 0.499 ms | — | One short diagnostic at 09d81bb1 source; no qualified multiplier |
| Integer reduction loop [M] | 16,384 iterations | 1.162 ms | 0.617 ms | — | Short diagnostic at b43664e0; one run, no qualified multiplier |
| Integer reduction loop [L] | 16,384 iterations | 3.58–3.72 ms | 0.578–0.666 ms | — | Historical diagnostic; failed control and cross-session stability |
| Keyword calls [Q] | 2,048 calls | 1.378–1.404 ms | 0.137–0.145 ms | — | Historical diagnostic; failed invocation control |
| Stable sort and output [Q] | 2,048 items | 5.150–7.889 ms | 0.306–0.313 ms | — | Historical diagnostic; spread/order checks also failed |
| ASCII text pipeline [Q] | 1,024 eleven-character blocks | 0.618 ms | 0.0415 ms | — | Historical diagnostic; only one session before idle gate failed |
| JSON transform and round trip [Q] | 128 records | 0.674–0.741 ms | 0.0610–0.0618 ms | — | Historical diagnostic; control floor/spread/interval checks failed |
| CSV parse and retain [Q] | 128 data rows | 1.037–1.185 ms | 0.102–0.108 ms | — | Historical diagnostic; control/spread/order/interval checks failed |
| Other CSV summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other Unicode text summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other JSON-lines summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Complete file copy/checksum manifest [S] | 0 / 16 / 1,024 fixture repeats | — | — | — | Output correctness verified; comparative timing pending |

**Ratio convention:** Lython elapsed time divided by CPython elapsed time;
above 1 means Lython takes longer. A dash means unavailable or unqualified,
never zero. The historical generated reports use the inverse convention;
their ratios must not be copied into this column. Ranges above are the smallest
and largest retained **session medians**, rounded for readability, not confidence
intervals. The ASCII row has just one retained median.

The historical measurements show substantial performance gaps on these jobs,
including a loop whose cost grows with its iteration count. They support
investigating interpreter dispatch and application workloads. **No workload
multiplier qualified in either campaign. These timings do not describe today's
HEAD:** subsequent interpreter and containment changes have been integrated.
The newer T, J, C, G, A and M rows are separate short diagnostics at their named revisions; they do
not qualify a current overall or complete-script multiplier.
The T, J and C rows supply complete-script comparisons; their candidate improvement
evidence remains inconclusive. No complete-script multiplier or overall speedup is qualified.

## Which code and machine were measured?

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
| T | 2026-10-14 | ca432c22, governed runtime and worker ownership; ASCII candidate rejected | [Complete text diagnostic](benchmarks/results/2026-10-14-representative-text/README.md), [loaded identities](benchmarks/results/2026-10-14-representative-text/micro.json) |
| J | 2026-10-14 | Working direct-decoder candidate on 64ec1fd4, with identical f95fb734 worker | [Complete JSON diagnostic](benchmarks/results/2026-10-14-representative-json/README.md), [timing identities](benchmarks/results/2026-10-14-representative-json/micro.json) |
| C | 2026-10-14 | f95fb734, governed runtime and worker ownership; parser candidate rejected | [Complete CSV diagnostic](benchmarks/results/2026-10-14-representative-csv/README.md), [loaded identities](benchmarks/results/2026-10-14-representative-csv/micro.json) |
| G | 2026-10-14 | Working migration on `9fc7e37e`, before local-import simplification | [Governed-memory diagnostic](benchmarks/results/2026-10-14-governed-memory/README.md), [loaded identities](benchmarks/results/2026-10-14-governed-memory/micro.json) |
| A | 2026-10-14 | `09d81bb153c006f3c248d8ba0dfdfe9cdbc70d63` source, built before commit | [Native state qualification](benchmarks/results/2026-10-14-native-working-state/README.md), [all attempts and loaded identities](benchmarks/results/2026-10-14-native-working-state/micro.json) |
| M | 2026-10-13 | `b43664e0f60aa78f3cbd638578e0ced28d926194` | [Shared frames and transfer cost](benchmarks/results/2026-10-13-shared-async-frames/README.md), [all attempts](benchmarks/results/2026-10-13-shared-async-frames/micro.json) |
| Q | 2026-10-09 | `9358c4e72f1a29e55703eee02b28ea404d01bc0a` | [Four-lane run](benchmarks/results/2026-10-09-quick/README.md), [warm medians and exclusions](benchmarks/results/2026-10-09-quick/warm.md) |
| L | 2026-10-09 | `56d4346d960b58f037da7d4590eb66c87ffec29e` | [Focused loop run](benchmarks/results/2026-10-09-core-loop/README.md), [all loop samples](benchmarks/results/2026-10-09-core-loop/warm.md) |
| S | 2026-10-12 | `87c1a94c347a88bc94927ea603a138cec4376170` (correctness, no timings) | [Complete-script validation](benchmarks/results/2026-10-12-script-negative-cases/README.md), [fixture and host contract](benchmarks/results/2026-10-12-containment-scope/README.md) |

Q and L used the same dedicated **Azure Standard_F4as_v7**: Ubuntu 24.04.4 LTS
x86_64, four AMD EPYC 9V45 vCPUs, one thread per core, 16 GiB RAM, no swap.
Lython was a clean Release build using **.NET SDK 10.0.401 / runtime 10.0.12**.
The primary external reference was **CPython 3.13.16**, built with GCC 13.3,
PGO/LTO, ordinary GIL and cyclic GC, without debug mode or JIT, launched with
`-I -S`. Workers kept ordinary GC/tiering and public Lython limits; optional
instruction fuel was unset. Both engines ran sequentially on that machine.
The linked receipts identify binaries, inputs, toolchains and sampling policies.
A ran on the shared Windows 11 x64 development workstation: i7-14700KF,
28 logical processors, 64 GiB RAM; SDK 10.0.300-preview.0.26177.108,
.NET 10.0.13 and CPython 3.13.2, ordinary GIL/GC, launched with `-I -S`.
Its 7.893-second owned group uses the existing generated-sum workload with
shared printed output. The candidate library's informational metadata names
the preceding commit; frozen hashes identify the added sum implementation.
Compilation and startup are excluded. This diagnostic does not qualify a
CPython multiplier or an improvement over earlier Lython.
M ran on that dedicated four-vCPU Ubuntu VM under .NET 10.0.12 with the same
CPython 3.13.16 reference. Its portable Windows Release libraries were built
using SDK **10.0.300-preview.0.26177.108**; its identical benchmark observer and
DLL hashes are frozen separately. Seven rounds in one 8.24-second owned group
provide diagnostic context, with compilation/startup outside the warm timer.

S verifies exact stdout and output-file bytes through Lython's public API in
both synchronous and genuinely yielded asynchronous execution, plus independent
CPython 3.13.2 checks on Windows. Its fixture repeats expand into different record/byte counts for
each script; they are not a shared row count. Correctness tests are not timing
measurements. The [original scripts](benchmarks/scripts) remain ordinary Python.

G uses the same shared Windows workstation/toolchain as A and CPython3.13.2.
Its warm CSV comparison excludes compilation/startup and is a single7.320-second
diagnostic; later local-import changes are unmeasured.

## Execution, compilation and startup are separate

The leading table uses **warm execution**: both engines reuse compiled code,
create fresh invocation state, execute the complete module, capture/project
output and perform the cheap golden check inside the worker timer. Compilation,
worker launch, transport and full output hashing are outside it. Module imports
retain their source positions and normal caches; invocation controls are never
subtracted. Historical Q/L cases have in-memory fixtures, not disk file I/O.

Lython's host boundary, memory governor and cooperative checks remain enabled.
CPython has no equivalent in-process containment. These are application costs
under the two ordinary execution contracts; neither a bare arithmetic test nor
a claim that the contracts provide identical protection.

| Boundary | Historical observation at Q | Status |
| --- | --- | --- |
| Warm invocation | See workload table above | No qualified workload ratios |
| Compile and run | Compile source, then execute fresh state in persistent workers | Mostly incomplete after idle gates; [retained results](benchmarks/results/2026-10-09-quick/compile-run.md) |
| Compilation only | Lython engine compilation versus CPython `compile()` | Mostly incomplete after idle gates; [retained results](benchmarks/results/2026-10-09-quick/compile.md) |
| Fresh-process empty control | Lython 170.361–175.524 ms; CPython 29.628–29.947 ms | Startup control; [retained results](benchmarks/results/2026-10-09-quick/fresh-process.md) |

Fresh-process timing includes parent-observed launch, compilation, execution,
pipe drain and exit, with OS file caches retained. It must not be compared with
warm execution. Its controls qualified, but workloads failed the required
separation from startup; no workload ratio was published. Compilation samples
and startup costs must not be folded into the warm table.

## Improvements against earlier Lython revisions

These compare Lython with earlier Lython, **not with CPython**. Each uses short
diagnostic collections, ordinary protection/GC/tiering and unchanged worker
boundaries. They have no milestone qualification or claimed confidence interval.
The percentages must not be added or multiplied into a current CPython ratio.

| Change and date | Baseline → candidate revision | Observed Lython time reduction | Evidence |
| --- | --- | ---: | --- |
| Synchronous instruction dispatch, 2026-10-09 | `56d4346d` → `891798a6` | 13.4–14.0%, 16,384-iteration loop | [Both collections](benchmarks/results/2026-10-09-sync-dispatch/README.md) |
| Exact integer addition, 2026-10-09 | `891798a6` → `e97cd4da` | 10.7–14.1%, same loop | [Both collections and rejected alternative](benchmarks/results/2026-10-09-integer-addition/README.md) |
| Function-scoped dispatch, 2026-10-11 | `1bf1e241` → `d10564e7` | 4.44% / 10.10%, complete method job | [Corrected producers, controls and safety evidence](benchmarks/results/2026-10-11-scoped-dispatch-corrected/README.md) |
| Proved impossible string admissions, 2026-10-13 | `7f5fbf70` → `db040f52` | 320.486 → 1.856 ms/job, 128 caught bound errors | [One short comparison, CPU/GC evidence and invalid setup](benchmarks/results/2026-10-13-proved-string-admission/README.md) |

## Remaining uncertainty and maintenance

The next useful comparison is the four complete CSV, text, JSON and file scripts,
with identical input/output bytes and a declared file-host boundary. Their
correctness is established. The [file-worker delivery](benchmarks/results/2026-10-12-file-workers/README.md)
at `ed987087` verifies all 12 cases against CPython 3.13.2 on Windows, in persistent
and fresh processes, with matched disk staging/reset and output checks. Quiet-VM
timing and qualification remain pending. The older retained-CSV
job is not evidence for bounded streaming or host latency.

Process-wide forced GC remains a containment concern. The separate
[embedding-impact experiment](benchmarks/results/2026-10-12-embedding-impact/README.md)
records host request tails, heartbeat stalls and collection counts; fewer full
collections alone do not establish better host latency. A separate
[Windows observer pilot](benchmarks/results/2026-10-12-embedding-observer-v2/README.md)
passes its fixed setup/output gates for async CSV and file jobs; shared-machine
data does not qualify host latency.
The [Linux async comparison at `4cdfd6b9`](benchmarks/results/2026-10-12-embedding-async/README.md)
qualifies all four complete jobs at that revision within its finite host model, while GC-independent
accounting and broader host-impact qualification remain open. The
[Linux sync/retained-pressure model at `f67419a8`](benchmarks/results/2026-10-13-embedding-sync-pressure/README.md)
meets the same latency targets in 30 original samples: no Gen2 in the four complete
streaming jobs, one Gen2 per fresh retained-pressure execution. It is a finite
embedding model; Windows impact and GC-independent reclamation remain open.
The [bound/exposed/retained-error model at `7f5fbf70`](benchmarks/results/2026-10-13-embedding-exception-pressure/README.md)
also meets those latency targets in 24 original samples, but incurs 128 Gen2
collections per completed job versus one for unbound catches. These cases
complete 12–14 jobs per nominal four-second window versus 1,250–1,267 for unbound catches;
each window finishes its last complete job.
The report also records failed memory recovery after reference withdrawal.
Regex performance waits
for [managed-only backend qualification](benchmarks/results/2026-10-13-regex-scalar-44/README.md).

For an optimization round, use the existing seconds-long diagnostic command;
reserve ten-minute lanes for declared milestones. Keep failed/incomplete samples,
ordinary limits and frozen acceptance rules. A new result must state its exact
revision, reference version, date, machine, boundary and qualification status.
Update this page's primary table only from that evidence, and retain the prior
identity. Do not relabel historical receipts as current results.

The [list and enumeration checkpoint](benchmarks/results/2026-10-14-list-and-enumeration/README.md)
preserves two additional short comparisons and their preceding-source identities.
Both are inconclusive; neither supplies a qualified speed change or CPython
multiplier. Complete ownership and forced-GC independence remain open.

The [zip containment diagnostic](benchmarks/results/2026-10-14-zip-state/README.md)
retains a higher candidate median with substantial noise. Its status is
inconclusive; it does not establish today's HEAD performance or async pull cost.

The [complete JSON decoder experiment](benchmarks/results/2026-10-14-representative-json/README.md)
records 28.0% less managed allocation against earlier Lython. Its nominal 3.7%
lower time remains inconclusive at 11.8% candidate spread. CPython remains the
external timing baseline; allocation totals establish neither RSS nor latency.

The [complete CSV profile and comparison](benchmarks/results/2026-10-14-representative-csv/README.md)
uses the dedicated four-vCPU Ubuntu VM, .NET 10.0.12 and isolated UTF-8 CPython
3.13.16. Its parser candidate is removed: a nominal 2.2% difference is smaller than
15.4% baseline spread. The source and single capture remain historical evidence;
no repeat, qualified ratio or embedding-latency claim follows.

The [governed-memory CSV diagnostic](benchmarks/results/2026-10-14-governed-memory/README.md)
keeps the earlier Lython comparison separate from the primary CPython baseline.
It establishes neither a current complete-script multiplier nor host tail latency.

- [Measurement methods, commands and qualification rules](benchmarks/COMPARISON.md)
- [Experimental history, profiles and rejected optimizations](benchmarks/EXPERIMENTS.md)
- [Historical Lython-only BenchmarkDotNet scan baselines](benchmarks/Baselines.md)

Documentation consolidated 2026-10-12; this date is not a new timing campaign.
