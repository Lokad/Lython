# B1288 JSON escape measurements reused during fill

This refines [B1287](../2026-11-21-json-string-validation/README.md).
The sizing and validation loop remembers the first four decoded escape
descriptors in 48 bytes of stack scratch. The exact admitted fill uses
their original byte positions and scalars. It replays any additional
escapes in the same bounded loop. No token changes its admitted
destination capacity. One additional test covers an 8,192-byte prefix
and escape replay after the scratch fills, through both run APIs.

Reference `256c5e651` and prototype `475051443` ran November 21,
2026 UTC on the quiet Azure F4as_v7 VM with Ubuntu 24.04.4 and .NET
11 RC3. The catalog and reference image are exactly those used for
B1287. Each lane uses two independently warmed processes per source
and seven rotated batches each. Ranges span all four process-median
pairings. Warm elapsed includes mediated RAM-backed I/O, guest
execution and prior-result disposal. Process CPU also covers request
and fixture handling. Compilation, startup and final teardown are
outside the warm clocks.

| Complete script | Warm elapsed change | Warm process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| JSON Lines export with expanded options | −4.50% to −0.36% | −4.13% to +0.32% | 3.99% / 0.33% |
| JSON normalization | −2.62% to +0.66% | −2.23% to +0.77% | 2.53% / 0.82% |
| Plain formatted report | −2.38% to +2.54% | −2.14% to +2.41% | 2.15% / 2.83% |
| Escaped-Unicode formatted report | −1.91% to −0.25% | −1.38% to +0.60% | 0.87% / 0.81% |
| Mixed-Unicode text control | −5.43% to −0.04% | −6.43% to +0.28% | 3.35% / 2.27% |

B1287's 2.23–4.18% escaped-report elapsed regression does not recur in
this refinement. Elapsed improves 0.25–1.91% on that workload with
tight duplicate controls. The CPU range spans zero. Plain report and
normalization elapsed and CPU also span zero. JSON Lines and the
unchanged text control both move favorably on elapsed with reference
process spreads above 3%. These measurements do not establish a
representative CPU improvement.

Keep the scanner and descriptor refinement private. This version
preserves the eliminated Unicode validation pass and avoids parsing
short escape sequences twice. The favorable escaped-report elapsed
result warrants preserving the design. The extra scratch and fill
branches do not yet pay for themselves across the other JSON scripts.
A further comparison should isolate a design that removes work without
expanding the fill paths.

Windows .NET 11 and Ubuntu .NET 10 each pass 217 white-box and 904
public Release checks. Both complete 32-file images match five CPython
goldens. The VM run verifies all 140 timed batches, passes both quiet
gates, joins all workers, removes RAM-backed fixtures and verifies the
persistent archive under the lease.

[Timing summary](summary.json) · [Raw VM observations](observations.tgz) ·
[Source, focused checks and procedures](checks-and-procedures.tar.xz).
