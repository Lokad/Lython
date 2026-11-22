# B1312 direct UTF-8 float parsing

`float()` now parses existing UTF-8 text and ASCII bytes. Unquoted
numeric CSV fields borrow their bytes from the parser. Both share the
same floating-point conversion, numeric whitespace and original-input
diagnostics. Unicode decimal digits and underscore groups normalize
into bounded stack scratch or admitted heap bytes. CSV keeps its
converted doubles in its existing field descriptors and boxes them
after final row admission. Conversion still occurs before reading
another source line or examining later fields.

The three meter reports open the input through the host, aggregate
4,096 readings at three sites and write the same verified
`summary.csv`. Each reads 12,288 numeric fields. Typed CSV uses
`QUOTE_NONNUMERIC`. The explicit variant uses ordinary CSV and calls
`float()` on the three numeric columns. The Unicode variant uses
Arabic-Indic digits, underscores in some fields and Unicode spaces.
The integer invoice runs its existing calculation and file output.

| Complete workload | Input | Local CLR allocation | Accounted execution peak |
| --- | ---: | ---: | ---: |
| Typed ASCII CSV meter report | 139,299 B | −29.68% | 5,761,673 → 4,142,729 B |
| Explicit float meter report | 139,299 B | −14.24% | 6,875,721 B |
| Typed Unicode CSV meter report | 223,267 B | −41.67% | 5,845,641 → 4,142,729 B |
| Integer invoice control | 127,031 B | −0.002% | 9,432,613 B |

All local allowances return to baseline. Both exact 32-file frozen
images match the four CPython goldens. Windows .NET 11 and Ubuntu
.NET 10 each pass 185 white-box and 1,353 public focused Release
checks. The bit-level float cases, original-input failures, asynchronous
CSV and borrowed wide-field cases pass. The initial refusal fixtures
targeted UTF-16 decode storage. Their replacements target the actual
wide normalization buffer. They verify terminal refusal before
allocation, unchanged output and input ownership, and cleanup. The
Windows sparse checkout initially omitted the representative CSV
scripts. The final run includes them.

## Quiet comparison

Compared `4337b7765` with `7d57b2f26` on 2026-11-22 UTC on the
quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3. Two
independent processes per revision warm for 12 seconds before
contributing seven rotated batches each. Ranges cover all four
pairings of process medians. Warm elapsed includes mediated RAM-backed
I/O and prior-result disposal. Process CPU covers the wider worker
request. Compilation, startup and final teardown are outside this
boundary. Negative changes favor the candidate.

| Workload | Elapsed change | Process CPU change | Ref / candidate elapsed spread | Max IQR |
| --- | ---: | ---: | ---: | ---: |
| Typed ASCII CSV | −16.33% to −12.77% | −16.77% to −11.33% | 3.36 / 0.87% | 0.61% |
| Explicit float | −8.21% to −6.54% | −7.29% to −5.59% | 1.77 / 0.05% | 0.53% |
| Typed Unicode CSV | −14.01% to −8.61% | −14.12% to −7.85% | 0.24 / 6.03% | 0.85% |
| Integer invoice control | −5.16% to +3.79% | −4.81% to +4.50% | 7.54 / 1.76% | 0.81% |

Retain the change. All pairings for the three affected reports favor
the new source on elapsed and CPU. The Unicode candidate processes
differ by 6.03%, so its gain magnitude has more uncertainty. The
unchanged integer invoice spans zero and its reference processes
differ by 7.54%. This run does not establish a change in invoice CPU.

All 112 batches pass output and file checks. Both machine gates
pass, every worker joins and the persistent archive verifies
under the lease. The service takes 287 seconds. The independent
CPython timing reference has its own measurement phase.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Sources and checks](checks-and-procedures.tar.xz).
