# B1026 expanded executable call runner

Compared private prototype `b2357bdb6 / 844d5957` with
retained `b8532e0ef / 79d43441` on the quiet Ubuntu
Azure F4as_v7 with .NET 11 RC3 on 2026-11-19 UTC.

A sole trailing exact keyword mapping prepares the callee's existing
admitted argument storage. The prototype attaches a compiled target to
the ordinary frame runner and removes its nested interpreter entry.
A dedicated opcode carries the expanded call to that runner. Lowered
targets and dynamic mapping expansion still use their current paths.

The prototype stays private. Both intended workloads span zero in warm
and compile-to-result elapsed and CPU. The f-string control is slower
in elapsed and CPU across all pairings. The cause is unresolved. The
mutable-bound compile-to-result candidate processes differ by 5.72%,
so the run also leaves uncertainty on that workload.

| Workload and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Expanded forwarding, warm | −3.06…+0.89% | −2.63…+2.90% | 2.95 / 1.09% |
| Expanded forwarding, compile-to-result | −0.48…+0.56% | −2.42…+0.47% | 0.27 / 0.77% |
| Mutable bound forwarding, warm | −2.47…+3.48% | −2.58…+3.49% | 1.81 / 4.22% |
| Mutable bound forwarding, compile-to-result | −2.16…+3.53% | −1.61…+3.38% | 0.09 / 5.72% |
| F-string report control, warm | +0.56…+2.74% | +1.75…+3.92% | 1.70 / 0.46% |
| Positional call control, warm | −1.21…+1.83% | −0.08…+2.42% | 1.43 / 1.62% |

Ranges cover the four pairings of process medians. The local allocation
diagnostic reports equal forwarding allocations and accounted peaks.
Every case returns the shared allowance to baseline. Both fully hashed
32-file images passed the comparison verifier with independent CPython
goldens. Windows .NET 11 and WSL .NET 10 passed 87 public and 81 white-box
focused tests each. Two original WSL isolated subprocess tests lacked the
matching probe and passed after it was built in the proper configuration.

The six jobs took 277.12 seconds with two workers per source, eight
seconds of warmup per worker and five rotated rounds. All 120 timed
batches matched, both quiet gates passed, every user joined, and the
observations were archived and verified under the lease.

Warm includes mediated RAM-backed I/O, result validation and prior-result
cleanup. Compile-to-result adds compilation and disposal of its owner.
Startup, fixture reset, framing and final cache teardown are outside
guest elapsed. Process CPU has the wider request boundary.

[Medians](summary.json) · [Raw observations](observations.tgz) ·
[Exact prototype core](prototype-core.dll.gz) · [Source, checks and procedures](checks-and-procedures.tar.xz).
