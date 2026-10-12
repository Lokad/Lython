# Linux embedding impact: current synchronous jobs and retained pressure

All five declared models meet the original finite host-impact targets: added
request p99 <=5 ms and largest added heartbeat stall <=25 ms. There are **30
eligible samples**, **15 matched pairs**, no excluded/retried samples, and the
fixed matrix finished in **210.01 seconds**. The longest owned child took 6.609s.

The four complete streaming jobs have no measured Gen2 collection. Retained
pressure incurs **one Gen2 collection per fresh job**: 1,244–1,358 in each 4-second
window. Its 128 caught denials do not repeat the full collection within one job.
Meeting these latency targets leaves the architectural forced-GC problem open.

## Results and measured revision

Milliseconds and count ranges below span three independent sessions. Added values
subtract each session's same-workload host-only control; counts cover the complete
observation window, including all jobs completed inside that window.

| Workload | Request p99 (ms) | Added p99 (ms) | Largest added heartbeat (ms) | Gen0 / Gen1 / Gen2 | Complete jobs |
| --- | ---: | ---: | ---: | --- | ---: |
| csv-summary | 0.837–0.855 | 0.773–0.791 | 0.801–0.870 | 3,845–3,986 / 0 / 0 | 81–84 |
| text-summary | 0.313–0.318 | 0.251–0.255 | 0.297–0.332 | 4,009–4,180 / 0 / 0 | 211–220 |
| json-summary | 0.716–0.722 | 0.652–0.660 | 0.983–1.088 | 3,036 / 0 / 0 | 82 |
| file-manifest | 0.593–0.650 | 0.529–0.585 | 2.154–2.791 | 448–466 / 1 / 0 | 19,856–20,646 |
| retained-pressure | 2.434–2.670 | 2.372–2.607 | 2.593–3.074 | 3,732–4,074 / 2,488–2,716 / 1,244–1,358 | 1,244–1,358 |

Measured producer: `f67419a86ee0304de9d0f05c0e68ef15c860e508`, production tree
`fc76a4a297f6f29e229b621bebf607d609a333fb`, on 2026-10-13 (Paris; raw UTC stamps
are preserved). Quiet Azure Linux VM, 4 logical processors, Linux 6.17.0-1022-azure,
.NET SDK 10.0.401/runtime 10.0.12. The exact subject DLL and toolchain identities are
in [prepared.json](reproduce/prepared.json); these are diagnostic Release DLLs,
not release NuGet packages. Later HEADs need their own qualification.

## Fixed execution boundary

The observer services a small JSON request and heartbeat with a managed thread
and 1-ms sleep, while the caller runs repeated complete `Run` jobs. Every job has
fresh execution state; compiled code and the fixed in-memory host fixture are
reused. Canonical CSV/text/JSON/file scripts use the independently checked 1,024-
scale fixture bytes at a 256-KiB execution cap. Success, full stdout and every
output-file byte are checked on every complete job. Report validation and host
activity belong to this embedding model; its allocations are whole-process
measurements, not library bytes per row or a CPython timing comparison.

Every child/control receives the same 128-MiB retained host graph (131,072 managed
byte arrays), two declared noncompacting full preparation collections outside
warmup/measurement, two real warm jobs and the same 2-second preparation pause.
All host arrays must be Gen2 before measurement. The 4-second closed-loop window
includes complete job boundaries; it is not a production request queue or SLO.
Controls perform only host requests during measurement, after identical warmup.

The retained-pressure [script](reproduce/retained-pressure.py) keeps a 16,000-byte
payload under a 28,000-byte cap and catches 128 failed 16,000-byte admissions. Each
job must return exactly `[128,16000]`. Exceptions are unbound: this case does not
claim coverage of retained exception objects or diagnostic exposure/binding.
The invariant held across all jobs: measured Gen2 count equals complete-job count.
Fresh executions allocate new live storage; this is different from repeating a
pinned denial without allocation progress inside the same execution. The report
therefore verifies suppression in this fixed case while retaining the process-
wide first-denial collection concern.

## Admission, evidence and stopping

The reused supervisor owns an exclusive VM lease, 28-second child deadline plus
one second of kill/cleanup, and a 300-second campaign ceiling. Eligibility checks
ordinary runtime settings, idle CPU median<=3%/max<=5%, no paging/steal, exact
source/fixture/artifact identities, all-old host graph, >=1,000 observations,
complete expected outcomes and the unchanged caps. All 30 originals qualified;
no replacement matrix or ten-minute comparative lane was run.

[Raw distributions/receipts](raw/), [independent validation](validation.json) and
[probe/preparation/runner sources](reproduce/) retain the evidence. Both owned
services are inactive/dead, their cgroups are gone and the VM lease is free;
[terminal proof](reproduce/terminal-proof.json) is frozen. Reproduce by archiving
the declared producer paths in [source-archive-paths.json](reproduce/source-archive-paths.json),
building the declared probe against that subject, and running the fixed 10-entry/
three-session supervisor. Actual rebuild hashes must be recorded separately.

This bounded matrix is complete. It qualifies these Linux synchronous and bare-
caught-denial models, not Windows impact, all exception/retention shapes, general
application latency, GC-independent accounting or performance relative to CPython.
The main runtime lead remains complete explicit ownership/root/resource coverage,
not another relief tier or configuration switch. See the
[ownership rule assessment](../2026-10-12-memory-roots/OWNERSHIP-RULE-2026-10-13.md).
