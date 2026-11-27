# B1822 Start declaration preparation at the first declaration

The builder records the earliest descriptor when a declaration is
emitted. The preparation walk skips the preceding literal constants
and is empty for code without declarations. A function can reserve its
descriptor slot before emitting a lambda within a default expression.
The recorded index accounts for that order. Integer table allocation
stays in its original phase after capture preparation.

The reference tape shows 51 of 57 constant visits removed in the
configuration manifest, including six complete nonempty walks. The
ordinary factory skips two of 198 entries. The escaping-rule control
skips zero. These are source-derived work counts.

The two 32-file images match four independent CPython output and file
goldens. Nine compilation cases match instructions, lexical slots, capture
sources, signed integer tables and compilation admission traces. The
cases include nested defaults, all four declaration operations, signed
bounds and the B1821 closure pressure cases. Each compiled owner restores
its shared allowance. Both local Release targets pass 252 focused public
checks. The initial Ubuntu snapshot retained the constructor from the
previous prototype. Synchronizing the restored file and rebuilding
nonincrementally resolves its compile error.

## Allocation and admission

Windows .NET 11 measures warm allocations and peak execution admission.
Compiled admission at return matches on all four scripts.

| Workload | API | CLR bytes, reference → trial | Peak admitted bytes, reference → trial |
| --- | --- | ---: | ---: |
| Ordinary definitions | sync | 22,744 → 22,744 | 92,040 → 92,040 |
| Escaping rule control | sync | 737,944 → 737,944 | 2,786,655 → 2,786,655 |
| CSV invoice | sync | 3,016,758 → 3,016,702 | 9,039,397 → 9,039,397 |
| Configuration manifest | sync | 95,824 → 95,824 | 55,858 → 55,858 |
| Ordinary definitions | async | 22,992 → 22,992 | 92,040 → 92,040 |
| Escaping rule control | async | 738,192 → 738,192 | 2,786,655 → 2,786,655 |
| CSV invoice | async | 3,016,924 → 3,016,918 | 9,039,397 → 9,039,397 |
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
| Ordinary definitions | compile only | −0.98% to +6.88% | +2.17% to +7.88% |
| Configuration manifest | compile to result | −2.81% to +1.94% | −4.82% to −1.28% |
| Escaping rule control | warm sync | −0.53% to +0.35% | −5.08% to +9.17% |
| Configuration manifest | compile only | +0.04% to +0.83% | −3.23% to −0.02% |
| Ordinary definitions | compile to result | −3.38% to +1.45% | −2.59% to +2.64% |

Keep the declaration-start change for the removed visits and unchanged
admission. The manifest uses 1.28–4.82% less compile-to-result process
CPU in this local comparison. Its elapsed range includes zero.
The ordinary factory's compile-only CPU rises 2.17–7.88% with 5.15%
candidate worker spread. Its compile-to-result CPU spans zero, and
the unchanged escaping-rule control has wide candidate CPU spread.
Treat the ordinary compile-only result as open regression debt.
Check it at the next quiet compiler comparison. The trial establishes
no general compile or warm execution speedup.

[Allocation](allocation-summary.json) · [Compiler parity](compiler-parity.json) · [Visits](visit-counts.json) · [Timing](summary.json) ·
[Image hashes](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Patches and checks](procedures.tar.xz).
