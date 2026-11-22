# B1367 Native nested fixed returns

Compared the frozen `638fc1138` core from delivery
`3d88c8095` with `2ae6b61c9` on
November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3. The rest of the 32-file image is identical, including
the benchmark worker and optional adapter.

Native-to-native calls can now forward a fixed tuple return into their
already-funded operand slots. The existing admission check still funds
its retained elements. The result stays private until the callee closes.
The same activation can update an exact dictionary when its existing
storage and key rules allow the write. Tuple length mismatches, capacity
growth, dynamic keys, protocols and actual suspension keep the ordinary
continuation path. The compiler emits the same 23 inspected tapes.

The helper-driven invoice allocates 3,674,390 bytes per warm run on
Windows, down from 6,098,548 bytes (**39.75%**). The implementation
removes the temporary returned tuple and an otherwise materialized
caller when the funded dictionary accepts the write. Every admitted
peak matches and every borrowed allowance returns to baseline.
All eight complete workloads and six targeted semantic probes match
CPython 3.13.2 on both frozen images.

## Quiet comparison

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread old/new |
| --- | --- | ---: | ---: | ---: |
| Nested-helper CSV invoice | warm | -20.83 to -14.90% | -21.03 to -14.08% | 4.51 / 2.86% |
| Ordinary CSV invoice | warm | -1.87 to +1.18% | -0.93 to +0.94% | 1.33 / 1.76% |
| Quoted policy CSV invoice | warm | +3.16 to +4.86% | +3.49 to +4.60% | 1.48 / 0.16% |
| JSON Lines shipment report | warm | -1.85 to +0.07% | -0.97 to +0.13% | 1.46 / 0.49% |
| Function-scoped helper | warm | -2.91 to +18.69% | -2.00 to +19.16% | 3.67 / 17.92% |
| Module-scoped helper | warm | +0.46 to +2.69% | -0.44 to +2.01% | 0.21 / 2.01% |
| Mixed factory | compile to result | +2.38 to +7.79% | +1.29 to +8.89% | 2.18 / 3.04% |

Negative changes favor the candidate. Ranges span all pairings of two
process medians per image. The sample has seven rotating batches per
worker after at least eight seconds of warmup. Warm clocks include the
run, applicable mediated RAM-backed I/O, output verification and
prior-result cleanup. Compile to result adds compilation and disposal
of the compiled owner. Wider CPU also includes the worker's reset and
request handling. Startup remains separate.

All 196 timed batches and both quiet gates passed in
386 seconds. Workers joined and the raw
archive was checked under the lease before cleanup. The exact source
pair passes 912 focused Release checks per platform. The checks cover
wider returns, repeated aliases, target order, capacity growth, real
host suspension, cancellation, refusal and shared allowance reuse.
The first local test conditions and the preceding Windows CI's
line-ending-sensitive diagnostic failure are preserved. The assertion
now compares offsets in the frontend's normalized source.

## Decision

Keep this first version private. The nested-helper invoice runs
14.90–20.83% faster and uses 14.08–21.03% less process CPU, alongside
a 39.75% allocation reduction. The quoted policy invoice takes
3.16–4.86% longer and the mixed factory takes 2.38–7.79% longer
from compile to result. The module helper takes 0.46–2.69% longer
elapsed, with its CPU range crossing zero. Function-helper timing
remains uncertain with 17.92% candidate worker spread.

Refine the shared retirement and native return branch so ordinary
calls keep the original completion path. Preserve the fixed
destination and exact dictionary path. Use the original adverse
controls and the complete affected invoice to decide whether the
simpler route earns promotion.

[Summary](summary.json) · [Local allocation](allocation-summary.json) ·
[Executable shape](shape-summary.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Attribution before the change](../2026-11-22-post-checked-local-call-profile/README.md).
