# B1034 and B1035 grouped function definition admission

Prototype `2789d33b1` groups known default backing, annotation backing
and function shell in the existing prepared definition loan. Layout
caches and unknown scalar growth retain separate admission. A tight
budget can refuse the known capacity before default expressions run.
The prototype stays private. The measured reduction in admission calls
and allocation comes with earlier refusal and extra coordination. The
paired scripts have mixed timing after confirmation.

| Collection and workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1034 Unannotated rules, warm | −5.48…+1.12% | −5.62…+1.44% | 2.28 / 4.60% |
| B1034 Unannotated rules, compile-to-result | −2.12…+2.69% | −2.91…+4.47% | 0.47 / 4.42% |
| B1034 Annotated rules, warm | −4.23…−0.62% | −4.51…+0.42% | 2.14 / 1.59% |
| B1034 Annotated rules, compile-to-result | −0.40…+2.16% | −0.43…+1.75% | 2.24 / 0.32% |
| B1034 Reused defaults, warm | −11.22…−0.38% | −11.73…+0.21% | 11.95 / 0.24% |
| B1034 One annotated definition, compile-to-result | −2.06…+0.88% | −3.27…−0.63% | 2.61 / 0.37% |
| B1034 Annotated rules, compilation only | −0.28…+1.56% | +0.68…+3.46% | 0.18 / 1.66% |
| B1034 Expanded forwarding control, warm | +0.58…+2.30% | +1.78…+3.34% | 1.25 / 0.46% |
| B1034 Positional control, warm | −4.27…+0.01% | −4.49…−0.41% | 3.63 / 0.81% |
| B1035 Expanded forwarding control, warm | −3.35…+1.74% | −3.48…+0.89% | 3.14 / 2.06% |
| B1035 Annotated rules, warm | −2.44…+3.64% | −2.11…+3.88% | 0.72 / 5.47% |
| B1035 Unannotated rules, compile-to-result | −1.51…+4.13% | +0.00…+3.05% | 1.67 / 4.00% |

Both runs compared `e93356adc` and `2789d33b1` on the quiet Ubuntu
24.04.4 Azure F4as_v7 with .NET 11 RC3 on 2026-11-19 UTC. Frozen core
hashes start with `b21ca2e4` and `34f75af0`.
Ranges cover all four pairings of process medians. Negative values
indicate less time. Initial forwarding adversity did not recur in
B1035. The repeat leaves warm annotated elapsed and CPU mixed, with
5.47% candidate spread. Unannotated compile-to-result CPU ran between
unchanged and 3.05% higher in the repeat, and elapsed spanned zero.

Local diagnostic allocations fell 1.58% for unannotated rules and 2.19%
for annotated rules at the same accounted peaks. First-use allocation
rose 0.55%. An instrumented host counted positive resizes falling from
22,541 to 16,397 for unannotated rules and from 14,366 to 8,222 for
annotated rules. This validates the displaced admission work. Those
counts use a separate observer and provide no elapsed estimate.

Windows .NET 11 and WSL .NET 10 each passed 237 public and 59 white-box
checks, including refusal before default effects and funding across
suspension. Both complete 32-file images passed the six-case catalog
verification. All 240 timed batches and the quiet gates passed over
409.34 and 139.16 seconds.
Workers joined and raw archives verified under the lease.

Warm includes mediated RAM-backed I/O, goldens and previous-result
cleanup. Compile-to-result adds compilation and its owner disposal.
Startup, fixture reset, request framing and final cache teardown are
outside guest elapsed. Process CPU covers the wider request.

[B1034 summary](summary.json) · [B1034 raw](observations.tgz) ·
[B1035 summary](confirmation/summary.json) · [B1035 raw](confirmation/observations.tgz) ·
[Prototype image](measured-core.dll.gz) · [Sources, observer and checks](checks-and-procedures.tar.xz).
