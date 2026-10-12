# Linux async embedding impact: four complete jobs

All four complete async jobs meet the prospective finite host-model targets:
added request p99 <=5 ms and largest added heartbeat stall <=25 ms against their
matched host-only controls. There are **24 eligible independent samples**, no
excluded/retried samples, and no Gen2 collection in any measurement window.
The fixed campaign finished in **183.97 seconds**.

This closes the four-workload Linux async measurement gap. It does not establish
GC-independent accounting, Windows host latency, retained-pressure latency, a
general application SLO or performance relative to CPython.

## Results and measured revision

Values are milliseconds and ranges across three independent sessions. Added
values subtract that session's same-workload host-only control. Collection counts
cover each complete observation window, including natural/observer allocations;
they are not an isolated count of induced events.

| Async workload | Eligible pairs | Workload request p99 | Added request p99 | Largest added heartbeat stall | Measured Gen0 / Gen1 / Gen2 |
| --- | ---: | ---: | ---: | ---: | --- |
| csv-summary | 3/3 | 0.408–0.423 | 0.344–0.357 | 0.551 | 892–893 / 0 / 0 |
| text-summary | 3/3 | 0.262–0.362 | 0.199–0.301 | 0.490 | 418–437 / 0 / 0 |
| json-summary | 3/3 | 0.390–0.402 | 0.326–0.340 | 0.651 | 520–523 / 0 / 0 |
| file-manifest | 3/3 | 0.068–0.073 | 0.004–0.008 | 0.030 | 0 / 0 / 0 |

Producer: `4cdfd6b9f4f52e34ce713dfd20b7820083db8961`, production tree
`32b91f2209a861070f2d5aeccbc61c2fdf32adf2`, collected 2026-10-12 on the dedicated Azure Linux
VM with four processors, .NET SDK 10.0.401/runtime 10.0.12. Exact DLL/source/fixture
hashes and runtime identity are in [evidence.json](evidence.json). The subject is
built from a verified Git archive; its source revision metadata is supplied from
that archive's commit. No production code or dependency changed.

CSV completes 19 jobs per window, text 22–23, JSON 14 and files 13. CSV still performs
roughly 220 Gen0 collections/s. These completion counts describe exposure to
allocation/collection traffic with delayed I/O, rather than a throughput comparison.
They must not be compared with synchronous or traced producers as speed ratios.

## Prospective model and gates

This is `embedding-v3-linux-async`, separate from the historical
[v1 Linux synchronous experiment](../2026-10-12-embedding-impact/README.md) and
[v2 Windows feasibility pilot](../2026-10-12-embedding-observer-v2/README.md).
Their failures/exclusions stay unchanged; observations are not pooled.

Each child retains 128 MiB in 131,072 managed byte arrays. Two declared blocking,
noncompacting preparation collections make this graph entirely Gen2. Compilation,
heap preparation, two complete async warmup jobs and a fixed two-second pause are
outside the four-second observation window. Preparation collection counts are
reported separately. Warm output files/fixtures and the script match within each
workload/control pair; each child starts independently.

A managed observer thread issues a small JSON request after a nominal 1-ms sleep.
Request latency includes timer/scheduling lateness plus service time; heartbeat
lateness is retained separately. This is a closed-loop request model with think
time, not a fixed offered-load/queueing model. The existing delayed host adds 25 ms
per host operation. Workload runs genuinely yield through RunAsync; the caller
waits while the runtime executes/awaits, and controls perform no measured guest jobs.

Three sessions reverse case order in the middle session. Eligibility requires an
exclusive VM lease, ordinary GC/tiering settings with runtime-override admission,
idle CPU (median<=3%, maximum<=5%), no paging/steal, exact source/DLL/fixture/output
identities, all-old graph, >=1,000 observations, complete outcomes and the unchanged
256 KiB execution cap. Every child has a 28-second deadline plus at most one second
of owned kill/cleanup; the fixed campaign limit is 240 seconds. The longest child
took 7.452 seconds including cleanup.

Preparation and matrix services are inactive/dead, their cgroups are gone and the
lease is free; [terminal proof](reproduce/terminal-proof.json) is retained. No
ten-minute lane, GC suppression, native timer/stack telemetry or protection
bypass was used. Source/binary identities are checked before and after collection.

## Evidence, reproduction and remaining work

[Raw requests/receipts](raw/), [validation](validation.json) and
[probe/preparation/runner sources](reproduce/) retain every sample and gate.
Generate the independent fixtures with tools/verify-representative-scripts.py;
build the probe in Release against the declared subject revision, then run the
fixed eight-entry/three-session supervisor. The supplied manifest identifies all
fixture bytes; [source-archive-paths.json](reproduce/source-archive-paths.json)
lists the paths passed to `git archive` at the producer revision to create
`subject.tar`. Preparation supplies that archive and the probe to the owned root
used by the runner. Rebuilds need their own actual artifact hashes; these are diagnostic
DLLs, not release NuGet artifacts.

Meeting these finite targets does not remove forced-GC dependence. Current
Gen0 relief still pauses the embedding process; separate traces demonstrate
induced collections. A safe replacement must cover
[execution roots and resource lifetimes](../2026-10-12-memory-roots/README.md).
Current synchronous/Windows qualification, retained-pressure impact and broader
ownership redesign remain distinct gates. CPython is the primary external
performance baseline; this host-impact model provides no CPython ratio.
