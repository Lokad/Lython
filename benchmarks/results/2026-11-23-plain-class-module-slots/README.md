# B1532 keep the module's operand slots through a plain class declaration

The quote-heavy CSV invoice had boxed its terminal integer results on
assignment because every class declaration triggered a mirror of all
module locals. The prototype `328a051a7` skips this mirror for a bare
class with a callback-free declaration suite. It compares against
`48bf2bc2f` and keeps the same fixed tuple return.

| Script and boundary | Change in elapsed | Change in process CPU |
| --- | ---: | ---: |
| Quote-heavy CSV invoice, warm | −10.78% to −8.80% | −11.73% to −7.26% |
| Quote-heavy CSV invoice, compile to result | −10.56% to −10.00% | −9.68% to −9.07% |
| Helper-driven CSV invoice, warm | −0.88% to +2.36% | −2.44% to +1.54% |
| Small configuration edit, compile to result | −1.49% to +0.67% | −1.42% to +0.19% |
| Function-scoped supplied calls, warm | −1.41% to +2.16% | −3.23% to +1.43% |

Ranges span all four process-median pairings. The quoted script
allocates 656,912 fewer CLR bytes per run on Windows .NET 11, a 16.68%
reduction. Its shared allowance peak increases by 608 bytes, from
9,432,874 to 9,433,482. The other six measured peaks match.

All seven smoke scripts match CPython output on both frozen images.
Each platform passes 1,089 focused public checks. The original
prototype fails 35 of 1,071 focused white-box checks per platform. A
reusable execution context exposes a live dictionary to its caller and
needs publication during the execution as well as afterward. These
timings belong to the original private prototype.

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on November
23, 2026 UTC. Each of four workers contributed seven rotated batches
per lane after eight seconds of warming. Warm includes mediated
RAM-backed I/O, verification and prior-result disposal. Time to result
adds compilation and code disposal. Wider process CPU includes
request handling. All 140 batches verify, including joined workers,
fixture cleanup and the image checks before and after measurement.

[Raw observations](observations.tar.xz) · [Timing](summary.json) ·
[Identity](identity.json) · [Allocation](allocation-summary.json) ·
[Source, verification and tests](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
