# B1368 Isolate nested fixed return preparation

Compared the frozen `638fc1138` core from delivery
`3d88c8095` with `7a8df051a` on
November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3. The rest of the 32-file image is identical, including
the benchmark worker and optional adapter.

The refinement keeps the ordinary call signature and retirement
check. The fixed path uses the tape's existing ownership bound.
Native-to-native calls forward a fixed tuple return into their
already-funded operand slots. The existing admission check still funds
its retained elements. The result stays private until the callee closes.
The same activation can update an exact dictionary when its existing
storage and key rules allow the write. Tuple length mismatches, capacity
growth, dynamic keys, protocols and actual suspension keep the ordinary
continuation path. The compiler emits the same 23 inspected tapes.

The helper-driven invoice allocates 3,674,390 bytes
per warm run on Windows, down from 6,098,548
bytes (**39.75%**). The implementation
removes the temporary returned tuple and an otherwise materialized
caller when the funded dictionary accepts the write. Every admitted
peak matches and every borrowed allowance returns to baseline.
All eight complete workloads and six targeted semantic probes match
CPython 3.13.2 on both frozen images.

## Quiet comparison

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread old/new |
| --- | --- | ---: | ---: | ---: |
| Nested-helper CSV invoice | warm | -18.34 to -17.71% | -17.76 to -16.66% | 0.02 / 0.75% |
| Quoted policy CSV invoice | warm | -1.01 to +1.51% | -0.74 to +2.27% | 0.90 / 1.63% |
| Mixed factory | compile to result | -0.74 to +7.57% | +0.33 to +8.75% | 5.82 / 2.42% |
| Function-scoped helper | warm | -3.51 to +1.48% | -3.40 to +1.95% | 4.94 / 0.22% |

Negative changes favor the candidate. Ranges span all pairings of two
process medians per image. The sample has seven rotating batches per
worker after at least eight seconds of warmup. Warm clocks include the
run, applicable mediated RAM-backed I/O, output verification and
prior-result cleanup. Compile to result adds compilation and disposal
of the compiled owner. Wider CPU also includes the worker's reset and
request handling. Startup remains separate.

All 112 timed batches and both quiet gates passed in
222 seconds. Workers joined and the raw
archive was checked under the lease before cleanup. The exact source
pair passes 912 focused Release checks per platform. The checks cover
wider returns, repeated aliases, target order, capacity growth, real
host suspension, cancellation, refusal and shared allowance reuse.
The initial control regressions and the previous platform fixture
failure remain in B1367.

Local Windows x64 disassembly without tiering shows that the generic
instruction prefix returned from 11,970 bytes in B1367 to the original
11,896 bytes. The native call helper went from 809 bytes originally to
1,135 in B1367 and 871 here. This establishes the code-size change. The
quiet clocks use normal Linux tiering.

The first storage preflight stopped before contacting the VM. The
launch reused the byte-identical archived B1367 catalog, reverified
with both B1368 images and under the VM lease. The original disk floor,
timing phases and goldens remain in effect.

## Decision

Retain this narrow source. The invoice uses 17.71–18.34% less warm
elapsed time and 16.66–17.76% less wider process CPU. The quoted
policy invoice no longer shows a consistent regression, and the
function-scoped helper spans zero. Mixed first use remains open
regression debt. Its elapsed range is −0.74 to +7.57% and its wider
CPU rises 0.33–8.75%. Same-image reference workers differ by
5.82%. B1367 also saw higher mixed CPU. The complete CSV gain and
removed tuple/frame work justify retaining the change with that
debt recorded. Reassess first use before the release boundary.

[Summary](summary.json) · [Local allocation](allocation-summary.json) ·
[Executable shape](shape-summary.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[First version and adverse controls](../2026-11-22-native-fixed-returns/README.md).
