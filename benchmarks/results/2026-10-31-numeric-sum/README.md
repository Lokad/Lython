# Numeric aggregation without per-item result boxes

Baseline **b1824ea3**, candidate that production source plus
[source.patch](source.patch), 2026-10-31. Candidate builds at docs-only 81abeab1;
loaded assembly identities are preserved below. These are fixed source
observations, not a measurement of arbitrary later HEAD or a primary CPython
baseline. Optional regex dependencies/code are unchanged and unused.

## Workload, hypothesis and design

The [numeric report](../../scripts/numeric-report.py) loads planned/actual JSON
arrays, computes each total once, and writes count, totals and variance to JSON.
Each large fixture has two arrays of 4,096 values; integer and finite binary-
fraction data have independent CPython 3.13.2 file/stdout goldens. No repeated-sum
loop inflates the proposed benefit. [catalog.json](catalog.json) preserves the
measured source and input/output bytes. The default representative catalog now
includes both shapes at 0, 64 and 4,096 values per array; its large source/input/
output bytes are verified identical to these frozen cases.

A single baseline diagnostic established that native Sum materializes one boxed
running result per integer/double item. Its two sums allocate 262,512 / 196,976 B
on prebuilt arrays/context, tracking 8,192 result boxes at 32/24 B plus fixed
transport/iteration overhead. The complete reports allocate about 1.25 / 0.94 MB.
These are allocation observations, not an inferred CPU share.

The candidate keeps the existing SumState and sync/async loops. Eligible signed
64-bit and compensated-double prefixes live in scalar fields. Only final results
or genuine general/protocol transitions materialize a value. It removes per-item
object Total updates and generalized integer addition from that eligible prefix;
there is no universal tagged value, numeric public type, pool, cache or new mode.

The fixed 64-bit phase policy is unchanged. Checked overflow and out-of-range
items enter the existing exact/prefunded arithmetic path. General transitions
do not restart compensation; numeric eligibility after callbacks and small
integer additions within float phase retain existing behavior. Empty starts,
signed zero, nonfinite correction, Python rounding, cursor acquisition/cleanup,
cancellation and real callbacks remain covered. At most 64-bit materialization
uses the existing minor-scalar headroom; substantial promotion retains allocation
preflights. No storage owner, admission retry or budget policy is added.

Pre-recorded acceptance: >=15% less allocation in both complete reports, no
per-item numeric **result** boxes on eligible prefixes, and no repeatable material
complete-script/control regression. The allocation condition passes. The fixed
elapsed observations are inconclusive; they establish no qualified speedup or
host-latency improvement. Accept the allocation redesign without a timing claim;
do not retune or retime its branches to rescue favorable rows.

## Fixed before / candidate / identical-before diagnostic

The [adapted existing diagnostic](driver/Program.cs) uses the maintained file
host and public warm Run, including fixture staging/reset, full stdout/file
checks and result disposal. Compilation/startup are excluded. Four warmups
precede seven rounds of five complete jobs. Every run returns allowance to its
compiled baseline, and compiled disposal returns it to zero.

A separate native Sum allocation boundary runs both sums on prebuilt arrays and
context; it includes common reflection/cursor overhead. It is **not** a timed
phase subtracted from the complete job or a replacement for public validation.
Native totals retain fixed allocations, final result boxes and a float transition.

| Report | Complete before B | Candidate B | Identical-before B | Change | Separate native sums before / after B |
| --- | ---: | ---: | ---: | ---: | ---: |
| integer | 1,252,739 | 990,666 | 1,252,739 | -20.92% | 262,512 / 432 |
| float | 941,200 | 744,699 | 941,200 | -20.88% | 196,976 / 464 |

[Before](before.json), [candidate](after.json) and [identical-before](before-control.json)
preserve all samples, identities, ownership observations and local elapsed values.
Windows 10.0.26200, 28 logical processors, pinned .NET 11 RC3; ordinary GC and
identical disabled tiering. No forced collections or overlapping local Lython
build/test. Local elapsed is unqualified. Compilation is excluded; earlier
[B09 evidence](../2026-10-31-compact-instructions/README.md) retains its own sources
and compilation boundary.

## Fixed representative VM comparison

