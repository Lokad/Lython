# Rejected short-comparison conditioning experiment

Source **01beee54**; the [candidate patch](source.patch) changes only the existing
micro sampler. It is **rejected and removed**. No production runtime, option,
configuration policy or new benchmark framework remains. Raw prior comparisons
are unchanged and are not retimed or reclassified as speedups.

## Fixed question and acceptance

B18's saved ordered samples show first-two native batches consistently slower
than later batches, including unchanged strict-input controls. The old sampler
sequentially warms/calibrates each engine, waits an unobserved two seconds, then
rotates short measured batches. Worker GC/tiering is ordinary; only supervisor
tiering is disabled. JIT/scheduling/GC effects are hypotheses, not attributed causes.

One candidate replaces the blind idle with **seven recorded rotating conditioning
batches per engine**, using existing calibrated counts. It adds no switches or
thresholds, changes no worker execution and keeps conditioning distinct from
measurement. Existing golden/refusal/cancellation/partial-response checks remain.
135 micro/sampling checks pass on each target; [exact logs](focused-checks.tar.gz).

The pre-code acceptance requires, in **every** conditioned case: identical-worker
A/A medians within 5%, each native IQR <=10%, and first-two/last-five ordered
medians in 0.90–1.10. A miss removes the patch; no threshold adjustment, repeated
samples or alternate counts. Historical controls remain regardless of outcome.

## Six fixed jobs, no retiming

Collected 2026-11-01 Paris / 2026-10-31 UTC, **43.73 seconds**, all full goldens
and both quiet gates pass. Dedicated Azure F4as_v7, Ubuntu 24.04.4, four EPYC
9V45 vCPUs / 16 GiB, .NET 11 RC3 and independent CPython 3.13.16 control. Ordinary
worker GC/tiering; owned 30-second job / 120-second collection bounds, joined
cleanup and released VM lease. No forced collection or background work remains.

Exactly six jobs: historical and conditioned samplers each run warm robust file
search, warm robust float JSON report and fresh compile/run robust file search.
Every job launches **identical benchmark/core assemblies** for its two Lython
labels, with the same ordinary settings and independent contained fixture roots.
Both sampler versions use the identical core SHA256
`c8fdd54ced1152536cccc2f5787dc0d4d60182f09305128ea189e4c6a7c65da6`.
The changed benchmark assembly affects only orchestration. Complete source, input,
stdout/files, worker/library identities, every conditioning/measured response and
machine gates are in [raw evidence](raw-vm-results.tar.gz).

Warm excludes compilation; fresh compile/run includes it in a warm host. Both
include the existing declared host I/O/reset/full checks/relevant cleanup.
Startup is excluded. These are A/A measurement controls, not old/new performance
or CPython speed comparisons. Ordered early/late statistics diagnose stationarity;
never use them to trim observations or replace seven-batch medians.

| Sampler / lane / complete script | A/A median delta | Native IQRs | First-two / last-five ratios | All criteria |
| --- | ---: | ---: | ---: | --- |
| baseline / warm / file-search.replace.large | -4.70% | 71.14/120.82% | 2.8166/3.2566 | fail |
| baseline / warm / numeric-report.float.replace.large | +0.92% | 9.82/99.67% | 2.4150/3.4068 | fail |
| baseline / compile-run / file-search.replace.large | -6.91% | 101.91/138.18% | 3.0742/3.4689 | fail |
| candidate / warm / file-search.replace.large | -2.47% | 3.47/6.46% | 1.1088/1.0293 | fail |
| candidate / warm / numeric-report.float.replace.large | -1.28% | 9.28/3.57% | 0.9937/0.9648 | pass |
| candidate / compile-run / file-search.replace.large | +2.13% | 1.98/4.37% | 0.9953/1.0855 | pass |

Conditioning removes most visible spread in this one finite control, but warm
search's **1.108776** ordered ratio exceeds the pre-set **1.10** bound. The
candidate fails; do not round it into acceptance. Its source is removed, the
original sampler is restored, and there is no second conditioning/timing attempt.
The existing method's nonstationarity remains an explicit limitation. Identical
workers reproduce it, so previous timing differences cannot all be attributed
to the candidate runtime changes. Allocation/semantic evidence remains separate.

[Acceptance calculation](acceptance.json), [collection summary](collection.json)
and [fixed collector](collect.py) preserve the controls and rejection. Further
runtime work can continue from concrete CPU/allocation findings; only qualified
elapsed claims require a reliable boundary. This failure is not a blanket stop.
