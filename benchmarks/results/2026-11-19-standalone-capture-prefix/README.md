# B1107 standalone captures in deferred invocations

R675 lets a slot-only factory construct functions and lambdas from cells
in its admitted work state. The compiled parent slot map identifies new
and inherited cells. A completed factory can release its work without
allocating a lexical invocation context. An operation that needs a frame
adopts the same work at the current instruction. Escaping closures keep
their cells and original funding.

The pair is `2166c7295` versus retained `9407f4fb7`. The reference has
the production and test sources of `34072680e`. Both measured cores were
built nonincrementally and frozen with the same worker and adapter files.
Windows .NET 11 and Ubuntu .NET 10 passed 251 focused Release checks
each. Six CPython goldens passed on both complete images and 14 source
probes matched across both Lython execution APIs. The new cases exercise
forwarded captures, mutable aliases and cancellation through real host I/O.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Captured rules | Warm | −2.78…+0.25% | −5.04…−1.16% |
| Retained sibling closures | Warm | −18.15…−14.12% | −16.77…−13.29% |
| Ordinary rules | Warm | −0.68…+2.27% | +0.52…+3.05% |
| Annotated rules | Warm | −2.37…+0.12% | −3.38…+1.33% |
| Captured rules | Compile-to-result | −2.84…−0.74% | −2.58…−0.63% |
| Named forwarding control | Warm | −0.89…+1.86% | −0.81…+2.42% |
| Computed default control | Warm `RunAsync` | −3.45…+2.78% | −2.88…+3.10% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Ranges cover all four pairs of duplicate worker medians. Warm elapsed
includes mediated I/O, golden checks and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process
CPU covers the wider request.

Local allocation fell from 4,419,296 to 3,534,560 bytes per captured-rule
run (**20.02%**) and from 314,280 to 210,600 bytes per sibling-closure
run (**32.99%**). Accounted peaks stayed equal across both images, and
every borrowed owner returned to baseline. Retain the removed frame
allocation with the measured sibling and captured-rule gains. The
sibling candidate workers differ by 3.89%. Ordinary-rule CPU rises
in every pairing and remains a control for the next delivery.

All 140 timed batches and both quiet gates passed in 375.74 seconds.
Workers joined, guest fixtures were removed and the archive was verified
under the VM lease. Exact bytes of the two older retired VM replicas
remain recoverable from their committed archives.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
