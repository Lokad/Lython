# B1277 admitted bulk UTF-8 CSV field output

The CSV writer prepares its dialect's search set once. It validates and
sizes each field before taking a window in the existing row builder.
Fields without escape prefixes copy their original UTF-8 once. Escaped
fields copy byte runs between prefixes. The renderer shares this path
across ordinary writes, dictionary writes and buffered history. The
host receives complete rows and pending writes retain their buffers.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
21, 2026 UTC. Reference `54b909bb1` uses the frozen image built from
source-equivalent `5232caad5`. The candidate is `9bf720efa`. Each
workload uses two workers per revision, at least eight seconds of warm
requests per worker and seven rotated batches per worker. The ranges
cross every pairing of independent process medians. Warm elapsed
includes mediated RAM-backed I/O, output verification and prior-result
cleanup. Process CPU covers the wider request. Compilation, process
startup and final teardown sit outside guest clocks.

| Complete script | Input | Warm elapsed | Process CPU | Local CLR allocation |
| --- | ---: | ---: | ---: | ---: |
| Restock export, ordinary descriptions | 3,072 rows / 182,437 B | −36.29 to −33.77% | −35.91 to −33.10% | −0.87% |
| Restock export, escaped Unicode dialect | 3,072 rows / 170,917 B | −23.32 to −19.49% | −22.02 to −19.73% | −5.07% |
| Restock export, short descriptions | 3,072 rows / 44,197 B | −10.08 to −8.72% | −11.10 to −7.24% | +0.004% |
| Invoice aggregation with a small CSV summary | 1,024 rows | −3.93 to −0.90% | −2.94 to −0.99% | +0.016% |

The three restock scripts filter and normalize a CSV inventory file and
write 2,958 records. Ordinary export elapsed process medians move from
5.884–5.935 ms to 3.781–3.897 ms. The escaped case includes multibyte
delimiter, quote, escape and terminator characters. The invoice control
performs little output relative to its input and arithmetic work. The
large export gains justify retaining the shared renderer.

Local accounted peaks rise by 62 bytes on ordinary export and 604 bytes
with the Unicode dialect. The short export and invoice peaks match.
Every execution releases the allowance back to baseline. Custom dialect
search metadata retains a conservative extra charge with its writer.
One field reservation admits the final field extent, and the existing
builder funds any replacement overlap. A cancelled admission is checked
before allocating the new backing.

Both Windows .NET 11 and Ubuntu .NET 10 passed 680 white-box and 438
public focused Release checks. The cancellation test now observes
initial capacity admission as well as growth, and checks that the first
field can cancel the write before admitting the second field. It also
checks that the sink stays empty. The procedures archive preserves
the initial growth-only observation failure, the raw cancellation
exception found by its correction and the final structured result.
The amended tests keep their original peak and budget-reuse checks.

Both 32-file images passed all four stock CPython 3.13.2 output checks
before VM staging. A further 41 dialect and field cases matched CPython
on each image in both execution modes. These checks establish semantic
agreement. All 112 timed batches and both machine gates passed in
222 seconds, and the VM verified the persistent observations archive
under the lease after all workers joined.

[Timings](summary.json) · [Allocation](allocation-summary.json)
· [Raw observations](observations.tgz)
· [Sources, checks and procedures](checks-and-procedures.tar.xz).
