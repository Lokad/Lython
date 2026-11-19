# B1113 fixed tuple returns from captured factories

R680 extends fixed unpacking to an adjacent tuple return from an eligible
executable frame. The suspended caller holds the admitted elements in its
operand slots until child cleanup succeeds. This removes a transient
tuple object and backing array. Protected framed returns, escaping tuples,
starred targets and mismatched counts follow ordinary tuple construction.

The pair compares `8ffd40dc0` with `d0fc1bb93`. Those images match the
production and test sources at `e1636b271` and retained `2841f6ac2`
respectively. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3
on November 19, 2026 UTC. Ranges cover all four combinations of duplicate
worker medians. Negative means less time.

| Workload | Boundary | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −5.60…−3.09% | −4.00…−2.48% | 0.93 / 1.71% |
| Captured rules | Compile-to-result | −3.47…+0.18% | −3.39…−1.15% | 1.53 / 2.22% |
| Ordinary rules | Warm | −0.74…+10.61% | −1.03…+12.45% | 0.17 / 11.26% |
| Annotated rules | Warm | −1.00…+3.07% | −2.41…+2.61% | 0.95 / 3.13% |
| Escaping sibling closures | Warm | −4.29…+0.84% | −4.87…−0.71% | 5.20 / 0.16% |
| CSV invoice | Warm | −1.89…+0.41% | −1.96…+2.00% | 2.22 / 0.13% |

Local captured-rule allocation falls from 3,534,560 to 3,288,800 bytes
per script (**6.95%**). The accounted peak matches. The escaping control
and both other rule shapes keep their allocation. All workloads return
their borrowed allowances to baseline. Captured warm elapsed and CPU
support the removed allocation stage. The captured one-shot elapsed
range spans zero. One ordinary candidate worker slowed after the first
sample. The cause remains unresolved. [Control repeat](confirmation/README.md).

Windows .NET 11 and Ubuntu .NET 10 each passed 271 focused Release checks.
The tests cover fixed unpack through suspension, terminal refusal before
target effects, aliases, failure spans and protected returns. Both complete
32-file images matched five CPython goldens. Eight supplemental programs
matched through both execution APIs. The initial sparse build omitted
shared fixture sources. The original probe using a subclass of builtin
`Exception` failed on both Lython images, and the corrected probe uses a
supported builtin exception. Both original conditions are archived.

Warm includes mediated RAM-backed I/O, goldens and prior-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Wider
process CPU includes worker work outside guest elapsed. All 120 timed
batches verified in 321.84 seconds. Both quiet gates passed. Workers
joined, fixtures were removed and the archive was verified under the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
