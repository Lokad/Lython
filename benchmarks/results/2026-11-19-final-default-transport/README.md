# B1043 direct default operand transport

Prototype `00a6862e7` writes evaluated defaults directly into their pending
definition owner. It removes the function and lambda replay loops, shares
their preparation, and forwards results through the existing adjacent
consumer when possible. Remaining producers use the append at their normal
completion boundary. Lambda preflights known capacity before its default
expressions, as `def` already did.

The prototype stays private. Unannotated rules run **1.98–4.48% slower
warm** with more CPU, and annotated compilation takes longer. Annotated
compile-to-result improves elapsed and CPU. Local allocation is effectively
unchanged, with 48 fewer accounted bytes in two cases. The targeted result
and smaller operand window leave higher elapsed and CPU on the principal
unannotated workload. Revisit if a different mechanism removes the replay
without the extra prepared instruction and result-consumer work.

| Workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Unannotated rules, warm | +1.98…+4.48% | +2.30…+3.88% | 1.79 / 0.65% |
| Annotated rules, warm | +0.00…+1.96% | −0.42…+1.69% | 1.17 / 0.77% |
| Unannotated rules, compile-to-result | +0.16…+2.72% | +1.10…+4.55% | 0.74 / 1.80% |
| Annotated rules, compile-to-result | −3.21…−0.02% | −1.73…−0.83% | 2.50 / 0.78% |
| Reused defaults, warm | +0.24…+4.80% | −1.65…+3.76% | 4.15 / 0.39% |
| One annotated definition, compile-to-result | −0.48…+1.65% | −0.87…+2.49% | 0.75 / 1.38% |
| Annotated compilation only | +0.60…+3.00% | +0.69…+1.18% | 1.30 / 1.08% |
| Expanded forwarding control, warm | −4.00…+0.68% | −2.87…0.00% | 3.38 / 1.44% |
| Positional control, warm | −1.00…+0.95% | −0.82…+3.27% | 0.03 / 1.93% |

B1043 compares `aa29c019e` and prototype `00a6862e7` on the quiet
Ubuntu 24.04.4 Azure F4as_v7 with .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover the four pairs of process medians. Negative values favor the
candidate. Reused-default elapsed depends on a 4.15% moving reference.
The positional row compares the prototype with R611 and its accumulated
positional timing. [R611 positional observations](../2026-11-19-prepared-definition-text/README.md).

The original and rebased prototypes passed focused Release suites on Windows
.NET 11 and WSL .NET 10. There were 436 cases per target at the original
base and 185 after rebasing, with separate preflight checks for both function
forms. These cover alias identity, caught definition failure, decorator
escape, genuinely suspended default evaluation, cancellation, terminal
refusal and allowance reuse. A Windows build-output collision was corrected
and the preflight checks passed on rerun. Both full 32-file images and the
six-case catalog verified against CPython. All local allowance balances were
restored after cleanup.

All nine jobs and 180 timed batches passed golden verification in 409.89
seconds. Quiet gates passed, workers joined, guest fixtures were removed
and the archive was verified under the lease. Warm includes mediated I/O,
goldens and earlier-result cleanup. Compile-to-result adds compilation and
its owner disposal. Startup, fixture reset, request framing and final cache
teardown are outside guest elapsed. Process CPU includes the wider request.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Measured core](measured-core.dll.gz) · [Prototype, checks and procedures](checks-and-procedures.tar.xz).
