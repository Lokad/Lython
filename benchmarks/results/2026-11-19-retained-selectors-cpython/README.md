# B1047 retained runtime against CPython

Measured Lython `9ac02d15d` with core `e4bceee2` against stock CPython
3.13.16 on 2026-11-19 UTC. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3. The comparison reused the complete candidate image from
B1045 and the same five regex-free cases as B1040. The worker images and
expected outputs verified before staging.
[Selector source pair and measured core](../2026-11-19-native-format-selectors/README.md) ·
[Earlier B1040 snapshot](../2026-11-19-retained-runtime-cpython/README.md).

| Workload | Input | Compilation only | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Shipment report with format selectors | 3,072 records / 189,441 B | 0.94–0.96× | 1.18–1.22× | 1.16–1.19× |
| Expanded forwarding report | 1,024 records / 26,112 B | 0.83–0.84× | 1.75–1.78× | 1.64–1.67× |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 0.79–0.80× | 4.06–4.19× | 3.92–4.00× |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 0.80–0.81× | 4.82–4.88× | 4.70–4.72× |
| CSV invoice | 4,096 data records / 127,031 B | 0.97–0.98× | 1.10–1.12× | 1.08–1.10× |

Ratios divide Lython elapsed by CPython elapsed across all four pairs of
process medians. Above one means Lython took longer. Lython warm control
spread ranges from 0.03% to 2.13%. The largest warm IQR is 2.94% for
annotated rules. Closures remain the largest gap in this catalog. The
same-input B1040 and B1047 observations are separate comparisons with
CPython. Use B1045 to attribute a change to prepared format keys.

The five jobs completed in 310.69 seconds. Each used two processes per
engine with all three phases and five rotated rounds. All 300 measured
responses passed protocol and golden checks. Both quiet gates passed,
workers joined, guest fixtures were removed and the archive verified under
the lease. The first staging check required 254 MiB of free headroom and
stopped before launch. The final staging retained 250 MiB of headroom,
with no retirement of previous evidence or change to measured work.

Warm elapsed includes mediated RAM-backed I/O, golden checks and prior-result
cleanup. Compilation alone includes disposal of compiled storage.
Compile-to-result includes compilation, execution and cleanup. Cold startup,
fixture reset, request framing and final cache teardown fall outside elapsed.
Process CPU includes the wider request. The measured runtime passed focused
Windows .NET 11 and WSL .NET 10 checks. Full delivery remains in progress.

[Per-process elapsed and CPU](summary.json) · [Raw observations](observations.tgz) ·
[Verified procedures and image hashes](procedures.tar.xz).
