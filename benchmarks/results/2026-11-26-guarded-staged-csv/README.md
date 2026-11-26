# B1755 Keep staged CSV publication inside the native read

The previous file path ended the read guard before completing a staged
record. A host admission callback could then advance the same source while
the outer parser still owned that row's descriptors. The focused regression
reproduces the nested advance in both hosting modes. `4a273f36d` keeps the
guard until publication, including after an asynchronous refill. A matching
private header uses the same combined admission after native staging and
after an in-buffer field transformation. Admission still revalidates the
header before adoption. An exposed header follows the live path.

| Workload | Rows using combined admission, before → after | Local CLR bytes, before → after | Peak admitted bytes, before → after |
| --- | ---: | ---: | ---: |
| Quoted invoice, 4,096 data records | 3,064 → 4,095 | 3,019,238 → 3,019,238 | 9,040,266 → 9,040,266 |
| Shipment, 2,048 data records | 2,046 → 2,047 | 1,160,870 → 1,160,864 | 2,817,644 → 2,817,644 |
| File-fed map, 2,048 data records | 2,046 → 2,047 | 808,528 → 808,528 | 2,270,071 → 2,270,071 |
| Plain invoice reader, 4,096 data records | 0 → 0 | 2,854,510 → 2,854,510 | 8,252,450 → 8,252,450 |

The reference is `93441ac82`, with the same runtime as `3507bdd2d`. The
separately instrumented transformed-only trial adopted the same rows as the
reference on these fixtures. Extending adoption through staging adds 1,031
eligible invoice rows in either API. The older invoice catalog's size field
says 1,024. The 174,147 B fixture contains 4,096 data records. Local allocation
uses uninstrumented Windows .NET 11 workers. The two complete frozen images
match independent CPython output and file goldens, and allowance balances
return to baseline.

| Native file workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Quoted invoice | Sync | −1.87 to −0.37% | −1.27 to −0.77% |
| Quoted invoice | Async | −1.56 to +0.02% | 0.00 to +2.08% |
| Plain invoice reader | Sync | −2.79 to −0.74% | −4.49 to −0.47% |

The November 26, 2026 UTC block ran on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Two processes per source warm for at least seven
seconds and supply seven rotated batches. Ranges cover the four pairings of
worker medians. Warm execution includes mediated RAM-backed I/O, output
verification and disposal. Process CPU also includes fixture reset and
framing. All 84 batches completed, workers joined and the VM verified the
archive under the measurement lease after removing fixtures.

Sync invoice CPU falls in this block, with favorable movement in the
plain reader as well. Async invoice CPU is flat to 2.08% higher. Retain
the reentry correction and single admission for matched rows. Whole-script
CPU improvement remains uncertain. Windows .NET 11 and Ubuntu .NET 10 each
pass 179 white-box and 478 public focused Release checks covering CSV, file
reading and delayed host composition. [Full delivery run](https://github.com/Lokad/Lython/actions/runs/45819964308).

[Timing](summary.json) · [Allocation](allocation-summary.json) · [Routes](routes.json) ·
[Transformed-only routes](transformed-only-routes.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and regression logs](procedures.tar.xz).
