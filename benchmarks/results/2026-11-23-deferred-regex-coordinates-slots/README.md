# B1441 Defer scalar coordinates within existing capture slots

Private `8c3f4fe0d` uses existing match storage for a projection marker
and the cursor's scalar anchor. A narrowed cursor admits one shared
window descriptor. When a script first asks for a scalar coordinate,
one ordered scan publishes all coordinates. Group text uses byte
ranges. The final match keeps no provider operation or token.

Compared with `e8fd8b73f` on the quiet Azure F4as_v7, Ubuntu 24.04.4 and
.NET 11 RC3, on November 23, 2026 UTC. Only the core DLL differs
across the two verified 32-file images.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Unicode named group text | Warm | −1.44 to +0.11% | 0.00 to +0.19% | 0.41 / 1.16% |
| ASCII named group text | Warm | +1.44 to +2.65% | +1.45 to +2.89% | 0.28 / 0.90% |
| 80-group Unicode register reads | Warm | +0.56 to +3.81% | +1.15 to +3.45% | 2.74 / 0.49% |
| Named template rewrite | Warm | −0.67 to +1.41% | +0.45 to +1.54% | 0.63 / 1.46% |
| Interleaved group text and offsets, narrow window | Warm | +0.76 to +2.75% | +1.54 to +1.82% | 1.15 / 0.82% |
| Large positional control | Warm | +1.78 to +3.38% | +1.01 to +4.12% | 1.11 / 0.45% |
| Named template rewrite | Compile to result | −0.20 to +0.46% | −0.96 to −0.57% | 0.28 / 0.38% |
| Compile and apply 64 named rules | Warm | −2.51 to −0.30% | −2.89 to −0.96% | 0.53 / 1.73% |

Ranges span pairings of medians from two independent processes per
source, each with eight seconds of warmup and seven rotated batches.
Warm elapsed includes applicable mediated RAM-backed I/O, output
verification and prior-result disposal. Compile to result adds host
compilation and code-owner disposal. Wider CPU includes worker resets
and protocol handling. Startup and final teardown are outside elapsed.

Keep the candidate private. Named group text and wide coordinate reads
avoid the allocation and scanning regressions in the earlier designs.
Unicode group text has flat CPU. ASCII and both offset controls are
slower, as is the unchanged positional control. The named-rule script
uses less CPU. The source and observations do not isolate the
positional result. Continue with the retained capture layout unless a
different cost is established.

Warm local allocations and peaks are effectively flat for the five
original goldens. The narrow control allocates 0.71% more and its
admitted peak drops 32 bytes. The wide control allocates 0.22% less at
the same admitted peak. All allowances return to baseline.
Windows .NET 11 and Ubuntu .NET 10 pass 437 focused Release checks each,
and both images match the seven CPython goldens. All 224 quiet batches
pass. The first staging attempt refused a missing archived replica
before changing any VM files. The replacement staging uses verified
Git evidence and retains the original failure. Both quiet checks,
worker joins, fixture removal and archive verification pass.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) ·
[Observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[Earlier prototypes](../2026-11-23-regex-deferred-coordinates/README.md).
