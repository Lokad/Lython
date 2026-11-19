# B1103 compiler-prepared machine integer continuations

R671 marks eligible adjacent arithmetic instructions during stack layout.
The native and general interpreters use this mark to continue a completed
machine-integer prefix. This removes repeated runtime adjacency and operand
checks. It uses an existing instruction operand bit and the existing emission
pass. Cancellation, source spans, exact integer promotion, dynamic protocols
and the resume instruction stay on their original paths.

The private candidate is `50d89ceb1`. The reference image was built from
`bc65698dd`, whose production and test sources match `80cbf4988`. Windows
.NET 11 and Ubuntu .NET 10 each passed 260 focused Release checks, including
an instruction-layout proof and an async reflected protocol. Both 32-file
images passed six CPython goldens. Local warm allocations, accounted peaks
and compilation allocations were effectively unchanged. Borrowed owners
returned to baseline.

The quiet Azure F4as_v7 VM ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Each range covers the four combinations of two independent worker
medians per image. Warm elapsed includes mediated I/O, output and file
verification and prior-result cleanup. Compilation includes compiled-owner
disposal. Compile-to-result adds compilation to the warm boundary. Process
CPU also includes work outside those guest elapsed clocks.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Warm | −0.24…+1.94% | −0.99…+1.50% |
| Annotated rules | Warm | +2.12…+3.66% | +1.03…+4.17% |
| Captured rules | Warm | −5.39…−3.28% | −5.11…−3.45% |
| Calls into two-addition helpers | Warm | −3.14…+5.72% | −2.87…+4.26% |
| Named forwarding control | Warm | −1.36…+1.25% | −2.01…+1.23% |
| Generated validator | Compilation | −0.25…+4.99% | −0.51…+4.46% |
| Calls into two-addition helpers | Compile-to-result | −1.79…+4.65% | −0.20…+4.50% |

Keep the implementation private. The captured-rule improvement and annotated
regression each hold across both worker pairs and have supporting CPU evidence.
The arithmetic helper result is mixed and its warm candidate processes differ
by 6.24%. The validator compilation control also remains uncertain. A future
redesign needs evidence about the operation or frame costs that differ between
captured and annotated rules. Prior results for the runtime-only prefix remain
available. [B1094 and B1095](../2026-11-19-machine-integer-prefix/README.md).

All 140 timed batches completed in 377.19 seconds. The quiet gates passed,
workers joined, fixture storage was removed and the observation archive
was verified under the VM lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