Two observations, fourteen preselected jobs, **111.81 seconds** below the
180-second outer bound. Both quiet-machine gates and all complete stdout/file
goldens pass. Dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs,
16 GiB, .NET 11 RC3; CPython 3.13.16 is a separate control in every job. No
startup is timed. Existing micro tooling uses warmup/settling and seven rotating
25-ms batches, ordinary GC and equally disabled tiering.

Warm execution excludes compilation; fresh compile/run includes new source
compilation in an existing host. Both execution boundaries include declared
fixture reset, host I/O, complete validation and relevant cleanup. No unrelated
medians are subtracted to infer I/O/cleanup phases. Controls are unchanged
CSV/text helper jobs and configuration editing.

Microseconds per complete job; delta is candidate / baseline minus one:

| Lane / script | Observation 1 old / new | Delta | Observation 2 old / new | Delta | Old/new IQR, observations 1; 2 |
| --- | ---: | ---: | ---: | ---: | --- |
| warm / numeric-report.integer.large | 2,622.6 / 2,506.1 | -4.4% | 2,601.0 / 2,403.9 | -7.6% | 27.8/113.4%; 7.9/125.2% |
| warm / numeric-report.float.large | 2,268.5 / 2,340.6 | +3.2% | 2,375.7 / 2,222.9 | -6.4% | 7.7/114.0%; 8.1/111.8% |
| warm / csv-summary-helpers.large | 7,524.7 / 7,597.6 | +1.0% | 7,214.0 / 7,170.8 | -0.6% | 5.8/2.8%; 6.6/4.9% |
| warm / text-summary-helpers.large | 6,308.0 / 6,091.9 | -3.4% | 5,827.6 / 5,727.3 | -1.7% | 8.6/7.8%; 6.4/7.1% |
| compile-run / numeric-report.integer.large | 2,936.2 / 2,661.6 | -9.4% | 2,560.1 / 2,597.7 | +1.5% | 193.7/117.9%; 118.5/167.3% |
| compile-run / numeric-report.float.large | 2,474.0 / 2,542.1 | +2.8% | 2,364.7 / 3,007.9 | +27.2% | 103.6/138.3%; 106.6/94.9% |
| compile-run / config-edit.small | 2,796.2 / 2,857.2 | +2.2% | 2,842.6 / 2,695.5 | -5.2% | 6.6/5.9%; 7.5/5.6% |

Numeric candidate spreads reach 113–167% and several old rows are also extremely
variable. Fresh float increases 2.8% / 27.2%; warm float reverses and integer
fresh rows reverse. Keep every outcome visible: no stable elapsed conclusion or
repeatable material regression follows. Quiet gates do not cure process GC/JIT/
scheduling variation. CSV/text/config controls remain separately visible; no
extra lanes, recaptures or favorable-sample selection were used.

[collection.json](collection.json), [raw-results.tar.gz](raw-results.tar.gz) and
[collect.py](collect.py) preserve each receipt, sample, machine gate, original
merged catalog and exact collector/deadline/cleanup logic. All owned workers and
collection services finish and are joined. The merged VM catalog retains the
unchanged prior CSV/text/config controls; local native diagnostics select only
the two reports. Per-case source/input/output identities, not catalog membership,
establish the common workload boundary.

## Focused validation and limits

Both targets pass 394 focused native numeric/sum cases. Public sum/numeric/
arithmetic coverage passes 584 existing cases on net11 and 586 on net10, with
all 40 float/phase cases (including two new signed-boundary cases) also passing
on net11. Existing tests cover cursor ownership, real suspension, callback
errors, terminal refusal, cancellation, start aliases and shared-budget reuse.
Six new complete-report cases run both APIs and return the shared 4 MiB budget
to baseline; net10 additionally passes the existing comparison-micro controls.

The report registration/fixtures were integrated after freezing the measured
workers; the maintained large source/input/output bytes are verified identical.
It adds workloads to the existing catalog/host/verifier, not another framework.
Platform/package qualification is tracked separately in PLAN.md. B09's full
ordinary/stress/package result proves its source, not this later numeric change.
No published package/tag changes or adapter publication are part of this work.

Stop this scalar-aggregate redesign after delivery checks. Remaining CPython
execution gaps and these inconclusive latency observations stay open. Do not
expand it into runtime-wide numeric representation, a new parser or tuning
campaign. Final released-adapter qualification remains independent.
