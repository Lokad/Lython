# B1773 quiet confirmation

The same `dc736119e` reference and candidate patch ran on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 26, 2026 UTC.
Two processes per image and lane warmed for five seconds each, then ran
seven rotated batches with a 440 ms target. The ranges span all four pairings
of process medians. Negative changes mean lower time. [Worker medians and controls](summary.json)

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment control | Warm async | −7.15% to −0.26% | −6.78% to −0.04% |
| Ordinary definitions | Warm sync | −2.66% to −1.98% | −1.98% to −1.66% |
| Escaping rules | Warm sync | −2.19% to +2.80% | −1.37% to +3.86% |
| Ordinary definitions | Warm async | −1.13% to +0.27% | +2.09% to +2.61% |
| Ordinary definitions | Compile-to-result | −2.05% to +0.59% | −0.91% to +1.45% |

Elapsed includes execution, verification and result cleanup. The shipment
control uses RAM-backed fixture I/O. Compile-to-result includes compilation
and compiled-owner disposal. Wider process CPU includes request framing and
fixture reset. No workload has an external timing baseline here. [Local block and allocations](../README.md)

Keep the literal-tuple ownership simplification and the smaller workspace.
The ordinary factory uses less warm sync CPU. Its async CPU rises with less
than 0.37% same-image CPU spread, although async elapsed crosses zero. The
local escaping-rule slowdown does not reproduce, and the shipment control
varies by up to 4.51% in elapsed between workers of the same reference image.
Recheck the ordinary factory's async CPU at the next callable delivery
boundary. Use a profile to select any correction if that increase persists.

All 32 files in each image match before launch. The original independent
CPython goldens also pass with the staged candidate on the VM. The quiet
gates pass before and after the collection, all workers join, and guest
fixtures are removed. Only verified duplicate expansions of earlier archives
were removed to stage the images. [Frozen image hashes](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures and storage verification](procedures.tar.xz)
