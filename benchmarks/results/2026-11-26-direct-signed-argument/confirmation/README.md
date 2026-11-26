# B1775 quiet confirmation

The same reference `088cf9d37` and candidate patch ran on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 26, 2026 UTC.
Two workers per image and lane warmed for five seconds each and ran seven
rotated batches targeting 440 ms per request. The ranges span all four paired
worker medians. Negative changes mean lower time. [Medians and spreads](summary.json)

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment control | Warm async | −4.17% to −0.04% | −4.64% to −0.44% |
| Ordinary definitions | Warm async | −15.68% to −6.22% | −15.16% to −2.57% |
| Escaping rules control | Warm sync | −3.77% to −0.28% | −5.68% to −1.43% |
| Defaults-only diagnostic | Warm sync | −6.85% to −5.76% | −8.09% to −4.73% |
| Ordinary definitions | Warm sync | −5.53% to −3.75% | −5.30% to −4.09% |

Warm elapsed includes applicable RAM-backed I/O, output and file checks, and
result disposal. Process CPU also includes framing and fixture reset. The
ordinary async reference workers differ by 11.41% in CPU. Shipment and
escaping rules use zero direct signed arguments and their CPU also falls.
These controls limit attribution of the whole measured change to the removed
handoff. The complete sync factory and the defaults-only case have less than
1.55% same-image elapsed spread. Both use the new route 2,560 times per run.
Keep the direct transport on the strength of those cases and matching target
allocation and peak admission. [Local block, routes and allocation](../README.md)

Windows .NET 11 passes 4,615 focused Release checks and Ubuntu WSL .NET 10
passes 2,367. All 32 files in each image verify. Both images match the same
four independent CPython goldens before collection. The staged candidate also
passes those goldens on the VM. All 140 timed batches complete, both quiet
gates pass, workers join, and the raw archive verifies after fixture cleanup.
[Image identities](identity.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz)
