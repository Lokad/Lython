# B1041 and B1042 compiled declaration text

The retained runtime `aa29c019e` shares one UTF-8 function name and the
compiled leading string literal across executions of each declaration.
Ordinary and generator functions use one code reference for the declaration,
scope and body. The lowered class path uses the same compiled function
metadata and shares the leading class literal. Overrides, mutable defaults
and annotations remain per definition. Public text projections have their own
funding. The change removes per-definition encoding and admission for the
immutable text and drops the duplicate metadata fields.

Unannotated and annotated rules improved **2.96–3.13%** and **3.42–6.69%**
warm with lower CPU. Both compile-to-result improvements repeated with
lower CPU. The same annotated script with a Unicode docstring improved
**8.48–13.30%** warm with lower CPU. The positional-call control slowed
0.56–1.63% on repeat with mixed CPU. Retain the rule and allocation gains.
Track the positional regression at the next call/operand comparison.

| Collection and workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| B1041 Unannotated rules, warm | −3.13…−2.96% | −2.27…−0.87% | 0.11 / 0.06% |
| B1041 Annotated rules, warm | −6.69…−3.42% | −6.32…−4.35% | 1.79 / 1.69% |
| B1041 Unannotated rules, compile-to-result | −6.16…−0.52% | −5.14…+1.19% | 3.34 / 2.58% |
| B1041 Annotated rules, compile-to-result | −3.16…−1.01% | −3.08…+2.59% | 1.92 / 0.29% |
| B1041 Documented rules, warm | −13.30…−8.48% | −12.17…−6.67% | 3.64 / 1.86% |
| B1041 One definition, compile-to-result | −2.34…−0.52% | −2.13…+0.04% | 1.79 / 0.07% |
| B1041 Annotated compilation only | −2.22…+0.16% | −3.83…+0.69% | 2.08 / 0.34% |
| B1041 Expanded forwarding control, warm | −1.69…+0.22% | −1.26…+0.43% | 0.43 / 1.50% |
| B1041 Positional control, warm | +1.20…+2.56% | +1.24…+3.75% | 0.39 / 0.95% |
| B1042 Positional control, warm | +0.56…+1.63% | −0.41…+1.67% | 0.66 / 0.40% |
| B1042 Annotated rules, compile-to-result | −3.88…−0.26% | −4.74…−1.70% | 1.44 / 2.30% |
| B1042 Unannotated rules, compile-to-result | −7.38…−3.48% | −7.33…−2.77% | 1.68 / 2.49% |

Both comparisons use `b491250cb` and prototype `0a69dd9bc` on the quiet
Ubuntu 24.04.4 Azure F4as_v7 with .NET 11 RC3 on 2026-11-19 UTC. The
prototype runtime and tests match `aa29c019e`. Ranges cover the four pairs
of process medians. Negative values favor the candidate. The repeat used
identical binaries and case rows with a changed worker order.

Local Windows allocation fell **5.15%**, **5.47%** and **14.37%** for
unannotated, annotated and documented rules. Accounted peaks fell
272,522, 278,668 and 680,076 bytes respectively. The one-definition case
allocates 1.26% less in that diagnostic. All budget balances return to baseline.

Windows .NET 11 and WSL .NET 10 each passed 333 public and 133 white-box
focused checks. The initial import fixture omitted the module authorization.
The test-only `0a69dd9bc` correction explicitly grants it. Both original
failure logs and passing checks are retained. CPython 3.13 agrees on the
shared Unicode metadata, independent overrides and generator behavior in
the direct and imported probes. Existing tests cover actual suspension and
cancellation, retained projection and separate owner disposal.

Both full 32-file images verified before staging. The nine initial jobs and
three confirmation jobs took 409.13 and 140.54 seconds. All 240 measured
batches passed their output and file checks. Both quiet gates passed, all
workers joined, guest fixtures were removed and the archives verified under
the lease. Warm includes mediated I/O, goldens and prior-result cleanup.
Compile-to-result adds compilation and disposal of its owner. Startup,
fixture reset, protocol and final cache teardown are outside guest elapsed.
Process CPU includes the wider request. Full platform and package checks
belong to the next delivery checkpoint.

[B1041 summary](summary.json) · [B1041 raw](observations.tgz) ·
[B1042 summary](confirmation/summary.json) · [B1042 raw](confirmation/observations.tgz) ·
[Measured core](measured-core.dll.gz) · [Sources, checks and procedures](checks-and-procedures.tar.xz).
