# B1641 Bind regex cursors to the prepared subject

Each cursor uses its own original string and normalized window for provider
preparation, capture validation and projection. The cursor owns this string
reference through provider cleanup. Lazy `finditer` clears its pending inputs
after successful opening and still avoids preparation for reversed windows.
Published matches keep the original string and their final capture ranges.
This removes the second set of window and subject inputs at each cursor pull.

Windows and Ubuntu each pass 789 focused .NET 11 Release tests and 124
.NET 10 tests. These cover in-flight provider cancellation, abandoned cursor
cleanup, terminal refusal, shared allowance reuse and independently retained
matches. Both frozen 32-file images match three complete CPython 3.13.2
goldens. The Unicode target scans 1,024 clipped matches with captures before
the match, retains them through `re.purge`, splits and replaces with a guest
callback. The compiled log scan uses single searches. The quoted invoice
uses no regex.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Unicode cursor window | Warm | −1.06 to +2.78% | −1.85 to +4.17% | 3.32 / 0.55% |
| Compiled log scan | Warm | −2.86 to +2.94% | −4.55 to +3.17% | 3.39 / 2.50% |
| Quoted CSV invoice | Warm | −0.64 to +2.95% | −3.45 to +2.27% | 2.15 / 1.43% |
| Unicode cursor window | Compile to result | −7.28 to −1.48% | −5.31 to −1.74% | 6.18 / 0.07% |

The reference is `33264df8d`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 24, 2026 UTC. Both images use the pinned
Utf8Regex 0.4.0 adapter. Ranges cover the four pairings of medians from two
workers per image and seven rotated batches per worker. Elapsed includes
applicable RAM-backed I/O, result verification and prior-result disposal.
Time to result adds compilation and compiled-owner disposal. Process CPU
also includes request framing and fixture reset.

Peak admission matches. The cursor reserves 8 more bytes per prepared
operation for its reference. The target allocates 16 more CLR bytes per
run. Every allocation diagnostic returns the allowance to baseline.
Warm elapsed and CPU cross zero across all three workloads. The target
reaches its result sooner in this collection, with a 6.18% spread between
the reference workers. The changed path does not establish a compilation
improvement. Retain the ownership invariant and small measured storage cost.
All 112 timed batches, both quiet gates, joins, fixture cleanup and archive
verification pass.

[Image identity](identity.json) · [Allocation](allocation-summary.json) ·
[Timings](quiet-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Exact patch and procedures](procedures.tar.xz).
