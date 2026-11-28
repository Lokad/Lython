# B1959 Bind fixed-width sum arguments on the managed stack

The candidate extends the existing fixed-width builtin binder to bodies
that consume their inputs before returning asynchronous work. Ordinary
executable `sum` calls no longer allocate an array to hold the final
arguments. A suspended sum takes the bound iterable and start value with
its existing owner. Expanded calls keep their owned arguments and the same
signature checks.

The Windows allocation witness saves 65,568–65,624 CLR bytes per 2,048-row
tuple or range script in both APIs. Compiled admission, peak execution
admission and growth-request counts match. All commands return allowance to
baseline. Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 566
focused Release cases, including builtin signatures, promotion, admission
and cancellation.

Both rounds use the same verified 32-file images on executable source
`09cecc9ce`. Only the core DLL differs, and four CPython 3.13.16 output
and file goldens verify per image. Timed ranges pair all medians from two
workers per image. Elapsed includes RAM-backed mediated I/O, verification
and previous-result disposal. Process CPU adds fixture reset and framing.
Compilation and startup are outside both clocks.

## Local WSL comparison

November 28, 2026 with .NET 11 RC3. Each worker warms for at least five
seconds and contributes seven rotated timed batches. All 140 pass.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Tuple generator reduction | `Run` | −1.96% to +0.46% | −4.37% to +1.36% |
| Tuple generator reduction | `RunAsync` | −7.04% to +25.97% | −6.63% to +17.92% |
| Range generator reduction | `RunAsync` | −1.08% to +0.20% | −3.67% to +1.46% |
| Iterable shipment control | `RunAsync` | −4.23% to +2.30% | −4.48% to +1.10% |
| File-fed map control | `RunAsync` | −10.27% to +6.12% | −16.43% to +3.10% |

The tuple async reference CPU spread is 20.73%. File-fed CPU has more
than 10% worker spread on each image. Preserve the adverse upper ranges
and use the repeat to assess the async effect.

## Quiet async repeat

November 28, 2026 UTC on the four-vCPU Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Both quiet gates and all 84 timed batches pass.

| Workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Tuple generator reduction | −2.82% to −1.53% | −3.65% to −2.16% |
| Range generator reduction | −2.58% to +2.70% | −2.06% to +2.13% |
| File-fed map control | −4.46% to +3.47% | −3.55% to +4.32% |

Tuple CPU spread stays below 0.80% on each image. The file-fed
candidate workers differ by 7.54% in CPU. Retain the allocation removal
and the confirmed async tuple improvement. The range and sync timings
remain inconclusive. The VM reuses 63 frozen image files. It verifies one
losslessly compressed historical core and removes the guest fixtures after
the measured workers join.

[Quiet summary](quiet-summary.json) · [Local summary](local-summary.json) ·
[Allocation](allocation.json) · [Admission](admission.json) · [Images](identity.json) ·
[Quiet observations](quiet-observations.tar.xz) · [Local observations](local-observations.tar.xz) ·
[Source and procedures](procedures.tar.xz).
