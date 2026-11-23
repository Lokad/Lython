# B1452 Initialize frontend catalogs independently

`c6d2f2b3d` separates module, callable, member-value, return-shape and
member-surface metadata. The first contract lookup no longer creates
every catalog or checks unrelated module aliases. The module parity suite
checks registrations, submodules and callable targets. The combined
callable and return staging copies disappear. The lookup maps own their
final data. Workbook-specific callable and surface data initialize on
first use of a workbook receiver.

Compared `dfabddb88` and `c6d2f2b3d` on November 23, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The two verified
32-file images differ only in the core DLL. The worker protocol and
stock CPython 3.13.16 match B1451.

| Fresh process | Reference median | Retained median | Median elapsed change | Changes by paired round |
| --- | ---: | ---: | ---: | ---: |
| Configuration edit | 278.8 ms | 270.1 ms | −3.13% | −7.81 to −0.30% |
| Typed ASCII CSV meter | 332.3 ms | 321.7 ms | −3.20% | −3.82 to −1.30% |
| Small XML selection | 253.1 ms | 242.5 ms | −4.19% | −6.75 to −0.62% |
| Empty control | 123.2 ms | 124.5 ms | +1.08% | −3.12 to +2.23% |
| Tiny arithmetic control | 152.9 ms | 152.2 ms | −0.45% | −3.82 to +9.32% |

The existing runner times launch through exit and pipe drain. The clock
includes runtime and worker initialization, compilation, one execution,
applicable RAM-backed I/O and in-worker cleanup. OS caches stay warm.
Seven fresh starts per case and Lython revision alternate source order
and rotate case order. Each smoke runs Lython first before its CPython
control. The paired ranges preserve all rounds. These diagnostic medians
are useful for distinguishing the observed first-use effects from the
independent warm compiler costs. [B1451](../2026-11-23-runtime-cpython-checkpoint/README.md).

| Warm compilation | Elapsed change | Wider process CPU change | Worker spread reference / retained |
| --- | ---: | ---: | ---: |
| Configuration edit | −0.49 to +0.81% | −2.40 to +2.55% | 0.74 / 0.56% |
| Typed ASCII CSV meter | −3.78 to −1.21% | −1.12 to +0.26% | 1.68 / 0.98% |
| Small XML selection | −0.14 to +1.10% | −0.02 to +4.00% | 0.97 / 0.27% |
| Generated read-only validator | −1.84 to +3.51% | −2.26 to +4.72% | 3.59 / 1.79% |

Two workers per revision warm for eight seconds and supply seven rotated
timed batches each. Ranges span all pairings of process medians.
Elapsed includes prior compiled-owner disposal. The wider CPU clock also
includes framing. The CSV elapsed result is favorable. Warm CPU spans
zero, including the adverse side of the XML and validator ranges.

A separate Windows diagnostic measures allocations on the calling
thread during compilation and disposal. Cold allocation falls
1,346,024 → 1,119,744 bytes for configuration, 1,400,280 → 1,186,224
for typed CSV and 1,331,304 → 1,065,184 for XML. The workbook control
falls 1,316,536 → 761,384 bytes. Empty, tiny and read-only cold
allocation match. Warm allocation and admitted peaks match in all
seven scripts. Every compilation refunds the allowance on disposal.

JIT events from the exact Windows images show that configuration never
initializes the workbook callable or member-surface catalogs. The
workbook control initializes both and skips the general callable
catalog. Both images agree on the workbook output. The source and
these controlled runs justify retaining the initialization change.

Windows .NET 11 and Ubuntu .NET 10 each pass 60 white-box and 431 public
focused Release checks. Both images match six independent CPython
goldens. All 70 fresh Lython/CPython pairs and 112 warm batches verify.
Both quiet gates pass, workers join, the archive verifies under the
lease and the guest fixture mount is removed.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and allowance](allocation-summary.json) · [Constructor traces](constructor-summary.json) ·
[Raw quiet observations](observations.tar.xz) · [Checks and procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
