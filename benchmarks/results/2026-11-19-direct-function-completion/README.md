# B1020 ordinary function-definition completion

Measured `d8d0451e5` / core `ff1b1895` against `30dd46669` /
core `1f747b54` on the quiet Ubuntu Azure F4as_v7 with .NET 11 RC3
on 2026-11-19 UTC. The retained source is unchanged at `934fe4a44`.
All six jobs used two processes per source and five interleaved
rounds. All 120 responses and both quiet gates passed in 273.87
seconds. Workers joined and archive verification preceded fixture
cleanup.

| Workload and phase | Prototype elapsed change | Prototype process CPU change |
| --- | ---: | ---: |
| Unannotated rule closures, warm | −1.75% to +1.14% | −1.78% to −0.17% |
| Unannotated rule closures, compile-to-result | −5.01% to +0.22% | −5.49% to −1.96% |
| Annotated rule closures, warm | −2.48% to −1.85% | −2.83% to −1.99% |
| Annotated rule closures, compile-to-result | −5.93% to −3.34% | −7.21% to −5.07% |
| Expanded forwarding, warm | −4.27% to −2.16% | −4.00% to −0.27% |
| Positional-call control, warm | −1.55% to +0.69% | −2.05% to +2.07% |

The change removes the outer async prepared-definition dispatcher.
Undecorated executable definitions with completed metadata bind and
retire their work directly. Dynamic metadata and decorators can still
use a continuation, with the same cleanup and default publication order.
Evaluated annotations copy directly into the admitted metadata dictionary.

Annotated results support retention on elapsed and process CPU.
Unannotated elapsed spans zero and its compile-to-result reference
spread is 3.62%. Forwarding reference spread is 2.17%. The positional
elapsed adversity from B1019 did not recur, and its CPU is inconclusive.
Local allocation is effectively unchanged. Accounted execution peaks
agree and the borrowed allowance returns to baseline in all cases.

The refinement passed 400 focused public tests on each of Windows
.NET 11 and WSL .NET 10. Ten independent probes matched through both
execution APIs. They cover definition and decorator order, identity,
generators and callback errors. Qualification of the first prototype
has its own evidence. [B1019 and the adverse control](../2026-11-19-direct-function-definitions/README.md).

Warm includes mediated I/O, output checks and previous-result cleanup.
Compile-to-result includes compilation and disposal of its owner.
Startup, fixture reset, framing and final cache teardown are outside
guest elapsed. Process CPU includes the wider request.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
