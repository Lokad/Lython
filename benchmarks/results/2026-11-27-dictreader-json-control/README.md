# B1848 Repeat the earlier JSON control in two positions

[B1846](../2026-11-27-ready-dictreader-row/README.md) improved the two
targeted CSV workloads. Its unchanged nested JSON document used
2.46–3.68% more process CPU and 0.22–1.44% more elapsed time. This check
reuses both exact images from B1846, including every worker and adapter
file. It runs the same JSON case first and last with opposite initial
worker orders. An unaffected Unicode word-count report sits between them.
The JSON fixture has 2,048 records and 103,331 bytes. The text fixture
has 35,840 bytes. Both complete scripts match independent CPython outputs
and file checks on Windows and on the VM for both images.

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 27,
2026 UTC. Two workers per source warm at least five seconds each and
supply seven rotated batches per position. Warm elapsed includes mediated
RAM-backed I/O, result verification and prior-result disposal. Process CPU
also includes fixture reset and request framing. Compilation and startup
sit outside both clocks.

| Workload | Position | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested JSON document | First | −1.25% to +2.32% | −1.53% to +2.82% |
| Unicode word-count report | Second | −5.15% to +4.87% | −5.59% to +3.89% |
| Nested JSON document | Last | −1.30% to +0.48% | −1.53% to +2.08% |

Ranges span all pairings of worker medians. Both JSON positions span zero
in both measures. The Unicode text reference workers differ by 7.19% in
CPU. The repeat leaves the effect on JSON unresolved and gives no basis
to attribute the earlier increase to an operation. Close the bounded
confirmation and keep the original adverse result in the benchmark guide.

All 84 batches pass and both quiet checks pass. Staging reuses all 64
role-specific image files and streams only 232,758 B of catalog and
procedure inputs. All workers join, RAM-backed fixtures are removed and
the persistent archive verifies under the lease.

[Timing summary](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Staging](stage-custody.json) · [Goldens and procedures](procedures.tar.xz).
