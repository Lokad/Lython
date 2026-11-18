# Share JSON numeric lexing with scalar construction

R534/B964 compared dd4c5165c (core 29192b95) with isolated R534 (core 40db9318)
on the quiet Azure F4as_v7 VM on 2026-11-18 UTC. Both images use the same .NET
11 RC3 worker, default GC and tiering, and 0.4.0 adapter. The comparison uses
two independent processes per image, eight seconds of rotated warmup and
five measured rounds per job. Results cover all process-median pairings.

The JSON lexer carries the numeric kind and significand to scalar construction.
It removes the kind rescan and separate machine-integer parse. A short
fixed-point token can produce its binary64 value with one division when the
significand and divisor are exactly representable. Other floating tokens use
the existing UTF-8 parser. Large integers keep their exact admitted conversion.
Callbacks receive the original token spelling and can still suspend.

| Complete script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Floating JSON report, warm | −10.80 to −7.30% | −9.89 to −6.56% | 1.96 / 1.93% |
| Floating JSON report, compile and run | −7.99 to −6.29% | −8.61 to −3.84% | 1.54 / 0.30% |
| Floating JSON report, compile | −0.72 to −0.60% | −0.99 to +1.31% | 0.12 / <0.01% |
| Integer JSON summary, warm | −0.52 to +5.30% | −2.66 to +4.48% | 1.42 / 4.36% |
| Helper text, warm control | −1.52 to −0.42% | −1.43 to +2.00% | 0.30 / 0.82% |

Negative percentages favor the candidate. Warm includes mediated RAM I/O,
output checks and prior-result cleanup. Compile and run also includes
compilation and compiled-owner disposal. Process CPU includes wider worker
work. Compilation, execution and startup remain distinct.

Both actual targets passed 250 white-box and 995 public focused Release tests.
The isolated probe matched exact CPython 3.13.2 results for 368 numeric tokens
through both APIs on each target. The cases include mixed signs, Int64
limits, binary64 rounding, subnormals and overflow. The existing JSON tests
cover original prefix/offset behavior, callbacks, cancellation and admission.

Both 32-file images passed all four complete golden programs. Accounted peaks
match, allowance balances restore and allocation differences are below 0.06%.
The five timing jobs took 233.79 seconds. The machine passed both quiet gates.
Workers joined and the archive hash matched. The only retired file was the
exact archived B852 prototype replica. The integer control diverged across
identical candidate processes, so its first comparison leaves uncertainty.

## Warm confirmation and retention

B965 reused the identical images and catalog. It increased warmup to 12
seconds per process and reversed the initial process order. Across all
pairings, floating elapsed fell 9.90–10.17% and CPU fell 6.70–9.72%.
Same-image elapsed spreads were 0.03% and 0.27%. Integer elapsed ranged
from 1.93% faster to 1.35% slower, with CPU ranging from 3.23% lower to
1.47% higher. The larger first-run slowdown did not reproduce. Both jobs
completed in 128.74 seconds with the same validation and cleanup gates.

Retain the shared lexical work and the measured floating report improvement.
The integer JSON result remains mixed. The combined source with R533 passed
300 white-box and 1,230 public focused checks per actual target. Full
qualification applies to that integrated source. Publication remains postponed.

[Summary](summary.json) · [Peak and cleanup checks](allocation-summary.json) ·
[Observations](observations.tgz) · [Source patch and procedures](procedures-and-checks.tar.xz) ·
[Confirmation](confirmation/summary.json)
