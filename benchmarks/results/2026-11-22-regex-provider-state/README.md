# B1403 Pass regex provider operation state directly

Frozen `d97fc617c` and `f1692ab57` ran on November 22, 2026 UTC
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
The core DLL is the sole difference in the 32-file images.
The production patch matches retained `5ee6cbac0`. The reference
has the same production source as `248266577` and uses its
verified frozen core. The retained histogram change is outside
this measured pair.

The same guarded provider entry now receives explicit operation
state and a static handler. Subject, cursor, compilation and
metadata calls drop their per-operation closures and delegates.
Provider access stays inside the cancellation, admission and
error boundary. Acquired handles still close when cancellation
or refusal arrives before handoff. The public contract and
optional PythonRe 0.4.0 adapter are unchanged.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread old / new |
| --- | --- | ---: | ---: | ---: |
| Named captures over 3,072 Unicode rows | Warm | −0.35 to +1.71% | −1.69 to +3.13% | 1.76 / 0.30% |
| Named captures over 3,072 ASCII rows | Warm | +0.77 to +1.61% | −0.09 to +1.45% | 0.68 / 0.15% |
| Rewrite 96 file lines using named groups | Warm | −1.98 to +0.93% | −2.41 to +1.09% | 1.28 / 1.67% |
| Compile 64 named rules | Warm | −3.22 to −2.26% | −3.21 to −1.15% | 0.03 / 0.97% |
| Positional call control | Warm | −6.99 to +1.42% | −7.33 to +1.49% | 8.42 / 0.58% |
| Rewrite 96 file lines using named groups | Compile to result | +0.29 to +1.09% | +0.35 to +1.34% | 0.29 / 0.50% |

Each row spans comparisons between the medians of two processes
per source after eight seconds of full-request warmup and seven
rotated batches. Warm elapsed includes guest regex compilation
where applicable, mediated RAM-backed file I/O, output checks
and prior-result disposal. Compile to result adds host
compilation and code-owner cleanup. Wider CPU includes resets
and request handling. Startup and final teardown have separate
boundaries. The unchanged positional control has 8.42% reference
worker spread. Both images verify five CPython goldens. There
is no new qualified external ratio or producer-performance claim.

Retain the allocation reduction at the existing provider boundary.
The named-rule batch allocates 22,016 fewer bytes locally (5.36%),
and file rewrites allocate 17,120 fewer (1.74%). Each matching
report allocates 400 fewer bytes. Positional allocation and all
admitted peaks match. The shared allowance returns to baseline.
Record the stable ASCII warm and rewrite first-use slowdowns
alongside the smaller named-rule CPU cost. The separate desktop
samples have wide variation and include adverse timings.

Windows .NET 11 and Ubuntu .NET 10 each pass 708 focused Release
checks. The first isolated Ubuntu run had five missing scenario
fixtures and three worker-image mismatches. The corrected rerun
stages the source fixtures and verifies identical loaded core,
adapter and producer files. The follow-up build fix at `cbe19cd78`
automates staging and removes the ancestor-directory searches.
All 168 quiet batches verify, workers join and the persistent
archive verifies after the guest mount is removed.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz) ·
[Reference core](../2026-11-22-prepared-histogram/reference-core.dll.gz).
