# Windows embedding observer: bounded feasibility pilot

This pilot tests the measurement setup, rather than qualifying host latency.
The fixed Windows window admits the required observation count, and declared
preparation makes the retained host graph comparable across the selected jobs.
Complete CSV and file jobs use the existing genuinely delayed host. Production
source and protection policies are unchanged.

The measured producer is `a29c64db1d6c8814adf78111c5c9eb998320a05f`, production
tree `32b91f2209a861070f2d5aeccbc61c2fdf32adf2`, collected on 2026-10-12 on
Windows machine `LOKAD-0399`, .NET runtime 10.0.12 and 28 logical processors.
Exact runtime, loaded DLL/source/fixture hashes and
raw observations are retained in [evidence.json](evidence.json) and [raw/](raw/).
The local SDK identity observed immediately after collection was
`10.0.300-preview.0.26177.108`; this differs from the pinned historical Linux
toolchain and is another reason to keep the models separate.
These are warm complete-job observations after compilation and two warmup jobs;
startup and compilation are outside the observation window.

## Prospective model and completion gates

This is a new `embedding-v2-windows-feasibility` model. The historical
[embedding-v1 report](../2026-10-12-embedding-impact/README.md), including its
Windows sample-count failures and file heap-age exclusions, remains unchanged.
The models must not be pooled or interpreted as an old/new performance comparison.

Each independent child retains 128 MiB in 131,072 managed byte arrays. Two fixed
blocking, noncompacting Gen2 preparation collections promote that graph before
warmup. Their counts are reported separately; measured counts begin after the
same two-second preparation pause. This is an explicit artificial old-heap model,
with inherited runtime settings during measurement. The probe adds no runtime
GC/tiering overrides. Preparation collections
do not demonstrate independence from forced GC in guest execution.

The observer preserves `Thread.Sleep(1)` and records timer/scheduler lateness
plus JSON-request service time, with heartbeat lateness recorded separately.
The declared observation window is 20 seconds, finishing the last complete job.
There is no busy-spin observer, native timer API or changed sample threshold.
Three independent sessions reverse the case order in the middle session. Every
child has a 28-second deadline and at most one second of kill/cleanup time.

Gates require at least 1,000 observations, an entirely Gen2 host graph, exact
canonical-script stdout/output-file bytes, the unchanged 256 KiB execution cap,
and a genuinely incomplete `RunAsync` task for every asynchronous job. The existing
delayed host adds 25 ms per host operation; its exposure differs from a saturated
synchronous workload. The benchmark caller waits for that task, while runtime
execution yields to real asynchronous host operations.

| Case | Children passing | Observations per child | Complete jobs per child | Measured Gen0 / Gen1 / Gen2 |
| --- | ---: | ---: | ---: | --- |
| control | 3/3 | 1,236–1,255 | 0 | 0 / 0 / 0 |
| csv-summary-async | 3/3 | 1,231–1,248 | 72–73 | 3,382–3,446 / 2–4 / 0 |
| file-manifest-async | 3/3 | 1,225–1,241 | 47 | 1 / 0 / 0 |

All nine children exited in 22.589–23.584 seconds. No exclusions were retried.
Measured CSV collections remain process-wide,
and these totals include natural/observer allocations.

## Interpretation and limits

This shared Windows machine has no quiet-machine/noise or runtime-environment
admission proof. Raw
request percentiles and stalls remain diagnostic data; no latency target, SLO,
CPython ratio or general performance claim is qualified here. Async text/JSON,
retained-pressure impact and a quiet matched host-only/current comparison remain
open. CSV's process-wide collection frequency is still an architectural concern.

The initial candidate build failed because an obsolete constant-false retention
branch became unreachable under warnings-as-errors. That branch was removed from
this finite pilot, and the final source builds without warnings. The failed build
receipt is preserved separately from the final build and measured producer.

## Reproduction

Generate independent fixtures with `tools/verify-representative-scripts.py`,
build [Probe.csproj](reproduce/Probe.csproj) in Release with `-p:SubjectRoot`
pointing to the chosen Lython checkout, and invoke `Probe.dll` with one of
`control`, `csv-summary-async`, `file-manifest-async`, the generated fixture root
and `benchmarks/scripts`. Use [run-matrix.ps1](reproduce/run-matrix.ps1),
from the repository root with
`-FixtureRoot` and a new `-OutputDirectory`, for all three sessions;
it preserves failures and owns process deadlines/cleanup. Rebuilds need their own
actual source and binary identities. No release NuGet artifact is produced.
