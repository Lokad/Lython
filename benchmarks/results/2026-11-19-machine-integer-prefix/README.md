# B1094 preserve completed integer work

R665 computes the first primitive operation once, follows the machine-integer
chain, and publishes its completed prefix when a later operation needs general
dispatch. It removes repeated chain entry detection and discards less work at
overflow, mixed operands or Python numeric protocols. Both executable runners use
the same change. The exact candidate `954f2acfd` remains private, compared with
`06b40a712`. The [B1093 profile](../2026-11-19-retained-rule-stage-profile/README.md)
motivated the change. It did not measure the frequency of partial chains.

Each local target passed 239 focused Release checks. They cover promotion,
floating-point values, reflection, unbound diagnostics, cancellation, refusal
and an actual asynchronous reflected protocol with awaited cleanup. Each
32-file image passed four CPython goldens. Rule allocations and accounted
peaks are unchanged in the local diagnostic. Forwarding differs by 12 allocated
bytes and has the same accounted peak. All budgets returned to baseline.

The quiet Azure F4as_v7 VM ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Each range covers the four combinations of two independent process
medians per image. Warm uses an already-compiled script and includes mediated
I/O, validation and prior-result cleanup. Compile-to-result adds compilation
and compiled-owner disposal. Process CPU has a wider boundary.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rule closures | Warm | −3.53…+20.82% | −3.94…+22.50% |
| Annotated rule closures | Warm | −3.52…−1.69% | −2.76…−1.02% |
| Captured rule closures | Warm | −2.51…−1.86% | −3.15…+0.16% |
| Expanded forwarding control | Warm | −0.64…+0.42% | −1.94…+0.81% |
| Ordinary rule closures | Compile-to-result | −2.33…+2.05% | −1.04…+2.30% |
| Annotated rule closures | Compile-to-result | −0.90…+0.40% | −1.27…+2.71% |
| Captured rule closures | Compile-to-result | −0.80…+0.82% | +0.73…+2.63% |
| Expanded forwarding control | Compile-to-result | −1.15…−0.89% | −2.81…−0.43% |

The ordinary warm candidate processes diverge by 22.99% using the same
binary. The slow process repeats 3.00–3.18 ms during timed batches, and the
other candidate stays near 2.52 ms. CPU rises correspondingly. The divergence
is visible late in warmup. Preserve both workers and the captured
compile-to-result CPU result. The [B1095 confirmation](confirmation/README.md)
reordered the workers and lengthened warmup. The large process divergence did
not recur and the annotated warm improvements did not repeat. The prototype
remains private.

The first setup left a six-job guard in an eight-job catalog, so it exited
before starting any worker. The empty owned fixture mount was released under
the lease. The corrected setup reused the same catalog and both images. It
completed 160 verified sample batches in 365.45 seconds. Both quiet gates
passed, workers joined, fixtures were removed and the archive SHA verified
under the lease. The original setup failure is in the procedures archive.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
