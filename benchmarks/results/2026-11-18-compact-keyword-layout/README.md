# Compact layout for native guest keyword binding

B998 compared baseline `0f48fcbbf` (frozen `bde27322`) with the compact
layout (frozen `47e0a58d`) on the quiet Azure F4as_v7 VM, 2026-11-18 UTC.
The compact layout replaces the first prototype's native dictionary with
an immutable sorted key/slot array. Definitions and lambdas share prepared
parameter data, including keyword-default keys. Positional arguments
keep a single representation check. Existing CLR consumers retain their
established name admission. [First prototype and original adverse observations](../2026-11-18-utf8-guest-keyword-binding/README.md)

| Workload and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Expanded forwarded report, warm | −5.44% to −0.17% | −5.42% to −0.61% |
| Expanded forwarded report, compile-to-result | −6.28% to −5.37% | −6.92% to −4.42% |
| Expanded forwarded report, compile | −1.08% to −0.34% | −1.26% to +1.45% |
| Ordinary named calls, warm | −0.88% to +2.84% | −2.13% to +3.13% |
| Ordinary named calls, compile | −2.40% to +0.43% | −0.85% to −0.71% |
| F-string report control, warm | −3.87% to +0.62% | −3.50% to +1.26% |
| Positional calls, warm | −0.73% to −0.19% | −0.41% to +1.62% |

The 1,024-record report's compile-to-result spread was below 0.71% for
each image. The warm candidate spread was 5.05%, so its gain has an
uncertain magnitude. The f-string and ordinary named
compilation adversity from B997 did not recur consistently. Ordinary
named warm remains mixed, with possible elapsed and CPU debt.

Local report allocation fell 8.15% and accounted peak fell 4.48%, with
the host allowance restored. Control allocations and peaks were effectively
unchanged. The ordinary named cold compilation allocation diagnostic
was 42,512 B versus 42,752 B. B997's larger cold difference remains in
its own record.

Warm includes mediated RAM-backed I/O, golden checks and prior-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Startup,
fixture reset and final cache teardown are outside elapsed. Process CPU
covers the wider worker request. Both 32-file .NET 11 RC3 images passed
verification. All seven jobs passed both quiet gates and completed in
323 seconds, with every worker joined and the raw archive verified under
the lease.

Each .NET 10 and .NET 11 target passed 734 focused Windows public checks,
and 104 WSL public and 318 WSL white-box checks. The Unicode test covers
multiple named slots, original overflow-key identity, positional-only
parameters and exact spelling against CPython. Retain the compact source
based on the compile-to-result and allocation results.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
