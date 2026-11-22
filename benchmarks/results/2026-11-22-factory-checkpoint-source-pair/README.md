# B1412 Pair the two Lython factory checkpoints

Compared the exact Lython images from [B1397](../2026-11-22-retained-source-cpython/README.md)
and [B1406](../2026-11-22-current-source-cpython/README.md) on November 22,
2026 UTC on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Only the core DLL differs across the 32 files. These original revisions
are `32c50290d` and `55424591f`. Both images match three complete CPython
goldens. This comparison collects Lython timings.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed worker spread old / new |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | +0.49 to +18.36% | +1.71 to +19.15% | 2.50 / 14.91% |
| Postponed nested factories | Compile to result | −2.39 to −1.93% | −3.35 to −1.02% | 0.12 / 0.36% |
| Nonlocal Unicode word report | Warm | −6.80 to −0.28% | −6.75 to −0.99% | 1.81 / 5.08% |
| Quote-heavy CSV invoice | Warm | −1.29 to +1.67% | −2.22 to +2.27% | 1.18 / 1.80% |

Ranges pair the medians of two independent workers per source. Each
worker warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes applicable mediated RAM-backed I/O, verification
and prior-result cleanup. Compile to result adds compilation and
compiled-owner cleanup. Wider CPU includes resets and protocol work.
Startup and final teardown sit outside the elapsed boundaries.

The warm factory regression appears in every pairing on both clocks.
The later workers take 1.087 and 1.249 ms, so the size depends strongly
on worker selection. Compile to result improves with stable workers.
This identifies warm factory work as the next target. The source pair
does not isolate a specific commit or explain the worker spread.

Two instrumented Windows .NET 11 executions per exact image verify
the factory result and the same admitted peak of 2,968,481 bytes.
Both images make 6,169 positive allowance moves. Three construction
requests account for 6,144 of them in both images. Each occurs 2,048
times. They reserve 256 and 672 bytes in callable construction and 520
bytes in default storage. Every test result and compile owner refunds.
These are admission counts and call stacks, without timing attribution.

All 112 timed batches and both quiet gates pass. All workers join, the
guest fixtures are removed and the persistent archive verifies under
the lease. Staging retires only a byte-verified duplicate of the B1411
candidate and preserves its recorded image and archive.

[Images](source.json) · [Timings](summary.json) · [Reservation counts](local-reservations.json) ·
[Observations](observations.tgz) · [Verification and procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
