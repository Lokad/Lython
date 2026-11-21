# B1279 shared UTF-8 float output

The formatter uses invariant round-trip UTF-8 digits and applies Python's
decimal point and exponent conventions in the same bounded buffer. Default
float repr copies these bytes into funded guest text. Finite JSON output
appends them directly and completes synchronously. Nonfinite JSON retains
Python spelling and evaluates `allow_nan` through the existing truth
protocol, which can suspend. CLR string consumers use the same formatter.

Measured November 21, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Reference `7cc150d96` uses the frozen image built
from production-equivalent `9bf720efa`. The candidate is `959ec0ef5`.
Two workers per revision warmed for at least eight seconds on each case
and supplied seven rotated batches apiece. Ranges cross all pairings of
independent process medians. Warm elapsed includes mediated RAM-backed I/O,
output verification and prior-result cleanup. Process CPU covers the wider
request. Compilation, startup and final process disposal are separate.

| Complete script | Warm elapsed change | Process CPU change | Local CLR allocation change |
| --- | ---: | ---: | ---: |
| Finite JSON export | −9.97 to −5.81% | −10.00 to −3.42% | −23.83% |
| Finite text export | −7.56 to −4.37% | −6.10 to −3.54% | −15.33% |
| Integer JSON export | −2.39 to −0.37% | −2.74 to +0.61% | +0.0003% |
| CSV invoice | −3.05 to +0.45% | −2.91 to 0.00% | +0.0050% |

The export reads 3,072 records and writes 9,216 numeric values. The
finite input is 153,298 bytes. The JSON and text outputs are 262,640
and 155,119 bytes respectively. The finite JSON reference elapsed
spread is 4.10%, with 0.50% candidate spread. Both finite workloads
improve across the full process-median range. The controls do not
establish a wider CPU benefit.

Allocation falls from 5,384,596 to 4,101,588 bytes on the finite JSON
export and from 8,365,588 to 7,083,308 bytes on the text export. The
four accounted peaks match and all executions return the allowance
to baseline.

The 51,843-value repr corpus matches both the reference and local
CPython 3.13.2. It covers subnormals, signed zero, nonfinite values,
power-of-ten neighbors and deterministic random bit patterns. The
procedure archive retains the corpus and the corrected test driver.
Both complete frozen 32-file images pass the four CPython output
checks. Windows .NET 11 and Ubuntu .NET 10 each pass 481 white-box
and 2,389 public focused Release checks.

All 112 timed batches and both machine gates pass in 224 seconds.
Workers join and the persistent raw archive verifies under the lease.
The comparison reports two Lython builds. CPython supplies the
semantic reference for this collection.

[Timing](summary.json) · [Allocation](allocation-summary.json)
· [Scalar corpus result](corpus-summary.json)
· [Raw observations](observations.tgz)
· [Source, checks and procedures](checks-and-procedures.tar.xz).
