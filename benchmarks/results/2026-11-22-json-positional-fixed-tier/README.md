# B1379 Fixed-tier positional-call diagnostic

Repeated both B1377 controls on the same 32-file images from
`927f0ca88` and `a3338b0a8`. The quiet Azure F4as_v7 runs Ubuntu
24.04.4 and .NET 11 RC3. Both workers per image use the same
`DOTNET_TieredCompilation=0` override. Worker identities record it
and verify every other runtime override is absent. This is a
diagnostic boundary. B1376 and B1377 record normal-tier behavior.

| Complete warm script | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Positional calls | −5.95 to −2.67% | −5.93 to −2.58% | 3.37 / 0.11% |
| Guest keyword forwarding | −0.45 to +0.78% | −0.36 to +1.90% | 0.43 / 0.80% |

Ranges span the pairings of two process medians per image. Warm elapsed
includes execution, stdout verification and prior-result disposal.
Wider CPU includes worker reset and request handling. Compilation and
startup are separate. All 56 batches verified in 114 seconds. Both
quiet gates, worker join and persistent archive verification passed.
The stage reused both images and retired one byte-verified duplicate
trace already archived in Git.

The positional result reverses direction when tiering is disabled.
Together with the B1377 listings, this establishes sensitivity to the
execution configuration and motivates profiling the actual default-tier
hot path. The normal-tier regression remains open. The diagnostic
also retains its 3.37% reference-worker spread and the inconclusive
forwarding control.

[Timing](summary.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) ·
[Normal-tier confirmation](../2026-11-22-json-positional-control/README.md).
