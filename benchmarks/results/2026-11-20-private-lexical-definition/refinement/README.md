# B1230 Isolate private capture admission

Compares retained `46ccc2efa` against private `559242ac1` with the same
catalog and measurement boundaries as [B1228](../confirmation/README.md).
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC.
Two processes per revision use 12-second warmups and seven rotated
0.8-second batches. Ranges cross all four process-median pairings.

| Warm script | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: |
| Postponed nested factories | +1.14% to +4.61% | +0.61% to +5.06% | 1.13% / 2.28% |
| Retained nonlocal siblings | −9.70% to −2.75% | −10.54% to −2.38% | 6.95% / 0.69% |
| CSV invoice | −2.02% to +0.09% | −1.36% to −0.23% | 1.58% / 0.56% |

The capture path still removes one shared-budget resize per capturing
factory. A common allocator constructs the callable and annotations after
admission. Capture-free and dynamic construction avoid the capture path.
The ordinary callable entry is 903 machine-code bytes under the same
local tiering-disabled probe, compared with 896 before the prototype and
1,061 for the combined entry. The captured entry is 1,004 bytes. Timing
changes cannot be attributed to those sizes alone.

Keep both versions private. The refined shape narrows the earlier
postponed slowdown, which still appears in every elapsed and CPU pair.
The siblings improve again, with a 6.95% reference process spread. The
invoice elapsed spans zero and process CPU is lower in this round. All
previous observations remain available in B1227 and B1228.

Windows .NET 11 and Ubuntu .NET 10 each passed 882 focused Release checks.
Both complete images verified the four workload goldens. The refined
image matches ten CPython probes in both execution modes. Allocations
are effectively flat. All allowances return to baseline at the same
accounted peaks. Both quiet checks and all 84 timed batches passed.
Workers joined, fixtures were removed and the persistent archive verified.
One superseded VM core copy was retired after verifying the reconstructible
Git image and committed raw archive.

[Timings](summary.json) · [Admission counts](admission-counts.json) ·
[Raw observations](observations.tgz) · [Source, checks and procedure](source-and-checks.tar.xz).
