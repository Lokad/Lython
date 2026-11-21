# B1259 shared annotation boundaries

Compared `264dbba10` with `dd2241ffd` on November 21, 2026 UTC.
Both Release workers used the Azure F4as_v7, Ubuntu 24.04.4 and
.NET 11 RC3. Each lane rotated two independent processes per source
and collected seven timed batches per process. Both complete 32-file
images passed the three CPython goldens before staging. All 112 timed
batches completed in a 367-second campaign. Both quiet gates passed,
workers joined and the archive verified under the lease before RAM
fixtures were removed.

Negative changes favor the candidate. Ranges cover the four pairings
of worker medians. Compilation includes code ownership disposal.
Compile-to-result also includes guest execution, mediated I/O,
golden observation and prior-result cleanup. Process startup and final
teardown are outside the guest clock. Sampled process CPU covers each
complete batch request.

| Script | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested annotated factories, postponed | Compile | −1.99% to −0.32% | −1.50% to +0.22% |
| Inherited dataclass configuration | Compile | −0.96% to +1.61% | −2.63% to +1.52% |
| Inherited dataclass configuration | Compile-to-result | −0.87% to −0.15% | −1.29% to +0.35% |
| Generated unannotated validator | Compile | −4.17% to −1.46% | −3.51% to −2.11% |

The validator improves on both clocks. Inherited dataclass compile
results are inconclusive, including a 2.13% spread between unchanged
reference workers. The elapsed changes in factories and dataclass
compile-to-result are small and CPU spans zero.

Local Windows Release compile allocation falls from 69,144 to 68,848
bytes for the factories, 602,944 to 598,984 for the dataclasses and
2,328,705 to 2,300,055 for the validator. The three warm allocation
measurements and accounted peaks are identical, and shared-budget
owners return to baseline. 820 public and 54 white-box focused Release
checks passed on both Windows .NET 11 and Ubuntu .NET 10. The added
regression covers annotation error order across sibling try suites,
a nested function and sibling classes.

Retained as `d9336a2cc`. Syntax validation now collects the last
annotation-dependent statement for relevant lexical scopes and discovers
regex imports. This removes the annotation prepass and duplicate lexical
discovery. Control-flow suites still export full bindings and regex
constant analysis retains its conservative walk. B1259 establishes a
validator compilation improvement and supplies no new CPython ratio.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Archive receipt](archive-receipt.json) ·
[Checks and source identity](checks-and-procedures.tar.xz).
