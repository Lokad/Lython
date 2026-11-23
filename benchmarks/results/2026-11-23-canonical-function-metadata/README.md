# B1460 Share the executable function parameter layout through its code

Private `fae84c7ac` makes compiled code own its parameter layout.
Function values and prepared definitions lose the duplicate reference.
Generated callables keep their independent signatures. The reference
is `fe081752a` with its exact B1457 core.

Two workers per source ran on the quiet Azure F4as_v7, Ubuntu 24.04.4
and .NET 11 RC3 on November 23, 2026 UTC. Workers warmed for eight
seconds each and ran seven rotated batches. Ranges span all pairings
of process medians. Warm elapsed includes applicable mediated
RAM-backed I/O, verification and previous-result disposal. Compile to
result adds compilation and compiled-owner cleanup. Process CPU also
includes fixture resets and protocol. Startup and final teardown are
outside elapsed.

| Workload | Phase | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | +0.15 to +2.35% | −1.74 to +1.02% | 1.92 / 0.26% |
| Wide native-buffer calls | Warm | −4.57 to +6.76% | −4.83 to +8.40% | 2.59 / 9.05% |
| Eager nested factories | Warm | +2.41 to +7.56% | +2.36 to +8.22% | 0.16 / 4.86% |
| Factories observing defaults | Warm | −4.02 to −0.61% | −6.50 to −0.42% | 3.39 / 0.16% |
| Fully supplied helper | Warm | +0.03 to +5.18% | −1.16 to +4.96% | 0.68 / 4.43% |
| Quoted CSV invoice | Warm | −1.35 to +3.03% | −2.44 to +1.79% | 1.38 / 3.02% |
| Postponed nested factories | Compile to result | +0.27 to +3.58% | +1.04 to +3.92% | 2.72 / 0.56% |
| Plain split | Warm | −2.62 to −1.36% | −2.84 to −0.95% | 0.87 / 0.42% |

Each factory run allocates 32,776 fewer bytes. That is 3.72% of
either regular factory run and 2.67% of the defaults observer.
Factory compilation allocates 24 bytes less. Both execution and
compilation restore their allowances at identical admitted peaks.

Keep this source private. The eager CPU and postponed time-to-result
ranges are adverse. The defaults observer improves. The Windows
Tier 1 diagnostic identifies a new virtual layout lookup in each
omitted-argument helper. The caller already has the immutable code,
so passing its layout through both prepared reads is worth a
bounded refinement. The profile and timing leave the cause of the
overall regression open.

Windows .NET 11 and Ubuntu .NET 10 each pass 348 white-box and 624
public focused Release checks. Both 32-file images match seven CPython
goldens. All 23 inspected code tapes, operand slots, native flags and
exception layouts agree. All 224 timed batches verify. Workers join,
guest fixtures are removed and the archive verifies under the lease.

[Timing](summary.json) · [Run allocation](allocation-summary.json) ·
[Compile allocation](compilation-allocation-summary.json) · [Code parity](executable-parity.json) ·
[Tier 1 inspection](jit-summary.json) · [Image identities](identity.json) ·
[Raw observations](observations.tar.xz) · [Source and checks](procedures.tar.xz).
