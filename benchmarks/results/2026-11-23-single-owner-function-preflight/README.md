# B1437 Prepay callable storage with evaluated defaults

Private `a1b6913c4` admits the callable and known annotation backing with
its evaluated positional defaults. The already private default tuple
carries the combined charge through suspension and refunds it on
abandonment. The callable takes its share before publication. This
reuses the existing operand tag and adds no per-callable field. The
combined request can be refused before default expressions run.

The reference is `d84047ca4` with the exact retained B1436 core. Only
the core differs across the two verified 32-file images. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23,
2026 UTC.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | +2.14 to +6.78% | +2.74 to +7.59% | 0.57 / 3.95% |
| Postponed nested factories | Compile to result | −1.21 to +3.05% | +0.15 to +1.86% | 1.74 / 2.53% |
| Eager nested factories | Warm | −3.06 to +0.93% | −3.42 to −0.56% | 2.71 / 1.37% |
| Factories observing defaults | Warm | −3.45 to +0.55% | −1.85 to −0.26% | 1.64 / 2.46% |
| Fully supplied helper | Warm | −1.44 to −0.15% | −0.78 to −0.19% | 0.95 / 0.35% |
| Quote-heavy CSV invoice | Warm | +0.25 to +8.95% | +1.14 to +8.33% | 2.52 / 6.00% |
| Large positional control | Warm | −0.50 to +1.52% | −0.49 to +1.51% | 0.54 / 1.48% |
| Ordinary split report | Warm | −0.41 to +2.87% | −0.30 to +1.80% | 0.10 / 3.19% |

Ranges span pairings of medians from two independent workers per source.
Each worker warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes applicable mediated RAM-backed I/O, output
verification and previous-result disposal. Compile to result also
includes compilation and compiled-owner disposal. Wider CPU includes
resets and protocol. Startup and final teardown are outside elapsed.

The instrumented host measures 6,169 to 4,121 growth requests in both
regular factory scripts and 6,170 to 4,122 in the observer. Each of
2,048 inner definitions replaces requests for 520 and 672 bytes with
one request for 1,192 bytes. The fully supplied helper remains at
seven. Local allocation is effectively flat. Admitted peaks match and
every owner returns its allowance.

Keep this source private. Eager and observer CPU improve, while
postponed warm elapsed and CPU get worse in every pairing. First-result
elapsed spans zero and CPU rises. The quoted invoice also uses more CPU.
Its 6.00% candidate spread leaves the size of the impact uncertain.
The invoice has no evaluated positional defaults. The source and
measurements leave the cause of its regression unresolved. A new
construction design needs to displace more work or explain this debt.

Windows .NET 11 and Ubuntu .NET 10 each pass 2,619 focused Release
checks and the four original default-accounting stress cases. Twenty-nine
Debug checks exercise storage assertions. Both images match seven
complete CPython goldens and two additional scripts covering eager and
postponed evaluation order, a callable retained by a failing decorator,
default replacement and an abandoned definition. Hosted async executions
match the same output. Their local timing ranges vary. Two focused
refusal tests confirm that the combined request stops before invoking a
default callback. The reference admits the separate requests and
completes in those tests.

Both quiet gates and all 224 timed batches pass in 437 seconds. Workers
join, fixtures are removed and the archive verifies under the lease.
Staging recovered three missing VM observation replicas from verified
Git bytes before retiring two older core images. The original staging
failure and public test filter correction remain in the procedures.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Host requests](local-growths.json) ·
[Observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
