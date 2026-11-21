# B1273 one governed UTF-8 casing and casefold pipeline

Measured `b140244f0` against `406388271` on November 21, 2026 UTC on
Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. The candidate adds
casefold to the pinned Python mappings. It removes the separate
fold lookup, CLR expansion encoding, growing builder and final
copy. Admission now covers the conservative final capacity up front.
[Initial casing prototype and observations](../README.md).

| Complete warm text report | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Original map report, 35,840 B | −9.78 to −6.24% | −9.78 to −5.87% |
| Mixed Unicode lowercase, 107,520 B | −14.04 to −11.73% | −13.25 to −11.48% |
| Mixed Unicode casefold, 107,520 B | −15.81 to −7.84% | −14.33 to −7.35% |
| ASCII lowercase control, 35,840 B | +0.45 to +3.68% | 0.00 to +3.14% |

Every file contains 3,072 lines. Ranges cover all pairings of two
independently warmed process medians per source, with seven rotated
batches per process. Negative changes indicate faster runs. The
casefold candidate medians differ by 6.69%. Both casefold processes
improve over both controls. The ASCII result reverses the initial
prototype observation. Keep the consolidation and Unicode gains,
with the ASCII regression recorded for the next relevant checkpoint.

Elapsed includes execution, mediated RAM-backed I/O, output checks
and prior-result cleanup. Process CPU covers the wider batch,
including fixture reset and request processing. Startup, compilation
and final process teardown sit outside these warm measurements.

The local casefold report allocates 9.48% less CLR storage. Its
accounted peak rises from 4,167,236 to 4,237,892 bytes (1.70%).
The conservative final allocation can refuse earlier near a shared
allowance limit. The rest of the reports' accounted peaks match and
all budgets return to baseline. The core DLL grows 17.5 KiB.

Windows .NET 11 and Ubuntu .NET 10 each pass 254 white-box and
313 public Release checks, including cancellation, retained funding,
terminal refusal, and contextual casing. Independent audits on both
platforms match CPython 3.13.2 for all 1,112,064 valid single scalars
across lower, upper, title, capitalize, swapcase and casefold.
The generated files reproduce exactly.

Both quiet gates and all 112 batches pass in 369 seconds. Both
32-file images and all CPython file/output goldens verify. Workers
join, guest fixtures are removed and the raw archive verifies under
the lease. The first local retrieval expected three jobs after taking
the four-job terminal observation. Download and verification use
that cached observation. The original condition remains in the
procedures.

[Timing summary](summary.json) · [Local allocation](allocation-summary.json)
· [Raw observations](observations.tgz) · [Source, checks and procedures](checks-and-procedures.tar.xz).
