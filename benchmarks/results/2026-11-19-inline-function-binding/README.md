# B1120 and B1121 function-owned binding state

Private `03e603b5a` stores mutable defaults and binding state on the
function object. It removes the separate binding allocation from ordinary
and generator definitions while retaining the shared parameter layout,
existing binder and conservative admission. The reference is `0d16f8307`.

Windows .NET 11 and Ubuntu .NET 10 each passed 372 focused Release checks.
Both complete 32-file images matched six CPython output and file goldens.
Local allocation fell 4.59% for ordinary rules, 3.20% for annotated rules
and 6.13% for captured rules. The three controls moved by at most 0.07%.
Accounted peaks match, and all checks returned the allowance to baseline.

Both runs used the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on
November 19, 2026 UTC. Warm elapsed includes mediated RAM-backed I/O, golden
checks and previous-result cleanup. Process CPU includes the broader worker
boundary. The confirmation uses unchanged binaries and reversed initial
worker order. Changes span four combinations of duplicate process medians.

| Workload | Initial elapsed | Initial CPU | Confirmation elapsed | Confirmation CPU |
| --- | ---: | ---: | ---: | ---: |
| Ordinary rules | −2.60…+0.43% | −3.82…+2.64% | −1.36…−0.16% | −0.45…+0.45% |
| Annotated rules | +2.30…+5.76% | +2.50…+4.01% | −1.11…+2.65% | −1.00…+3.38% |
| Captured rules | −2.14…−0.62% | −3.61…−0.52% | −2.99…−0.19% | −4.37…+0.99% |
| Reused defaults | −1.65…+1.34% | −1.26…+2.54% | +1.77…+7.87% | +1.40…+7.34% |
| Expanded forwarding | −1.51…+2.53% | −1.14…+4.37% | — | — |

Captured elapsed improves in both runs. Annotated timing was adverse in the
first run and spans zero in the confirmation, where the reference workers
differ by 3.71%. The reused-defaults confirmation is slower across all
elapsed and CPU pairings, with 4.22% elapsed spread between candidate
workers. Keep the prototype private. Further work should isolate call-time
layout from definition allocation before reconsidering the binding design.

All 180 timed batches and both pairs of quiet gates passed. Both services
joined their workers, removed their guest fixtures and verified the
observation archive under the lease. The first staging preflight declined
for disk headroom. The successful preflight verified complete alias sets
for the archived images before retiring selected VM copies.

[Initial summary](summary.json) · [Initial observations](observations.tgz) ·
[Confirmation summary](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).
