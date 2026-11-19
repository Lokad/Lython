# B1019 direct prepared-definition prototype

Measured `d8d0451e5` / core `ff1b1895` against private prototype
`24d933c9e` on the quiet Ubuntu Azure F4as_v7 with .NET 11 RC3 on
2026-11-19 UTC. Six jobs used two processes per source and five
interleaved rounds. All 120 timed responses and both quiet gates
passed in 274.40 seconds. Workers joined and the archive was verified
before fixture cleanup.

| Workload and phase | Prototype elapsed change | Prototype process CPU change |
| --- | ---: | ---: |
| Unannotated rule closures, warm | −1.65% to −0.42% | −1.96% to +1.39% |
| Unannotated rule closures, compile-to-result | −0.92% to +0.20% | −0.51% to +1.37% |
| Annotated rule closures, warm | −2.84% to −0.93% | −1.41% to +1.35% |
| Annotated rule closures, compile-to-result | −3.91% to −1.17% | −4.57% to −2.27% |
| Expanded forwarding control, warm | −0.24% to +0.74% | −0.44% to +1.36% |
| Positional-call control, warm | +0.52% to +3.06% | −0.78% to +3.05% |

The prototype removes an outer definition dispatch state machine and
separates the publication of already-evaluated annotations from
callback evaluation. Its strongest result is annotated compile-to-result,
where the reference elapsed control spread is 2.55%. Both rule warm
elapsed ranges favor the candidate and their CPU ranges span zero.
The positional elapsed movement is adverse and remains unexplained.
Local warm allocation is effectively unchanged. Accounted peaks agree
and every workload returns the borrowed allowance to baseline.

Windows .NET 11 and WSL .NET 10 each passed 400 public and 70 white-box
focused tests. Ten probes agreed through both APIs, including ordering,
default and annotation identity, decorators that publish before raising,
generic functions and generators. The exact prototype remains private
while a shared completion path is developed and the control measured
against the same reference.

Warm includes mediated I/O, output checks and previous-result cleanup.
Compile-to-result also includes compilation and disposal of its owner.
Startup, fixture reset, transport and final cache teardown are outside
guest elapsed. Process CPU includes the wider request.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
