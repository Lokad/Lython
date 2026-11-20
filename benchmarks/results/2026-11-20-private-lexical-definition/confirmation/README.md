# B1228 Lexical definition admission confirmation

The exact B1227 images and catalog ran on the same quiet Azure VM with
12-second warmups and seven rotated 0.8-second batches. Timing boundaries
and pairing match [B1227](../README.md).

| Warm script | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: |
| Postponed nested factories | +3.72% to +10.39% | +3.58% to +9.69% | 1.83% / 4.52% |
| Retained nonlocal siblings | −10.00% to −6.63% | −8.83% to −5.05% | 2.23% / 1.47% |
| CSV invoice | −2.30% to +1.18% | −0.88% to +1.25% | 2.52% / 1.02% |

The earlier postponed regression and sibling improvement recur with
supporting CPU. The invoice intervals span zero. The combined design
stays private. Both quiet checks and all 84 timed batches passed, workers
joined, guest fixtures were removed and the persistent archive verified.

[Timings](summary.json) · [Raw observations](observations.tgz) ·
[Both procedures](../source-and-checks.tar.xz).
