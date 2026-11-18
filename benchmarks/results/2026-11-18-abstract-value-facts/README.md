# B1015 precomputed abstract-value effects

Measured reference `faeec7e81` (`9edeede4`) and prototype `7a35af179`
(`c9969271`) on the quiet Ubuntu Azure F4as_v7 with .NET 11 RC3 on
2026-11-18 UTC. Both complete 32-file images and the catalog verified.
Five jobs used two processes per source, eight seconds of warmup and five
rotated rounds. Both quiet gates and all 100 measured golden responses
passed. Workers joined and raw evidence was verified before cleanup.

| Workload and phase | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Configuration manifest, compilation | −8.99% to −5.55% | −9.64% to −5.62% |
| Generated configuration validator, compilation | −3.03% to −1.62% | −3.15% to −1.02% |
| Configuration manifest, compile-to-result | −3.40% to −1.14% | −2.04% to +0.68% |
| Expanded forwarding, warm | −1.96% to +0.44% | −3.71% to +2.30% |
| Positional calls, warm | −1.96% to +1.45% | −1.65% to +1.26% |

The compiler summarizes reachable dictionaries, mutable collections and
possible callbacks when publishing an abstract value. The old lookup-time
graph walk, its identity table and its pending stack are gone. Compounds
fold their existing child summaries. Captured function snapshots already
strip mutable collection facts. Dictionary removal and mutable sequence
widening share one pass when both are required. The sequence rewrite keeps
its existing conservative work boundary.

Both compilation-only workloads improve with supporting process CPU.
Manifest compile-to-result improves in elapsed time and its CPU range
spans zero. Both warm controls are inconclusive. A separate Windows first-
compile diagnostic shows 2.39% less allocation for the manifest and 1.08%
less for the generated validator. The accounted execution peaks and
budget cleanup checks agree.

Windows .NET 11 and WSL .NET 10 each passed 86 focused white-box and 736
public cases. Twenty-five differential probes agree in both APIs, including
diagnostic order and text, nested mutable aliases, conditional joins,
dataclass fields and retained closures. Cancellation and the first
admission refusal are tested during construction. The first WSL public
run had no .NET 10 probe in its test ancestor directory. Its log remains
in the archive alongside the passing run using the exact built probe.

Compilation includes public compilation and compilation-owner disposal.
Compile-to-result includes mediated file I/O, output checks, result cleanup
and compilation. Startup, fixture reset and final teardown are outside
guest elapsed. Process CPU covers the wider request.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
