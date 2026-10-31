# Allocation-coupled JSON integer parsing

Baseline **0268ebff** (production 959df325), candidate the same production source
plus [source.patch](source.patch), 2026-10-31. The patch was built at docs-only
85997446. Assembly identities and exact workload bytes are preserved below.
These are fixed observations, not arbitrary later HEAD or a primary CPython table.
Regex is unchanged and unused.

## Hypothesis, contract and acceptance

The [preceding CPU capture](../2026-10-31-numeric-cpu/README.md) attributes shared-
budget work to integer number paths in 146/484 guest samples (inclusive counts).
Source inspection finds a limb reservation/refund before non-allocating Int64
parsing, even for a one-digit value. Each token allocates a temporary owner and
resizes the shared allowance twice. Existing scalar/container headroom already
covers minor values. Float parsing does not take that reservation path.

For validated callback-free integer tokens at most 20 bytes, try the bounded
signed-64-bit span parse first. Only larger/out-of-range tokens create the original
limb and text-workspace reservations, before construction. Exact BigInteger parsing,
replacement overlap, retained-magnitude funding and cleanup remain. Twenty bytes
is a representation bound, not an input ceiling: larger tokens still parse.
There is no new owner, helper, cache, mode, policy or retry. Hooks remain before
this choice, with funded callback text, retained aliases and real suspension.
Managed nesting and cancellation checks are unchanged; long CLR fallback parsing
has the same cancellation limitations. Small values can fit budgets that formerly
refused unnecessary scratch; identical tiny-budget thresholds are not a contract.

Pre-recorded acceptance: remove the per-small-token reservation/root roundtrips,
reduce complete integer-report allocation by at least 20%, leave float/control
allocation unchanged, and avoid a repeatable material script/control regression.
Allocation passes at **26.46% less**. The fixed timing observations have high
spread and increases; they establish neither a repeatable material regression
nor a qualified elapsed gain. Retain this deletion on its allocation/simplicity
result, with no speedup or embedding-latency claim. Stop this redesign; no retiming
or branch tuning to rescue rows.

## Fixed before / candidate / identical-before diagnostic

The [adapted existing driver](driver/Program.cs) measures complete public warm Run
with maintained file fixture staging/reset, full stdout/file goldens and result
disposal. Compilation/startup are excluded; common reflection overhead is included.
Four warmups precede seven rounds of five complete jobs. Every measured execution
returns allowance to the compiled baseline; compiled disposal returns it to zero.
No forced GC, concurrent local build/test or replacement benchmark framework.

| Complete job | Before B | Candidate B | Identical-before B | Change |
| --- | ---: | ---: | ---: | ---: |
| integer JSON report | 990,659.2 | 728,504.0 | 990,659.2 | -26.46% |
| float JSON report | 744,688.0 | 744,688.0 | 744,688.0 | unchanged |
| CSV helper report | 9,193,923.2 | 9,193,923.2 | 9,193,923.2 | unchanged |
| text helper report | 5,583,107.2 | 5,583,107.2 | 5,583,107.2 | unchanged |

[Before](before.json), [candidate](after.json) and [identical-before](before-control.json)
include all samples/identities/balances and unqualified local elapsed values.
Windows 10.0.26200, 28 logical processors, pinned .NET 11 RC3, ordinary GC and
identical disabled tiering. Each numeric report loads two arrays of 4,096 values,
sums each once and writes the report; independent file/stdout goldens apply.

## Fixed representative VM observations

Two observations, fourteen preselected jobs, **111.10 seconds** under the 180-second
outer deadline; both quiet gates and every full file/stdout check pass. Dedicated
Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs, 16 GiB, .NET 11 RC3; pinned
CPython 3.13.16 is an independent control in every job. Existing micro tooling
uses warmup/settling and seven rotating 25-ms batches, ordinary GC and equally
disabled tiering. Startup is excluded. Warm excludes compilation; compile-run
compiles fresh source in an existing host. Both include declared fixture reset,
host I/O, complete validation and cleanup. No unrelated-median phase subtraction.

Microseconds per complete job; delta is candidate / baseline minus one:

| Lane / script | Observation 1 old / new | Delta | Observation 2 old / new | Delta | Old/new IQR, observations 1; 2 |
| --- | ---: | ---: | ---: | ---: | --- |
| warm / numeric-report.integer.large | 2,496.1 / 2,434.3 | -2.5% | 2,495.2 / 2,070.7 | -17.0% | 10.0/143.6%; 29.9/91.1% |
| warm / numeric-report.float.large | 2,249.2 / 2,236.2 | -0.6% | 2,243.5 / 2,622.8 | +16.9% | 6.1/120.3%; 10.7/149.9% |
| warm / csv-summary-helpers.large | 8,070.6 / 7,395.1 | -8.4% | 7,123.2 / 7,241.6 | +1.7% | 2.4/4.3%; 5.9/6.7% |
| warm / text-summary-helpers.large | 5,825.9 / 5,993.9 | +2.9% | 5,676.8 / 6,045.1 | +6.5% | 6.7/9.9%; 6.2/7.0% |
| compile-run / numeric-report.integer.large | 2,681.6 / 2,381.8 | -11.2% | 2,609.9 / 2,370.3 | -9.2% | 100.9/104.9%; 101.9/127.0% |
| compile-run / numeric-report.float.large | 2,360.8 / 2,490.7 | +5.5% | 2,421.9 / 2,645.6 | +9.2% | 100.9/129.6%; 99.7/102.3% |
| compile-run / config-edit.small | 2,987.0 / 2,802.1 | -6.2% | 2,747.1 / 2,800.6 | +1.9% | 9.8/6.4%; 7.9/22.6% |

Candidate numeric spreads reach 91–150%; fresh float increases in both observations
and warm float reverses. Text control point medians increase 2.9% / 6.5%, within
its old/new 6–10% spreads; CSV/config reverse. Preserve every increase and the raw
CPython controls. These diagnostic timings do not update docs/BENCHMARKS.md's
primary comparison. No second collection or branch tuning is justified.

## Validation and evidence

Focused Release checks pass on each target: **363 native JSON/integer** and
**718 public JSON/numeric-report** cases. Existing tests retain signed-width and
out-of-range exact integers, malformed/prefix coordinates, actual large-magnitude
and text-workspace refusal, parse-hook suspension/aliases, cancellation, retained
results and shared-budget reuse. No fixtures, assertions or timeouts changed.
Full delivery qualification is recorded separately in PLAN.md, not inferred from
an older source checkpoint.

[collection.json](collection.json) summarizes all jobs. The [raw archive](raw-results.tar.gz)
contains every receipt/log, before/after machine gates, collector source and exact
catalog, including source/input/output bytes. [collect.py](collect.py) is the fixed
collection driver, not a supported new benchmark framework. Receipt hashes bind
baseline/candidate workers and toolchains. The outer systemd service completed
successfully and joined its workers. No production profiler/native code was added.
