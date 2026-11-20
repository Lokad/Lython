# B1236 and B1237 prepared UTF-8 keys for format mappings

`str.format_map` now prepares root names with the UTF-8 keys already used
for bracket selectors. Exact dictionary hits read the live value without
decoding the name or allocating a guest key. A miss or custom lookup still
gets a separately admitted key, which Python can retain. Both branches
reuse the existing execution-owned template and the same subscript
semantics. The refinement keeps successful lookups out of the async
handoff used for misses and protocols.

The input is the 189,441-byte, 3,072-record shipment report previously
measured in B1233. The mapping variant changes the report's format call
and fields. Both variants produce the same output file and stdout as
CPython 3.13.2. The original named `.format` report serves as the control.

| Exact source pair and workload | Phase | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| B1236 `b21a7ac4a` to `f2a38804c`, mapping | Warm | −13.91% to −8.68% | −13.24% to −8.91% |
| B1236 mapping | Compile-to-result | −12.52% to −10.01% | −11.56% to −9.95% |
| B1236 named control | Warm | +0.70% to +1.08% | +0.73% to +1.22% |
| B1237 `b21a7ac4a` to `a82bbee1d`, mapping | Warm | −12.52% to −9.54% | −12.75% to −8.74% |
| B1237 named control | Warm | −0.34% to +1.72% | −0.73% to +1.47% |

The retained refinement confirms the mapping gain. The named control spans
zero with an adverse skew, so a small cost remains possible. The original
adverse observation is preserved and its cause remains unisolated.
Compile-to-result was measured on the first prototype. B1237 timed warm
execution only. Neither comparison times CPython.

In the Windows allocation diagnostic, the mapping report's median drops
from 4,491,894 to 3,975,184 CLR bytes per warm run (11.50%). Peak accounted
execution memory drops from 4,726,513 to 3,500,785 bytes (25.93%). Both
prototypes deliver those same figures and the shared allowance returns to
baseline. The named control's allocation is within 0.06% and accounted
peak is unchanged.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Two workers per image and seven rotated batches per lane give
the four pairings above. B1236 used 12 seconds of warmup per worker.
B1237 used 15 seconds and reversed startup order for the mapping lane.
All 140 batches matched the goldens. Both machine checks passed in each
run and the persistent archives verified under the lease after cleanup.
Warm elapsed includes mediated RAM-backed I/O, golden checks and previous
result disposal. Compile-to-result adds compilation and code-owner
disposal. Process CPU covers the wider request, including fixture reset
and framing. Worker startup and final teardown sit outside guest clocks.

Each prototype verified two CPython scripts through its complete frozen
image. Windows .NET 11 and Ubuntu .NET 10 each passed 1,375 focused public
checks on both variants, including suspended lookups, Unicode keys,
mutation, admission and cleanup. The first prototype also passed 502
focused white-box checks on each platform. The staging records preserve
the first local preflight rejection and the matched immutable archives
used to recover space.

[B1236 timing](summary.json) · [B1237 timing](refinement/summary.json) ·
[Retained allocation](refinement/allocation-summary.json) ·
[Source and checks](checks-and-procedures.tar.xz).
