# B1486 Separate native-call activation from closed returns

Private `70fc5d1e4` moves the existing inline values, argument binding and
activation into a separate non-inlined helper. Selected closed factory and
integer returns bypass the workspace. On Windows x64 the Tier 1 stack
subtraction for `StartNativeCall` falls from 520 to 184 bytes. The ordinary
activation still uses the same layout in an additional 448-byte frame, before
saved registers and call overhead. These are JIT observations for this build.

The quiet Azure F4as_v7 compared both 32-file images on November 23,
2026 UTC, on Ubuntu 24.04.4 and .NET 11 RC3. The reference core
`f2ac9fa89` has the same runtime sources as `edb02618a`. Seven independent
CPython goldens and all 224 timed batches verified. Ranges cover pairings
of medians from two workers per source, after eight seconds of warmup and
seven rotated batches each. Warm elapsed includes applicable mediated RAM
I/O, output verification and prior-result cleanup. Compile to result adds
compilation and compiled-owner disposal. Process CPU also includes worker
reset and transport. Startup and final teardown sit outside elapsed.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −2.91 to +4.00% | −3.30 to +2.50% | 3.82 / 3.17% |
| Fully supplied helper | Warm | −1.77 to −0.12% | −3.79 to −0.70% | 1.57 / 0.11% |
| Eager nested factories | Warm | −6.99 to +4.73% | −6.35 to +6.19% | 6.26 / 5.98% |
| Short nonlocal word report | Warm | −2.91 to +10.61% | −2.26 to +9.76% | 4.22 / 9.31% |
| Factory defaults observer | Warm | −0.83 to +4.74% | 0.00 to +4.89% | 1.10 / 4.46% |
| Wide native calls | Warm | −1.26 to −0.56% | −3.25 to +1.10% | 0.15 / 0.56% |
| Postponed nested factories | Compile to result | −3.15 to +2.51% | −2.40 to +2.96% | 0.93 / 4.88% |
| Quote-heavy CSV invoice | Warm | +0.22 to +1.06% | −0.73 to 0.00% | 0.84 / <0.01% |

The factory effect is inconclusive, the defaults observer has potential
CPU debt and the short text worker is unstable. The fully supplied control
uses less CPU, but the measured change does not justify the additional call
and ordinary stack frame. Keep the existing production path and reconsider
operand transport when it displaces call setup or storage. Local allocation
is within 120 bytes per complete workload and the admitted peaks match.
Both allowances return to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,310 white-box and 2,649
public focused Release checks. The original small-stack suite passes all
37 cases on Windows. On WSL the continuation resumes on a pool thread in
6 of 6 isolated attempts per source with the original fixture. The second
command can start on another worker while the original prefix is busy.
The fixture now observes ambient context restoration on the original
worker before releasing the delay. The same exact core binaries pass 3 of 3
attempts each on the intended 256 KiB thread. The retained source passes
all 47 stack and thread-context checks on each platform with `f16d52732`.
The original failed condition and its logs are preserved.

Both quiet gates, worker joins, guest fixture removal and archival checks
pass. [Identity](identity.json) · [Timings](summary.json) ·
[Local allocation](local-allocation.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures and diagnostics](procedures.tar.xz).
