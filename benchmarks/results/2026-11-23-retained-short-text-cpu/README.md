# B1488 CPU attribution for retained short and mixed-width text

The November 23, 2026 UTC diagnostic sampled `9cea1fc0a` on the quiet
Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. The measured `f2ac9fa89`
image has identical production sources and all 32 files verify. Each phase
uses one process with eight seconds of warmup and about five seconds of
CPU-clock sampling at 199 Hz. `perf` samples all worker threads with a JIT
symbol map. Worker clocks include mediated RAM-backed I/O, verification,
and prior-result disposal. Compile to result also includes compilation and
compiled-owner disposal. Process samples include worker activity outside
those clocks. This attribution carries no source-pair timing claim.

| Nearest resolved method group | Short report warm | Mixed-width histogram warm | Short report compile to result |
| --- | ---: | ---: | ---: |
| All process samples | 1,023 | 1,006 | 1,031 |
| Resolved runtime ancestry | 959 | 936 | 928 |
| Native instruction dispatch | 302 | 130 | 292 |
| Call entry and operand movement | 70 | 55 | 84 |
| Word decoding and casing | 125 | 298 | 106 |
| Dictionary lookup and accumulation | 125 | 200 | 108 |
| Admission or retirement | 68 | 74 | 46 |
| Host I/O | 46 | 63 | 43 |
| Other resolved runtime | 223 | 116 | 249 |
| Frontend nearest Lython method | 3 | 1 | 32 |
| Unmapped leaf | 116 | 124 | 111 |

The runtime groups classify each sample once by its nearest resolved
runtime method. A signature mentioning a type from another namespace
has no effect on classification. The raw profile retains inclusive
call ancestry separately. Inlining, unresolved addresses and omitted
frames limit estimates of removable work. Frontend and unmapped rows
are independent observations outside the exclusive runtime grouping.

On the short report, `NativeExecution.RunCore` has 147 exact warm leaf
samples and 114 in compile to result. The corresponding mixed-width
warm count is small. Validate an operand or instruction handoff that
actually removes dispatch inside the common helper. Keep the mixed-width
workload as a distinct cost control because its samples concentrate in
casing and dictionary work. The inclusive stacks alone cannot attribute
the short report's gap to call entry or frame setup.

The initial run stopped before sampling because one script still pointed
to a retired core image. That run joined all work, retired the fixture
mount and archived its failed condition. The corrected procedure passes
one verified image path to both collector and worker launcher. All three
CPython goldens and 9,472 profiled invocations verify. Both quiet gates,
worker joins, fixture removal and persistent archive verification pass.

[Attribution](attribution.json) · [Identity](identity.json) · [Raw profiles](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Initial failed run](initial-failed-observations.tar.xz) ·
[Initial terminal status](initial-failed-terminal-status.json) ·
[Earlier source pair](../2026-11-23-short-text-source-pair/README.md).
