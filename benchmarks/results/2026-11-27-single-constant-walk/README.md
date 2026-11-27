# B1821 Prepare machine literals during declaration preparation

The builder classifies the immutable constants while it prepares the code
object's declarations. The separate constructor walk disappears. Exact
signed values use the same admitted nullable integer table. Constants
and instruction metadata retain their original values and coordinates.

The two 32-file images match four independent CPython output and file
goldens. Seven local compilation cases match the complete instruction
metadata and signed machine tables, including wide integers and nested
captures. The prepared tables match the original integer constants by
value on both images. Cancellation during a successful one-entry table
reservation releases that temporary charge before returning the invalid
script. Disposing each compiled owner restores the shared allowance.

Windows .NET 11 passes 5,872 focused Release checks. Ubuntu WSL .NET 10
initially passes 5,867. The five remaining local checks require the
matching Release probe and a CPython executable named python. They pass
after building the probe and putting CPython 3.13.2 on the test-local path.
Both original failure conditions remain in the procedures.

## Allocation and admission

Warm allocation and admitted peaks are from Windows .NET 11. Compiled
admission at return matches on all four scripts.

| Workload | API | CLR bytes, reference → trial | Peak admitted bytes, reference → trial |
| --- | --- | ---: | ---: |
| Ordinary definitions | sync | 22,744 → 22,744 | 92,040 → 92,040 |
| Escaping rule control | sync | 737,944 → 737,944 | 2,786,655 → 2,786,655 |
| CSV invoice | sync | 3,016,758 → 3,016,702 | 9,039,397 → 9,039,397 |
| Configuration manifest | sync | 95,824 → 95,824 | 55,858 → 55,858 |
| Ordinary definitions | async | 22,992 → 22,992 | 92,040 → 92,040 |
| Escaping rule control | async | 738,192 → 738,192 | 2,786,655 → 2,786,655 |
| CSV invoice | async | 3,016,974 → 3,016,974 | 9,039,397 → 9,039,397 |
| Configuration manifest | async | 96,040 → 96,040 | 55,858 → 55,858 |

## Local comparison

Ubuntu WSL runs two workers per source with three seconds of warmup and
seven rotated timed batches per worker. Intervals span the four pairings of
worker medians. Negative values mean less time. Warm execution includes
mediated RAM-backed I/O, output and file verification and previous-result
disposal. Compile-only includes compilation and previous-program disposal.
Compile to result adds execution, mediated I/O, output verification and
current compiled-owner disposal. The wider process CPU clock includes
fixture reset and request framing. Startup sits outside all lanes.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | compile only | −1.63% to +0.27% | −0.94% to +2.67% |
| Configuration manifest | compile to result | −4.34% to +2.59% | −6.30% to +1.59% |
| Escaping rule control | warm sync | −12.95% to +12.94% | −15.37% to +12.61% |
| Configuration manifest | compile only | −7.46% to +4.57% | −4.53% to +0.06% |
| Ordinary definitions | compile to result | −1.36% to +8.74% | −2.17% to +7.18% |

Keep the existing compilation passes. All five local elapsed and CPU
ranges include zero. The ordinary compile-only lane measures −0.94%
to +2.67% CPU, and the unchanged escaping-rule control has wide
reference-worker spread. The four complete workloads match peak
compilation admission. Earlier table admission changes the first
refused reservation in the capture-after-literal case at a shared
allowance of 46,239 B. In the two-local capture case, the admitted
literal table overlaps the snapshot scratch and adds 80 B to peak
compilation admission, from 44,844 B to 44,924 B. At 44,844 B the
reference compiles and the candidate refuses. Refusal remains terminal
and the allowance is reusable after disposal. Keep the evidence and
revisit only with a design that avoids the additional overlap or can
remove more of the compiler work.

[Allocation](allocation-summary.json) · [Compiler parity](compiler-parity.json) · [Timing](summary.json) ·
[Image hashes](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Patches and checks](procedures.tar.xz).
