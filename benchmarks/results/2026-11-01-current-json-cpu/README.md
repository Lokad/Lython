# Current complete JSON/configuration CPU assessment (B39)

Source **1c369000**, 2026-11-01, dedicated Ubuntu 24.04.4 Azure F4as_v7,
four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3. Two fixed captures,
**6.093 seconds total**, below the prospective 30-second capture / 90-second
collection bounds. Service runtime is 17.792 seconds. Quiet-machine and complete
stdout/file goldens pass before/after; all workers/profilers join, the service is
inactive and the VM lease is available. No candidate, retiming or speedup claim.

## Question and boundary

The recent text/CSV profiles do not cover these complete jobs. This assessment
asks whether default JSON container/callback transport, serialization conversion
or guest dispatch establishes a coherent next redesign. Current source already
has native UTF-8 token decoding and synchronous scalar readers; their existence
does not justify recreating them or blaming container awaiters without evidence.

[Prospective criteria](pre-collection-plan.md) select one fixed pair using the
existing perf driver: 512 warm synchronous public jobs each, ordinary worker
GC/tiering, cpu-clock at 199 Hz with frame-pointer chains and CLR perf maps.
Actual named guest threads identify guest samples. Startup/compilation are
excluded; fixture reset, efficient mediated I/O, full checks and declared cleanup
are included. Prior results are disposed within the batch; final results remain
through response and cached compilations end on worker exit. The normal shared
unbounded allowance keeps ownership/admission paths active; finite-budget refusal
and lifetime behavior are qualified separately. No hard deadline guarantee.

## Observations and limits

| Complete job | Input | Capture | Guest CPU | Guest samples | Unknown |
| --- | --- | ---: | ---: | ---: | ---: |
| JSON helpers | 4,096 records / 110,592 bytes | 4.441 s | 3.60 s | 713 | 39 |
| Configuration edit | 1,024 existing paths / 7,177 bytes | 1.652 s | 0.28 s | 54 | 10 |

JSON's main instruction loop has 108/713 self samples (15.1%); Monitor enter/exit
has 101 (14.2%), distributed over existing reservation resizing, text construction,
operand storage and iteration. ReadJsonString has 32 (4.5%); ReadJsonObjectAsync's
MoveNext has 17 (2.4%). These self samples are **not** whole-parser costs, removable
CPU fractions or proof that an async rewrite would help. Inlining and managed/
native transitions limit attribution; unknown leaves and malformed perf-map tails
remain explicit. All leaf categories and native caller origins are preserved.

Configuration's 54 guest samples are too sparse to establish a dominant mechanism.
Its remaining elapsed time is not labeled as measured I/O, cleanup or compilation
cost by subtraction. All-thread CPU includes 0.69 / 0.72 seconds of tiered-JIT work,
versus 3.60 / 0.28 seconds on the guest threads. These are short diagnostic captures,
with ongoing tiering and profiler overhead, not steady-state or embedding-latency
qualification. No CPython timing comparison is inferred.

## Bounded design decision

No runtime prototype is selected. Source confirms direct UTF-8 string paths,
shared container parsing and genuine optional callbacks. Separate synchronous
default parsers would duplicate delimiter/error/depth/ownership behavior; these
captures do not establish enough removable transport to justify that complexity.
A universal frame/value redesign does not follow from main-loop samples. Native
monitor origins are distributed, not evidence that shared-budget synchronization
can simply disappear. Do not reopen rejected cursor/reservation tweaks.

Stop B39 after this source/attribution decision. The substantial performance gap
remains open; a future design must name concrete deleted state/transport and freeze
its semantic/lifetime and before/candidate/identical-before criteria before code.
No further capture is commissioned to rescue this investigation.

## Evidence

[summary.json](summary.json) records exact loaded hashes, all-thread CPU, gates,
inputs and outcomes. Both workers load the fresh nonincremental core
`076c855f2527f4377a5dfdc2fef81a8b97b7f43cfd52560ce50b2b3f4751b9c5`,
version 0.11.0+1c369000. [Raw capture](raw-capture.tar.gz), SHA256
`5534ab8885a6e81f747331135901209a5bb8613e1b3bb5e6c26a8354f866ba2d`, preserves
perf data/maps, all chains/leaves, records, goldens, machine gates, catalog and
collector logs. Readable collector/analyzer sources accompany it. The first archive
command lacked permission to read root-owned perf data; archiving with sufficient
read access fixed that observation step without repeating either capture.
