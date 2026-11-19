# B1063 inline ASCII membership for explicit trim

The private R632 prototype uses a 128-bit ASCII mask for explicit trim
characters and admits a hash set for non-ASCII members. It removes a
temporary allocation and its admission from each ASCII-only call. The
reference is `43c9f2c38` and the prototype is `d74c2437c`.

Windows .NET 11 and WSL .NET 10 each passed 524 focused Release checks.
The checks cover mixed Unicode, the two mask halves, cancellation after
Unicode scratch admission, unchanged identity and retained output.
Earlier fixtures that expected ASCII hash storage were updated to exercise
the remaining Unicode scratch. The exhausted-allowance test also checks
the unchanged ASCII result. Both complete 32-file images passed five
CPython goldens.

Local allocation fell 7.01% on expanded forwarding and 35.20% on the
mixed-Unicode trim script. Forwarding's accounted peak stayed equal.
The mixed case's peak fell from 1,261,168 to 1,261,040 B.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover all four combinations of duplicate process medians.
Negative changes favor the prototype.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Expanded forwarding | Warm | −1.64…+1.26% | −3.11…+3.29% |
| Expanded forwarding | Compile-to-result | −4.31…−2.95% | −4.31…−1.96% |
| Mixed Unicode trim | Warm | −12.48…−9.76% | −13.39…−10.43% |
| Default-whitespace normalization | Warm | +1.46…+2.97% | +1.79…+2.71% |
| Shipment JSON selector report | Warm | +0.94…+2.54% | −0.40…+3.13% |

The gains on explicit trim justify a refinement. The default-whitespace
case and the untouched JSON control both show adverse elapsed. Preserve
the existing dedicated whitespace scan and compare the same workloads
before selecting a retained design.

The initial staging attempt inherited a recovery-only launch setting
and stopped before a worker started. The corrected staging preserved the
same images and catalog. Both quiet gates and all 100 timed goldens passed
in 231.99 seconds. Workers joined, fixtures were removed and the archive
verified under the lease. Warm includes mediated I/O, goldens and
previous-result cleanup. Compile-to-result adds compilation and disposal.
Process CPU has a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks, initial staging failure and procedures](checks-and-procedures.tar.xz).
