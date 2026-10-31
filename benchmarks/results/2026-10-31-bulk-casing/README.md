# Bulk ASCII casing prototype — rejected

The prototype is removed. It misses the pre-recorded requirement of at least 15%
repeatable warm improvement on both maintained text shapes in two observations,
without material control regression. Preserve this negative result; do not tune
run thresholds or individual branches to rescue the experiment.

Baseline is **2b01c4d6**, which already contains the independent native completion
cancellation fix. Candidate is that revision plus the exact
[source.patch](source.patch). The optional regex adapter is unchanged and these
workloads do not use it. This is not a new CPython baseline or current-HEAD claim.

## Hypothesis, prototype and correctness

The [CPU attribution](../2026-10-31-executable-cpu-attribution/README.md) found
11.2% of text guest samples in Unicode context-property lookup. The prototype
processed ASCII runs in bounded native spans through BCL Ascii.ToLower/ToUpper,
using the existing governed builder's write window. It carried final-sigma state
from the last non-ignorable original scalar and retained existing Unicode rune/
expansion behavior. There was no new builder/storage abstraction, cache,
configuration, unsafe code or execution engine.

Both targets pass 226 native and 192 public focused checks with the prototype:
CPython-derived long/mixed/ASCII/control/supplementary/expansion/sigma goldens,
retained method/result ownership, preflight and construction overlap refusal,
terminal origin, cancellation and budget reuse. Initial, growth and final-copy
refusals assert their respective requested estimates and leave input intact.

A separate witness exposes a real baseline bug: lower/upper can return after
cancellation is raised during their last expansion growth. The unmodified
161a7e9c helper fails both native cancellation cases; a single completion check
before final projection fixes them. [Before](cancellation-before.log) and
[fixed](cancellation-fixed.log) preserve that proof. The fix was committed as
2b01c4d6 before benchmarking and remains after removing the bulk prototype.
The independent Python/ownership regressions also remain; their names describe
observable casing behavior rather than the discarded implementation.

## Fixed allocation diagnostic

One local before / after / identical-before sequence reuses the maintained
representative catalog and file host through the
[adapted existing diagnostic](driver/Program.cs). It measures warm public Run,
fixture reset/staging, complete stdout/file goldens and result disposal, excluding
compilation/startup. Every execution returns shared-budget accounting to its
compiled baseline; compiled disposal returns it to zero. Four warmups precede
seven rounds of five executions per shape; reflection overhead is common.

| Complete text shape | Before allocated B / invocation | After | Identical-before |
| --- | ---: | ---: | ---: |
| Plain | 3,762,392 | 3,762,392 | 3,762,392 |
| Helpers | 5,583,107 | 5,583,107 | 5,583,107 |

Allocation is unchanged. Local timing is unqualified: plain medians are
13.53 / 11.74 / 11.56 ms; helpers 13.52 / 14.30 / 13.14 ms. Windows
10.0.26200, 28 logical processors, .NET 11 RC3, ordinary GC and tiering disabled
equally. No local Lython build/test overlaps collection. All processes complete
under 30-second caps. [Before](before.json), [after](after.json) and
[control](before-control.json) retain every round, catalog/assembly identities
and ownership observations. No forced GC is used.

## Fixed representative VM comparison

2026-10-31 UTC, dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs /
16 GiB; identical .NET 11 RC3 / CPython 3.13.16. Both quiet gates and all complete
stdout/file goldens pass. Fourteen fixed jobs finish in **110.60 seconds** under
the 180-second total bound and shared VM lease. The existing micro command
rotates baseline/candidate/CPython order over seven 25-ms batches after one-second
warmups/settling; ordinary worker GC/tiering, owned 30-second process
group caps and joined cleanup. No rows are rerun or discarded.

Warm complete-script milliseconds, old / prototype:

| Script | Observation 1 | Observation 2 | Result |
| --- | ---: | ---: | --- |
| Plain text summary | 6.126 / 11.609 | 9.662 / 16.981 | +89.5% / +75.7% point regressions; noisy |
| Helper text summary | 5.868 / 5.062 | 5.848 / 5.390 | 13.7% / 7.8% lower, below target |
| CSV helper control | 7.254 / 7.402 | 7.321 / 7.362 | +2.0% / +0.6% |
| JSON helper control | 7.804 / 7.909 | 7.965 / 7.890 | +1.3% / -0.9% |

Both text shapes use identical 35,840-byte mixed input: 27,648 ASCII bytes,
first line `Alpha café 😀`. Different input density does not explain their
difference. Plain observation 1 IQR fractions are 23.9%/2.4%; observation 2
163.1%/84.9%. Keep that severe noise visible. Helper IQR fractions are
7.2%/9.5% and 8.3%/6.7%. No causal attribution or precise slowdown multiplier
follows; neither affected shape satisfies the fixed acceptance test.

Fresh compile/run is also inconclusive and unfavorable: plain 7.005/29.517 ms
then 12.665/13.512; helpers 7.217/36.307 then 6.862/18.972. Config controls
are essentially unchanged then 6.3% lower. Compilation/runtime controls do not
justify selecting a favorable helper-only result. CPython values remain context
in [collection.json](collection.json); the authoritative comparison table keeps
its measured revision.

[raw-results.tar.gz](raw-results.tar.gz) contains every response/sample, machine
gate, identity, full stdout/file verification and exact source/collection patch.
[collect.py](collect.py) records the fixed lanes and stopping budget. The existing
phase diagnostic supplies separate compile/I/O/cleanup attribution; this
experiment adds no instrumentation or benchmark framework to production.

## Final disposition

Remove the ASCII-span branch and extra prototype-only plumbing completely.
Retain the independently proven native completion cancellation fix and semantic/
ownership tests. Focused checks on the restored scalar implementation are
recorded in PLAN.md. Stop B06; do not repeat measurement, lower its threshold,
add an internal tuning switch or retain a dormant alternate casing backend.

VM tiering description corrected 2026-11-01 from [recorded worker identities](../2026-11-01-native-utf8-errors/tiering-correction.json).
The supervisor alone disables tiering; both workers retain ordinary settings.
Local Windows allocation diagnostics still use their stated disabled tiering.
Raw measurements and performance conclusions are unchanged.
