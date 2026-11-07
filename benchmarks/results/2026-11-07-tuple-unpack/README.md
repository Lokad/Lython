# Immutable tuple unpack transport — R70/B423

2026-11-07. Retained production change onfd5ab1c1; measured candidated6445fc5
(.NET11) /4b3fda59(.NET10). Exact source inventory/patch retained. Public API and
Python semantics unchanged; fewer unnecessary scratch admissions can avoid earlier
conservative rejection. This does not promise identical tiny-budget thresholds.

Exact builtin tuples already own immutable storage. Unpacking now borrows that
tuple directly, eliminating its checked iterator chain, leased List, backing copy
and shell/capacity reservation. A small local value retains either that owner or
the existing snapshot lease. Mutable/user-protocol sources still snapshot; starred
remainders admit their own capacity before target effects. No new budget, ledger,
pool, cached owner, policy or execution route. Sync/async assignments, loops,
annotations and JSON separators use the same immutable-storage rule. Existing
error translation remains for other inputs; no tuple-subclass protocol bypass.

**824internal/1,106public union per actual.NET11/10 pass**, with matching probes.
The first net11public run has1,104cases;24final ordinary tests include2new refusal/
reuse cases, yielding1,106distinct cases. All24final ordinary regressions pass on
the baseline core35cd0db3. Tuple/list controls preserve real target suspension,
cancellation, retained starred aliases, first terminal refusal despite finally
return, stopped target effects and shared-budget reuse. General iterator tail,
arity, generator, JSON and admission/ownership checks remain. No timeout/assertion
relaxation or architecture-only test replacement.

## Allocation / desktop diagnostic

Existing five-script driver, independent before/current/before-control assemblies
sharing process GC/CoreLib/hashing, both APIs and normal GC/tiering. All goldens,
shared balances, image isolation and owned fixture cleanup pass. Returned-record
CSV allocation **−13.57%sync /−14.40%async**; ordinary CSV/text/JSON/report allocation
is essentially unchanged. Desktop clocks are diagnostic: affected sync−5.97to+10.02%
with14.53%old-role drift, async−7.15to−1.50%; text is adverse+9.42–9.98%sync and
+5.31–8.34%async. Other adverse/variable controls remain in raw data. No desktop
CPU claim, discarded observation or causal explanation of that text result.

## Controlled complete-script source pair

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, unchanged7cf37ca5 manifest worker and
normal optional0.4adapter. Baseline15c04e2a matches fully qualified824fc6bb source;
local baseline35cd0db3 has the same production source andfd5ab1c1 metadata.
Two independent baseline/two current processes,5s warmup/role/job and7rotated150ms
rounds. **Four jobs complete in114.59seconds**. Negative elapsed means faster.

| Complete job | Elapsed change across all process pairs | Baseline / current spread |
| --- | ---: | ---: |
| csv-validated-records.large / warm | -7.45 to -6.45% | 0.58% / 0.49% |
| csv-validated-records.large / warm-async | -9.34 to -7.01% | 1.96% / 0.55% |
| csv-summary-functions.large / warm | +0.06 to +0.28% | 0.14% / 0.08% |
| text-summary-helpers.large / warm | -0.96 to -0.15% | 0.49% / 0.33% |

Affected elapsed improves across both candidate processes. The ordinary CSV/text
controls stay near baseline; this does not reproduce the adverse desktop text
result, whose cause remains unresolved. Preserve CSV+0.28%elapsed and+4.07%coarse
processCPU observations. Affected CPU direction is favorable but its10ms ticks,
outside-job protocol work and process variation do not establish a precise CPU
gain. No universal speedup, CPython multiplier or historic debt repayment claim.
Retain the complete storage-phase deletion and independently measured allocation/
controlled elapsed benefit. Larger ordinary call/frame consolidation stays open.

Warm includes public invocation, mediated RAM-file I/O, complete stdout/file
validation and prior-result cleanup; compile/startup/staging/reset/transport/final
cached teardown excluded. Desktop includes fixture reset/result disposal and must
not be compared by absolute time. Async uses completed host I/O here; real target
suspension is separately tested. No startup/RSS/pending-I/O/embedding-tail claim.

All source/actualReady/auxiliary/golden/quiet/Closed/exit/join/empty-fixture gates
pass. Persistent workers/records remain separate from the owned128MiB RAM fixtures;
all users finish and the mount is removed before archive verification under lease
and independently locally.150sowner+30stop+30margin; staging315,863,040B passes,
no reclaim or restart. Initial early local observation guard refused before any
service query; the same completed collection was subsequently retrieved once.

Full Windows/Ubuntu, original Stress and installed-package delivery is pending
for this new source; baseline's green receipt does not qualify it. Publication
postponed. B423 terminal/archived/verified; never query it again.

[Controlled assessment](assessment.json), [allocation/adverse desktop observations](local-assessment.json),
[focused checks](focused.json), [source/image identities](identity.json),
[VM identities](vm-identity.json), [pre-code plan](pre-code-plan.md), [contract](contract.md),
[VM archive](observations.tgz), [receipt](archive-receipt.json),
[local source/cores/tests/raw observations](local-observations.tar.xz).
