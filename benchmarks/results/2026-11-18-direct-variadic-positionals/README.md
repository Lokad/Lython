# R564 direct positional overflow

The shared binder formerly accumulated spill values in a governed `PyList`,
then copied them into the guest `*args` tuple. Its temporary backing remained
charged even when a later missing argument aborted the call. The binder now
records the first spilled operand and count during validation. After checking
the required arguments and exact defaults, it admits and fills the final tuple
from those same operands. The caller owns them until binding finishes. This
also covers a bound receiver that spills and expanded positionals interleaved
with named arguments. The dictionary used for keyword overflow is outside this
change.

## Evidence

Comparison on 2026-11-18 between `da1946b1c` (`f35e7fd6`) and the archived
source patch (`087eebd6`). Local Windows 11, Core i7-14700KF, .NET
11.0.0-rc.3.26522.148, CPython 3.13.2. Both frozen worker images passed the
stdout and file checks for the 1,024-record UTF-8 forwarding report and the
unchanged positional-call control. The report uses a helper with a keyword-only
format argument and forwards its positional overflow to `str.format`.

| Workload | CLR allocation per warm run | Peak accounted execution memory |
| --- | ---: | ---: |
| UTF-8 forwarding report | 4,148,566 → 3,853,654 B (−7.11%) | 5,999,417 → 5,278,521 B (−12.02%) |
| Positional control | 1,060,696 B in both | 1,383 B in both |

The allocation sample uses three warmed observations per worker and checks
allowance reuse after result cleanup. Allocated bytes are process-wide. The
accounted peak is Lython's estimate rather than a CLR live-heap measurement.

| Case and lane | Reference → updated, first pair | Reference → updated, reversed roles |
| --- | ---: | ---: |
| Report warm | 17.652 → 14.701 ms | 22.142 → 21.604 ms |
| Report compile to result | 16.019 → 15.463 ms | 16.438 → 31.731 ms |
| Report compilation | 65.18 → 71.64 µs | 49.89 → 50.58 µs |
| Positional control warm | 3.518 → 3.880 ms | 4.158 → 3.843 ms |

Warm timing includes mediated file I/O, golden checks and prior result disposal.
Worker startup, fixture reset and final cache teardown are outside elapsed.
Compilation is timed separately. Compile to result includes disposal of the
compiled owner. The report's CPython warm medians moved from 10.288 to
17.890 ms. The large adverse reverse-order compile-to-result sample is unresolved.
These local timings do not establish a throughput or CPU change. Retention is
based on eliminating scratch storage and its stranded allowance.

The new caught-error regression fails on the reference by 704 charged bytes
and passes on the updated core. Focused Release checks passed on .NET 10 and 11
on Windows (29 white-box and 193 public tests per target) and under WSL (29
white-box per target). They cover aliases, missing arguments, admission refusal,
cancellation during admission and reuse.

[Machine-readable summary](summary.json) · [Source patch, procedure, raw observations and checks](evidence.tar.xz)
