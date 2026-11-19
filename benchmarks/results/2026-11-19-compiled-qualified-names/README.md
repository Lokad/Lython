# B1051 and B1052 compiled qualified names and closed leaf functions

Retained `56d7e329d` makes the lexical qualified name one immutable UTF-8
value per compiled declaration. It removes per-function name caching and the
runtime class/parent walk. Compile admission covers the retained name and the
temporary path together. Fully resolved leaf functions with no captures also
reuse their original live module without retaining the factory frame. Defaults,
annotations and namespace-dependent bodies keep their defining scope.

The 2,048-definition registration report improves **8.57–11.94% warm** and
**6.66–8.33% compile-to-result**, with lower CPU in both. Windows allocation
falls **18.18%** from 3,245,004 to 2,655,172 bytes and its accounted peak
falls from 3,737,427 to 2,365,267 bytes. The two ordinary rule workloads
allocate 5.01% and 6.36% less. Their accounted peaks each fall 1,048,576 bytes.
Other case allocations move at most 0.14%. Budgets return to baseline.

| Collection and workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1051 Unannotated rules, warm | −3.19…−0.62% | −5.97…+1.43% | 2.13 / 0.51% |
| B1051 Annotated rules, warm | −3.76…+23.37% | −3.49…+21.02% | 3.42 / 23.95% |
| B1051 Annotated rules, compile-to-result | −4.10…−0.56% | −2.92…+1.24% | 1.06 / 2.61% |
| B1051 Registration report, warm | −11.94…−8.57% | −11.57…−7.10% | 1.52 / 2.27% |
| B1051 Registration report, compile-to-result | −8.33…−6.66% | −9.13…−5.47% | 0.26 / 1.56% |
| B1051 Expanded forwarding, compile-to-result | +0.27…+2.69% | −0.19…+3.84% | 1.03 / 1.37% |
| B1051 Positional calls, warm | −0.54…+1.99% | −0.82…+3.27% | 0.65 / 1.88% |
| B1051 Generated validator, compile-to-result | −3.18…+1.49% | −4.61…+2.00% | 3.57 / 1.21% |
| B1052 Annotated rules, warm | −1.46…+2.00% | −0.78…+1.63% | 0.13 / 3.38% |
| B1052 Positional calls, warm | −4.92…+0.54% | −5.76…−0.41% | 5.21 / 0.52% |
| B1052 Expanded forwarding, compile-to-result | −0.61…+0.79% | −1.34…+1.81% | 0.62 / 0.79% |

The initial annotated warm result has a 23.95% spread between identical
candidate workers. That spread falls to 3.38% on repeat and the elapsed and
CPU effects span zero. Forwarding and positional elapsed also span zero on
repeat. Ordinary-rule elapsed remains uncertain.

Both local targets passed 768 focused cases on the combined source. Four
qualified-name probes agree with CPython through both APIs, including lazy
generic parameters and identity across separate instances. The revised budget
test admits the name in compilation and refuses later concatenation before guest
code can resume. The isolated frame change passed 671 cases per target and
allows the same 400-definition script at 256 KiB where the preceding runtime
refuses through both APIs. Its separate execution peak falls from 411,235 to
206,435 bytes. Checks include live global updates, independent defaults and a
returned body that still needs its pattern scope.

The same two 32-file images, `c8e749318` and `48309e4e3`, ran on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
The candidate's source and tests match `56d7e329d`. Both images passed the
six-case CPython catalog. Ranges cover all four pairings of process medians.
Negative changes favor the candidate. B1052 changed worker order.

The eight initial jobs took 364.20 seconds and the three confirmation jobs
took 139.97 seconds. All 220 measured batches passed output checks. Both quiet
gates passed. Workers joined, guest fixtures were removed and raw archives
verified under the lease. Warm includes mediated I/O, golden checks and prior
result cleanup. Compile-to-result adds compilation and compiled-owner cleanup.
Startup, fixture reset, protocol and final cache teardown fall outside guest
elapsed. Process CPU covers the wider request.

[B1051 summary](summary.json) · [B1051 raw](observations.tgz) ·
[B1052 summary](confirmation/summary.json) · [B1052 raw](confirmation/observations.tgz) ·
[Exact measured core](measured-core.dll.gz) · [Sources, checks and procedures](checks-and-procedures.tar.xz) ·
[Delivery CI](https://github.com/Lokad/Lython/actions/runs/44467869801).
