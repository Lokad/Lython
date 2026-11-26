# B1789 Quiet confirmation of deferred-definition site placement

The [local diagnostic](../README.md) provides the same frozen images,
source patch, fixture goldens and phase boundaries. This run uses the
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two workers per
source warm for five seconds and supply seven rotated timed batches
with a 440 ms target. Ranges span all four pairings of worker medians.

| Script and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Ordinary definitions, compile only | −0.37% to +0.34% | −0.39% to +2.00% |
| Protected file search, compile only | −0.44% to +1.32% | −1.48% to +2.74% |
| Ordinary definitions, through result | −1.13% to +1.07% | −2.42% to +0.35% |
| Escaping rules, through result | −2.90% to −0.05% | −1.39% to +0.19% |

The local spread and adverse control CPU do not recur. All four quiet CPU
ranges cross zero. Retain the change to retire the completed-tape scan while
preserving the generated tape and compiler allowance. The data establishes
no compilation speedup. The earlier B1788 compile-side cost remains open.

Both quiet checks and all 112 batches pass. Workers join, fixtures are
removed and the persistent observations archive verifies. Staging reclaimed
5,828,608 bytes by retiring one inactive B1780 VM worker DLL also referenced
by its confirmation. Both full images matched the frozen local copy, a
separate compressed DLL backup was checked, and both committed timing
archives stayed byte-identical.

[Timing and spread](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and custody](procedures.tar.xz)
