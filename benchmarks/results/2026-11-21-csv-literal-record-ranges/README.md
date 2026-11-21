# B1302 direct ranges for native CSV records

For eligible native text lines, the parser records original byte
ranges for fields that need no transformation. It avoids compacting
those fields into a second representation of the line. Quoted fields
and scratch growth transfer from the first affected field into the
existing parser, preserving earlier ranges. Both paths share final
allocation, tail validation and materialization. Quote and escape
semantics, line numbers, terminal refusal and retained rows keep
their existing behavior.

Compared `d51891009` with `bf011b805` on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC. Two independently
warmed processes per revision contribute seven rotated samples each.
The ranges span all pairings of process medians. Warm elapsed includes
mediated RAM-backed I/O, golden checks and prior-result disposal.
Process CPU covers the wider request. Compilation, startup and final
teardown are outside these clocks.

| Complete warm workload | Elapsed change | Wider process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| CSV policy invoice, 4,096 records / 127,031 B | −3.67% to −0.88% | −4.44% to −0.75% | 0.73% / 2.15% |
| Free-function CSV invoice, same input | −3.26% to −2.12% | −3.81% to −1.36% | 0.92% / 0.25% |
| Policy invoice with every field quoted | +0.25% to +1.17% | 0.00% to +2.44% | 0.63% / 0.29% |

The fully quoted input has the same logical records and output.
Retain the implementation for the eliminated copy and the measured
gain on both invoice scripts. Keep the small adverse fully quoted
result as an explicit tradeoff. Accounted peaks match. The candidate
adds eight CLR bytes per complete invocation in the local diagnostic.
Every case returns its allowance to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 149 white-box and 449
public focused Release checks. The new mixed-record regression
checks retained rows across a quoted multiline field, a record
wider than scratch capacity, a literal quote, an empty record and
a final empty field at EOF. Both complete 32-file images passed
all three CPython goldens. Both VM gates and all 84 timed batches
passed. Workers joined and the durable archive verified under the
lease after fixtures were removed.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
