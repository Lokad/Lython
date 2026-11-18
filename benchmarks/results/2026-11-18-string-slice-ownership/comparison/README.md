# Complete log extraction with admitted UTF-8 slices

On 2026-11-18 UTC, B971 compared 98b9f87f7 (27cb473e) with 227d80b47
(f2824fea0). The 32-file worker images differed only in the core DLL.
The Azure F4as_v7 VM used .NET 11 RC3, ordinary GC and tiering, and the
optional 0.4.0 adapter. Both images matched the CPython verifier's stdout
and file outputs before measurement. This is a Lython revision comparison.

The 2,048-line fixture contains 158,025 UTF-8 bytes of mixed ASCII and
Unicode. A script copies accepted event IDs and decodes their interleaved
labels into 1,927 tab-separated rows. It exercises prefix and suffix removal,
contiguous slices and strided slices. The unchanged positional-call script
controls for movement outside those operations.

| Complete script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Log extraction, warm | −7.21 to −1.70% | −7.81 to −1.38% | 2.05 / 3.80% |
| Log extraction, compile-to-result | −4.53 to −2.61% | −6.57 to −3.40% | 0.48 / 1.52% |
| Positional calls, warm control | −0.05 to +1.96% | −1.60 to +2.08% | 0.07 / 1.94% |

Changes cover all pairings of the two process medians from each build.
Negative favors the candidate. Each process had eight seconds of rotated
warmup and five measured rounds. Warm includes mediated RAM I/O, output
checks and prior-result cleanup. Compile-to-result adds compilation and
compiled-owner disposal. Process CPU also covers wider worker activity.

A separate local diagnostic measured 3,791,076 to 3,852,734 CLR bytes per
extraction, up 1.63%. The 61,658-byte difference is consistent with one
32-byte temporary reservation per strided result. Both builds reached the
same 2,775,855-byte accounted peak and returned their allowance to baseline.
The control matched in allocation and peak. Keep the terminal-refusal fix,
with the allocation debt and control movement visible. The favorable elapsed
and CPU ranges are specific to this extraction workload.

All three jobs finished in 141.04 seconds. Quiet endpoints, joined workers,
fixture cleanup and SHA verification passed. Staging retired only a
superseded B852/B853 core replica and two exact archived observation
replicas. Source and raw evidence remain available. Full cross-platform
test and package-consumer qualification is separate.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Verified inputs and procedures](inputs-and-procedures.tar.xz)
