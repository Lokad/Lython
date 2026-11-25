# B1668 Construct single comparisons without chain storage

The parser constructs a binary expression for the common one-operator
comparison. It constructs operand and operator lists only when a second
comparison follows. Both forms use the same comparison-token handling and
right-operand parsing. The patch removes 26 net lines from the parser.

The reference is `ac9f9211b`. Both 32-file images match three complete
CPython 3.13.2 goldens. Their 14 executable bodies and 3,246 instructions,
source spans and retained admissions match. They also agree on all 271
diagnostic inputs, including every pair of comparison operators, missing
operands, lexical line endings and long chains. All compilation owners
refund their allowance. Windows and Ubuntu pass 726 focused checks on each
Release target.

| Complete script | Compile allocation, reference | Compile allocation, candidate | Retained admission |
| --- | ---: | ---: | ---: |
| Sparse generated validator | 2,169,688 B | 2,154,328 B | 2,294,048 B in both |
| Defaulted rule pairs | 64,288 B | 64,128 B | 74,175 B in both |
| Configuration manifest | 213,664 B | 213,664 B | 212,960 B in both |

## Initial quiet comparison

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Four workers warm for five seconds each and supply seven rotated
batches with a 0.63-second target. Ranges cover the four pairings of worker
medians. Compilation includes prior compiled-owner disposal. Time to result
adds execution, applicable mediated RAM-backed I/O, verification and result
disposal. Wider CPU also includes fixture reset and request framing.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Sparse generated validator | Compile | −1.05 to −0.25% | −1.17 to +0.80% | 0.69 / 0.12% |
| Sparse generated validator | Compile to result | −1.02 to +0.30% | −0.37 to +1.59% | 1.21 / 0.12% |
| Configuration manifest | Compile | −0.38 to +0.50% | −0.67 to +1.48% | 0.08 / 0.80% |
| Defaulted rule pairs | Compile | −2.38 to +0.42% | −1.71 to +0.79% | 0.44 / 2.41% |
| Defaulted rule pairs | Compile to result | +2.72 to +2.93% | +2.15 to +3.18% | 0.05 / 0.15% |

The allocation reduction is established. Sparse-validator elapsed falls
slightly during compilation, with mixed CPU and time-to-result readings.
Defaulted-rule time to result rises in this block. The unchanged tapes and
mixed compilation result leave the source of that increase open. Both quiet
gates and all 140 timed batches verify after worker and fixture cleanup.

## Phase confirmation

The same images repeat the validator compilation and split the rule workload
into compilation, warm execution and time to result. The target batch grows
to 0.87 seconds. Both quiet gates and all 112 batches verify.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Sparse generated validator | Compile | −0.92 to +1.28% | −2.13 to +1.73% | 1.74 / 0.47% |
| Defaulted rule pairs | Compile | −0.52 to +2.33% | −0.42 to +3.14% | 0.84 / 2.00% |
| Defaulted rule pairs | Warm | −4.39 to −0.68% | −3.12 to +0.64% | 2.77 / 1.08% |
| Defaulted rule pairs | Compile to result | −2.41 to −0.25% | −2.60 to −0.33% | 0.94 / 1.26% |

The rule time-to-result increase does not repeat. Overall timing remains
mixed. Keep the smaller parser and measured allocation reduction, and
carry the original adverse rule result into the next definition change.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](compile-allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Confirmation timings](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal status](confirmation/quiet-terminal-status.json) ·
[Confirmation procedures](confirmation/procedures.tar.xz).
