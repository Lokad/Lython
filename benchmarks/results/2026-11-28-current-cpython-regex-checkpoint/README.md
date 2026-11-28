# B1932 Compare the current checkpoint with CPython

The November 28, 2026 UTC comparison uses an Azure F4as_v7 running
Ubuntu 24.04.4, .NET 11 RC3 and stock CPython 3.13.16. The frozen
32-file image matches committed `2ef73bd71`. Both regex workloads use
the optional adapter with signed PythonRe 0.5.0. Seven independent
output and file goldens pass both locally and on the VM.

Two workers per engine warm for at least five seconds and supply
seven rotated batches per lane. Ratios divide Lython by CPython and
span pairings of worker medians. Warm elapsed includes applicable
RAM-backed I/O, output and file verification, and prior-result
disposal. Compile-only uses resident source and includes previous
compiled-owner disposal. Compile-to-result also includes execution,
applicable I/O and result cleanup. Process CPU also includes fixture
reset and framing. CPython uses its synchronous file APIs with
`-I -S -X utf8`. Startup sits outside both clocks.

| Workload | Boundary | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Iterable-row shipment | Warm RunAsync | 2.20046–2.21376 ms | 1.94904–1.95527 ms | 1.125–1.136× | 1.127–1.161× |
| Compiled-pattern regex log search | Warm | 2.67783–2.71755 ms | 0.96541–0.97195 ms | 2.755–2.815× | 2.625–2.728× |
| Generated ordinary definitions | Compile only | 0.50068–0.50556 ms | 0.94442–0.95310 ms | 0.525–0.535× | 0.537–0.541× |
| Iterable-row shipment | Warm | 1.99402–2.01709 ms | 1.95130–1.95304 ms | 1.021–1.034× | 1.024–1.035× |
| Module-level regex log search | Warm | 3.04883–3.09664 ms | 1.54657–1.56180 ms | 1.952–2.002× | 1.926–1.953× |
| Nested JSON document summary | Warm RunAsync | 0.65308–0.66108 ms | 0.67361–0.67902 ms | 0.962–0.981× | 0.999–1.013× |
| File search, first line | Warm | 0.06676–0.06690 ms | 0.05110–0.05118 ms | 1.304–1.309× | 1.019–1.022× |
| Configuration edit | Compile to result | 0.09548–0.09575 ms | 0.12340–0.12556 ms | 0.760–0.776× | 0.714–0.739× |

All 224 timed batches pass in 330.7 seconds. Both quiet gates pass,
workers join, RAM-backed fixtures are removed, and the persistent
archive verifies. The first attempt stopped before timing because
the manifest omitted the auxiliary assembly map. The corrected
attempt used the same binaries, scripts and fixtures. Two redundant VM
core files were reclaimed after their complete local images and remote
observation archives verified. The exact local copies remain available.

Async shipment still shows a double-digit elapsed and CPU gap. First-line
search is about 16 microseconds slower with about 2% more process CPU.
Dense JSON has a small elapsed advantage while process CPU spans parity.
The two regex log scans remain the largest warm gaps in this set. The
B1931 matched comparison establishes lower allocation. The quiet CPython
runs compare distinct revisions and cannot isolate its CPU effect.
[B1931](../2026-11-28-regex-group-resolution/README.md).

## Allocation and admission

A separate Windows .NET 11 allocation witness compares the exact B1926
and B1932 cores on the shipment, dense JSON and compiled log workloads.
The compiled log allocates 114,688 B less in `Run` and 114,862 B less in
`RunAsync`. Shipment and dense JSON differ by at most 618 B. Peak execution
admission and compiled reservations match in both APIs across all three
workloads. Every command returns the borrowed allowance to baseline.

## Current profile

Three WSL EventPipe captures use the same B1932 image, with four seconds
of verified warmup and six seconds of sampling. All 172 sampled batches
match their goldens. The shares below cover resolved Lython stacks after
excluding known waits and unclassified native leaves. Inclusive regions
overlap with their callees.

| Workload | Inclusive sample attribution |
| --- | --- |
| Iterable shipment, `RunAsync` | Generator advancement 71.3%, outer pull continuation 51.1%, bound generator factory 6.7% |
| Iterable shipment, `Run` | Generator advancement 73.4%, bound generator factory 11.8% |
| Dense JSON, `RunAsync` | JSON construction 34.8%, converted dictionary update 29.0%, accumulated text windows 6.5% |

The async outer pull continuation is the nearest Lython method in 2.12%
of samples. The bound generator factory is nearest in 3.07% async and
8.12% sync. Advancement and continuation regions include guest work.
In dense JSON, byte-builder growth is nearest in 6.01%. The earlier
B1922 trace also places most of this growth below the text reader's
append operation. An opened file's length hint is advisory. Further
reader work needs a removable copy or ownership stage with supported
decoding, cancellation and refusal behavior. [B1922](../2026-11-28-reader-checkpoint-profile/README.md).

[Timing](summary.json) · [Allocation](allocation.json) · [Image identity](identity.json) ·
[VM observations](observations.tar.xz) · [Profile attribution](profile-attribution.json) ·
[Raw profiles](profile-observations.tar.xz) · [Procedures and initial attempt](procedures.tar.xz).
