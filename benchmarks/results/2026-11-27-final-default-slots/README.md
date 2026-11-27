# B1820 Stage deferred defaults in their final slots

This trial on 489fde50e writes admitted positional and keyword defaults
directly into the native frame slots that already own deferred definitions
after placement. The native prefix skips the completed placement copy.
When a default is still being evaluated at handoff or failure, its
unpublished owners move back into the existing ordinary staging window.
That window remains in the shared frame layout. Admission runs at the
original instruction and still prepays eventual materialization.

The generated ordinary factory has 93 marked declarations out of 96.
The escaping factory has no marked declarations. A declaration replacing
a parameter keeps ordinary staging so default expressions can observe the
previous binding. Both frozen images match all four independent CPython
goldens. The final trial passes 3,603 focused Release public checks on
Windows .NET 11 and Ubuntu WSL .NET 10. Two completed cases match
CPython 3.13.2. The suspension regression joins file cleanup and restores
the shared allowance after cancellation during a keyword default.

## Allocation and admission

Windows .NET 11 runs each complete script in both APIs. The measurements
include per-run CLR allocations and peak execution admission. Compiled
admission matches between images on each case. Shared allowance returns
to baseline on all runs.

| Workload | API | CLR bytes, reference → trial | Peak admitted bytes, reference → trial |
| --- | --- | ---: | ---: |
| Ordinary factory | sync | 22,744 → 22,744 | 92,040 → 92,040 |
| Escaping rule factory | sync | 737,944 → 737,944 | 2,786,655 → 2,786,655 |
| CSV invoice control | sync | 3,016,758 → 3,016,702 | 9,039,397 → 9,039,397 |
| File search control | sync | 56,160 → 56,160 | 36,512 → 36,512 |
| Ordinary factory | async | 22,992 → 22,992 | 92,040 → 92,040 |
| Escaping rule factory | async | 738,192 → 738,192 | 2,786,655 → 2,786,655 |
| CSV invoice control | async | 3,016,974 → 3,016,974 | 9,039,397 → 9,039,397 |
| File search control | async | 56,408 → 56,408 | 36,512 → 36,512 |

## Local comparison

Ubuntu WSL runs two workers per source with three seconds of warmup and
seven rotated timed batches per worker. Intervals span the four pairings of
worker medians. Negative values mean less time. Warm execution includes
mediated RAM-backed I/O, output and file verification and previous-result
disposal. Process CPU also includes fixture reset and request framing.
Compile-only includes compilation and compiled-program disposal.
Process startup sits outside all lanes.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary factory | warm sync | +3.04% to +16.96% | +2.58% to +11.71% |
| Escaping rule factory | warm async | −5.21% to −1.44% | −5.81% to −3.92% |
| Ordinary factory | warm async | −7.43% to +3.54% | −9.91% to +5.56% |
| File search control | compile only | −4.20% to +2.93% | −1.89% to +0.34% |
| Ordinary factory | compile only | −10.53% to +1.53% | −11.76% to +18.68% |

Retain the current runtime. The ordinary factory uses 3.04–16.96% more
warm sync elapsed time and 2.58–11.71% more process CPU in the local
comparison. Its unchanged-image workers vary, and the escaping rule
control uses 3.92–5.81% less CPU. Warm async and compile CPU for the
ordinary factory span zero. Execution allocation and admitted peaks
match on both factories in both modes. The trial removes a slot copy
while retaining the same workspace and prepayment. Keep the prototype
and raw observations as evidence. A follow-up should remove a complete
reservation or physical storage stage before timing again.

The four retained regression cases also pass on the restored runtime on
both local Release targets after a nonincremental build.

[Allocation](allocation-summary.json) · [Timing](summary.json) ·
[Image hashes](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Patches and checks](procedures.tar.xz).
