# Function binding ownership comparison

R597 compared private prototype `4499c67ba` with retained runtime
`6ce82ba48` on 2026-11-19 UTC. The candidate puts a Python function and
its mutable binding state in one object, removes a duplicate declaration-name
reference and preserves the shared compiled parameter layout. Generated
methods keep a standalone binding owner. Memory estimates are unchanged.

The candidate stays private. Its allocation benefit and annotated rule
improvement came with higher forwarding compile-to-result elapsed time and
CPU on the confirmation. The original forwarding warm slowdown did not
repeat consistently. The cause of the adverse compile-to-result result is
open.

| Source pair / workload | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1023 unannotated rules warm | −0.85…−0.58% | −3.11…+1.40% | 0.07 / 0.20% |
| B1023 unannotated rules compile-to-result | −3.93…+2.58% | −4.35…+2.48% | 4.36 / 2.32% |
| B1023 annotated rules warm | −1.68…+1.88% | −1.32…+3.81% | 2.50 / 1.09% |
| B1023 annotated rules compile-to-result | −3.04…−0.31% | −3.98…−2.41% | 0.78 / 2.02% |
| B1023 forwarding warm | +0.27…+4.57% | +0.21…+3.64% | 0.85 / 3.41% |
| B1023 positional calls warm | −2.33…+0.19% | −1.64…0.00% | 1.35 / 1.20% |
| B1024 forwarding warm | −3.52…+1.20% | −1.99…+2.45% | 2.88 / 1.96% |
| B1024 forwarding compile-to-result | +2.69…+3.94% | +1.34…+3.93% | 0.77 / 0.45% |
| B1024 annotated rules compile-to-result | −3.76…−0.48% | −5.77…+1.04% | 2.37 / 1.01% |

Ranges cover the four pairings of process medians. Negative changes favor
the candidate. All 180 timed batches matched their stdout and file goldens.
The source pairs ran on the quiet Azure Ubuntu F4as_v7 with .NET 11 RC3,
eight seconds of full-request warmup per process and five rotated measured
batches. B1023 took 274.50 s for six jobs. B1024 took 140.70 s for three.
Both runs passed their quiet gates, joined the workers, removed guest
fixtures and archived verified evidence under the lease.

Warm elapsed includes mediated RAM-backed I/O, golden checks and
prior-result cleanup. Compile-to-result includes compilation and disposal
of the compiled owner as well. Startup, fixture reset, framing and final
cache cleanup are outside guest elapsed. Process CPU has a wider boundary.

The local Windows allocation diagnostic allocates **2.79% less** for
unannotated rules and **1.76% less** for annotated rules. Their accounted
peaks stayed at 5,189,329 and 5,229,781 bytes, respectively. Every case
returned the shared allowance to baseline. Windows .NET 11 and WSL .NET 10
each passed 70 white-box and 270 public checks. The prototype matched the
reference on 16 probes through both APIs.

[Initial medians](initial/summary.json) · [Initial raw observations](initial/observations.tgz) ·
[Confirmation medians](confirmation/summary.json) · [Confirmation raw observations](confirmation/observations.tgz) ·
[Exact prototype core](prototype-core.dll.gz) · [Source patch, checks and procedures](checks-and-procedures.tar.xz).
