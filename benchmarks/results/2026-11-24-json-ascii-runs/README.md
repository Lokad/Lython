# B1585 Bounded ASCII runs in JSON string emission

The private `db64b017a` candidate replaces byte-by-byte ASCII classification
in the shared key and string emitter with bounded searches of unchanged runs.
Both `ensure_ascii` modes keep the existing builder, growth admission and
cancellation checks. Non-ASCII bytes still go through the same Rune decoder.
This comparison leaves builder growth and publication in place.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 with .NET 11 RC3 on November 24,
2026 UTC. The verified 32-file images for `ffa2f83f4` and `db64b017a` differ
only in the core DLL. All five scripts verify CPython 3.13.16 goldens. Each
revision has two workers with eight-second warmups and seven rotated batches.
Ranges span candidate/reference pairings of worker medians. Warm elapsed
includes mediated RAM-backed I/O, output verification and previous-result
cleanup. Wider CPU includes reset and protocol. Compilation, startup and
final cleanup sit outside the clocks.

| Warm script | Elapsed change | Wider process CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| JSON Lines, expanded options | +0.41 to +0.79% | +1.04% | 0.21 / 0.17% |
| JSON Lines, default options | −2.91 to +1.77% | +0.19 to +1.96% | 3.28 / 1.49% |
| JSON Lines, heavy escaping | −0.64 to +0.67% | −0.69 to 0.00% | 1.03 / 0.28% |
| Short text report control | −5.18 to +1.37% | −4.81 to +1.57% | 2.53 / 4.27% |
| Quoted CSV invoice control | −2.03 to −0.33% | −1.96 to −0.70% | 1.05 / 0.68% |

Keep the candidate private. The expanded script is slightly slower on both
clocks with stable workers. The default script gives mixed elapsed and higher
CPU. The unchanged CSV control moves favorably. These data leave the exact
effect uncertain and show no consistent JSON improvement. CLR allocation is
within 458 bytes per run. All five peak admissions match and every execution
returns the allowance after cleanup.

Separate instrumented runs find 7.31 unchanged ASCII bytes per search on the
expanded and default scripts, and 1.89 on the escape-heavy script. The two
controls never enter the emitter. A Windows x64 tiering-disabled inspection
shows the emitter growing from 1,049 to 1,209 bytes. The timings do not
establish whether generated code size contributes to the result. Both
platforms pass 221 white-box and 986 public focused Release checks.

The initial disk preflight refused staging before deleting files or starting
workers. It underestimated the staging space consumed by the larger catalog.
The successful retry kept the measured workloads, sample sizes and checks
and used separate staging and collection reserves. All 140 batches, both
quiet gates, worker joins, fixture removal and archive verification pass.

[Timing and spread](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Scan counts](run-counts.json) · [Images](identity.json) · [Observations](observations.tar.xz) ·
[Procedures, failed preflight and JIT](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
