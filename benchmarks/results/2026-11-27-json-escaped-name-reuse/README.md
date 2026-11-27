# B1824 Reuse admitted names for short escaped JSON fields

A short escaped property name can decode into bounded managed stack
scratch and look up the immutable record layout. A hit reuses its
canonical key without allocating or temporarily admitting a separate
string. A miss admits the actual UTF-8 value before allocation. Long
names and values use the same escape writer and final-string owner.
The existing token validation, error offsets and hook paths remain.

The affected inputs are localized versions of the existing service
totals and JSON Lines reports. The two property names are café and 株数,
encoded with ordinary JSON Unicode escapes. The documents contain
2,048 and 4,096 data records respectively. Reported totals and output
bytes match their plain-key controls. The plain-key scripts are unchanged.
Both frozen 32-file images match all four independent CPython goldens.
Windows .NET 11 and Ubuntu .NET 10 each pass 925 public and 238 white-box
focused Release tests covering offsets, Unicode, key aliases, duplicate
fields, hooks, cancellation and governed cleanup.

## Allocation and admission

Windows .NET 11 runs both APIs with the borrowed allowance. The allowance
returns to baseline after every execution. Peak execution admission and
retained compiler admission match on the four cases.

| Workload | API | CLR bytes, reference → trial | Peak admitted bytes, reference → trial |
| --- | --- | ---: | ---: |
| Plain service totals control | sync | 815,500 → 815,506 | 1,806,411 → 1,806,411 |
| Plain JSON Lines control | sync | 1,370,518 → 1,370,518 | 3,297,534 → 3,297,534 |
| Escaped service totals | sync | 1,175,630 → 815,352 | 1,806,407 → 1,806,407 |
| Escaped JSON Lines | sync | 2,141,224 → 1,420,448 | 3,347,008 → 3,347,008 |
| Plain service totals control | async | 815,800 → 815,800 | 1,806,411 → 1,806,411 |
| Plain JSON Lines control | async | 1,371,182 → 1,371,682 | 3,297,534 → 3,297,534 |
| Escaped service totals | async | 1,176,232 → 815,960 | 1,806,407 → 1,806,407 |
| Escaped JSON Lines | async | 2,142,230 → 1,421,280 | 3,347,008 → 3,347,008 |

## Local comparison

Ubuntu WSL runs two workers per source with three seconds of warmup and
seven rotated timed batches per worker. Intervals span the four pairings of
worker medians. Negative values mean less time. Warm execution includes
mediated RAM-backed I/O, output and file verification and previous-result
disposal. The wider process CPU clock includes fixture reset and request
framing. Compilation and startup sit outside these warm lanes.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Escaped service totals | warm sync | −22.69% to −13.07% | −17.91% to −11.00% |
| Plain JSON Lines control | warm async | −4.66% to −2.64% | −5.98% to −2.02% |
| Escaped JSON Lines | warm async | −9.00% to +0.78% | −11.64% to +0.96% |
| Plain service totals control | warm sync | −5.88% to +14.96% | −6.32% to +13.63% |
| Escaped service totals | warm async | −6.20% to −1.65% | −12.32% to −2.35% |

Keep the shared escape writer and short-name reuse. The localized
document allocates about 31% less and localized JSON Lines allocates
about 34% less in each API. Peak admission and the existing ownership
balances match. The escaped document's local sync comparison uses
13.07–22.69% less elapsed and 11.00–17.91% less process CPU. Its async
lane also uses less elapsed and CPU. The initial plain document and
escaped Lines ranges cross zero.

The pinned repeat gives the escaped Lines script 7.08–9.91% less
elapsed and 2.53–13.74% less CPU. The plain document control has
mixed elapsed and uses 2.55–5.45% less CPU, so the exact size of the
Lines CPU effect remains uncertain. Retain both blocks and the
allocation result. These observations concern short escaped property
names in repeated record layouts.

## Pinned repeat

The same frozen images and goldens rerun the two inconclusive lanes
on one logical CPU in Ubuntu WSL. Workers warm for six seconds before
seven rotated batches. Both timings retain the original boundaries.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Escaped JSON Lines | warm async | −9.91% to −7.08% | −13.74% to −2.53% |
| Plain service totals control | warm sync | −4.72% to +2.63% | −5.45% to −2.55% |

[Allocation](allocation-summary.json) · [Fixture fields](field-census.json) · [Timing](summary.json) ·
[Image hashes](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Patches and checks](procedures.tar.xz).

[Repeat timing](confirmation/summary.json) · [Repeat raw observations](confirmation/observations.tar.xz) · [Repeat receipt](confirmation/archive-receipt.json) · [Repeat procedures](confirmation/procedures.tar.xz).
