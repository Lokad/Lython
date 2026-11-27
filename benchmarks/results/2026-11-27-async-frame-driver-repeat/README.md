# B1863 Repeat the async frame-driver comparison in reverse lane order

The repeat uses the exact two 32-file images from B1854 and reverses the
four existing lane positions. The reference contains `9cee6f423`.
The candidate fuses the async frame and call-chain drivers, removing the
per-frame task handoff. The original source patch and focused Release
checks still apply. Both images passed all three independent CPython
output and file goldens locally and on the VM. [B1854](../2026-11-27-async-frame-driver/README.md).

The run uses the same Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on
November 27, 2026 UTC. Four workers per lane warm for at least five seconds
each and contribute seven rotated batches. Ranges span pairings of worker
medians. Warm elapsed includes mediated RAM-backed I/O, verification and
previous-result cleanup. Wider process CPU includes fixture reset and
framing. Both exclude compilation and startup.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Wide shipment control | Sync | −3.68% to +1.90% | −3.54% to +2.60% |
| Bound shipment | Async | +2.64% to +4.99% | +2.82% to +6.48% |
| File-fed map | Async | −5.21% to −0.60% | −5.71% to −0.10% |
| Wide shipment | Async | −1.34% to +3.27% | −1.98% to +1.85% |

The file-fed async CPU reduction reproduces with a smaller lower bound.
The original adverse sync comparison becomes inconclusive. Candidate
sync workers differ by 5.97% on CPU. Wide async still spans zero and
bound async moves adversely on elapsed and CPU. Keep the patch out of
delivery. The simpler driver remains worth reconsidering if a design can
address the bound path or a measurement can explain the differing results.

Both quiet gates and all 112 batches pass. Collection takes 164.10
seconds. The workers join and the persistent archive verifies under
the lease after the RAM-backed fixtures are removed.

[Timing summary](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and verification](procedures.tar.xz).
