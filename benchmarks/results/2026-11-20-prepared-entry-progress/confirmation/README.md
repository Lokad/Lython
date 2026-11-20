# B1226 Async factory and invoice confirmation

Uses the exact B1225 images and catalog on the same quiet Azure VM.
The two independent processes per role warm for at least 12 seconds.
Seven rotated rounds target 0.8 seconds per batch. Timing boundaries
and four-way median pairings match [B1225](../README.md).

| Workload | Boundary | Elapsed change | Wider process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm `RunAsync` | −2.65% to +2.47% | −2.38% to +2.52% | 3.81% / 1.40% |
| File-backed CSV invoice | Warm | −1.64% to +0.50% | −2.44% to +0.85% | 0.12% / 2.05% |

Both elapsed and CPU intervals span zero. The invoice's initial
across-pair CPU increase did not recur. Async factory timing remains
uncertain. The first staging attempt stopped before starting a worker
because it duplicated the existing catalog and protocol. The repeat
reused those verified immutable files under the original disk floor.
All 56 batches and both quiet checks then passed. Workers joined and
the verified persistent archive survived fixture teardown.

[Timing](summary.json) · [Raw](observations.tgz) ·
[Both collection procedures](../source-and-checks.tar.xz).
