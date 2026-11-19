# Reused key storage for function metadata

B1028 and B1029 compare retained `b8532e0ef / 79d43441` with
prototype `c2b6d9265 / 7e525683` on the quiet Ubuntu Azure F4as_v7
with .NET 11 RC3 on 2026-11-19 UTC. Runtime and test source match
retained `2409f05964`.

Each executable definition site can share the immutable key table for its
keyword defaults and annotations. The current definition still owns a
separate mutable dictionary and values. The first completed record can share
its admitted layout with the execution lookup owner. Later definitions fill
only their own values in prepared order. Normal dictionary mutation detaches
private key backing when an insertion requires it. Caches retain no previous
definition values.

| Workload and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1028 Unannotated rules, warm | −2.40…+0.16% | −4.83…0.00% | 1.17 / 1.43% |
| B1028 Unannotated rules, compile-to-result | +1.09…+2.57% | +1.08…+3.44% | 0.36 / 1.10% |
| B1028 Annotated rules, warm | −2.79…−1.43% | −3.64…−1.70% | 0.69 / 0.70% |
| B1028 Annotated rules, compile-to-result | −4.95…−0.66% | −4.18…−0.90% | 1.29 / 3.18% |
| B1028 Reused defaults, warm | −0.56…+6.75% | −0.70…+6.71% | 2.43 / 4.81% |
| B1028 Single annotated definition, compile-to-result | −1.22…+0.11% | −2.49…+0.59% | 1.13 / 0.21% |
| B1028 Annotated rules, compilation | +0.55…+1.90% | +0.71…+1.32% | 0.76 / 0.59% |
| B1028 Expanded forwarding, warm | −3.98…+4.53% | −1.60…+3.00% | 5.01 / 3.67% |
| B1028 Positional calls, warm | −5.70…+0.49% | −5.71…+0.83% | 5.18 / 1.32% |
| B1029 Unannotated rules, compile-to-result | −1.45…0.00% | −3.85…0.00% | 0.56 / 0.91% |
| B1029 Reused defaults, warm | −3.29…+6.81% | −4.60…+9.88% | 4.29 / 5.90% |
| B1029 Annotated rules, compile-to-result | −5.36…+0.25% | −5.29…−1.34% | 4.44 / 1.42% |
| B1029 Annotated rules, warm | −3.13…+3.12% | −2.16…+3.35% | 2.75 / 3.60% |

Ranges cover all four pairings of process medians. B1028's adverse unannotated
compile-to-result result did not recur on B1029. Both annotated compile-to-result
runs show lower CPU. The repeat's annotated elapsed range reaches zero with
4.44% reference spread. The reused-default control is unresolved. Its candidate
elapsed spread grows from 4.81% to 5.90% across the two comparisons. The initial
annotated compilation-only increase is measured separately from execution.

| Local allocation diagnostic | Allocated bytes | Accounted peak |
| --- | ---: | ---: |
| Unannotated rules | 4,697,936 → 4,272,424 (−9.06%) | 5,189,329 → 4,076,049 (−21.45%) |
| Annotated rules | 3,716,840 → 3,127,568 (−15.85%) | 5,229,781 → 3,690,725 (−29.43%) |
| Single annotated definition | 15,704 → 15,968 (+1.68%) | 5,813 → 6,101 (+4.95%) |
| Reused defaults | 143,440 → 143,568 (+128 B) | 2,146 → 2,306 (+160 B) |

All cases restore the shared allowance. Retention is based on the reduction
in repeated construction and its allocation and admission benefits, with
supporting annotated compile-to-result CPU. Single-definition elapsed
crosses zero. The first-use allocation increase and reused-default timing
remain visible.

Both full 32-file images and the six-case catalog verified. The first verifier
rejected the single-definition catalog because its fixture still declared the
original size. The corrected fixture was independently verified with CPython
before staging. Five additional probes match CPython through both Lython APIs,
including dictionary deletion/reinsertion, decorator escape, annotation
evaluation order and suspended generator defaults. Windows .NET 11 and WSL
.NET 10 each passed 251 public and 31 white-box focused tests.

Nine jobs took 410.13 seconds in B1028 and four took 185.25 seconds in B1029.
Both passed quiet gates and all 260 timed batch checks. Workers joined and the
archives verified before relinquishing the VM lease. Warm includes mediated
RAM-backed I/O where the script uses it, golden verification and prior-result
cleanup. Compile-to-result adds compilation and disposal of the compiled
owner. Startup, fixture refresh, transport and final cache teardown are
outside guest elapsed. Process CPU has the wider request boundary.

[B1028 medians](summary.json) · [B1028 raw observations](observations.tgz) ·
[B1029 medians](confirmation/summary.json) · [B1029 raw observations](confirmation/observations.tgz) ·
[Measured core](measured-core.dll.gz) · [Source, tests and procedures](checks-and-procedures.tar.xz).
