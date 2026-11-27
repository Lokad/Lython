# B1895 Confirm generator-expression timing on the quiet VM

This is a comparison of the exact B1893 images, `63ae080ac` and `af9558316`.
The candidate loops directly over the acquired iterator and avoids creating a
namespace for the optimized `.0` local. It retains the previously measured
13.09% allocation reduction at the same admission peak. B1894 and later
revisions are outside this timing comparison. [Original B1893 measurements](../2026-11-27-direct-generator-outer/README.md).

On November 27, 2026 UTC the Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3.
Both complete 32-file images matched their hashes and verified four independent
CPython 3.13.16 output and file goldens. The timed scripts both read 2,048 CSV
records from 27,350 bytes. The reduction creates a generator expression for each
row. The file-fed quantity map uses ordinary calls over the same input.

Two workers per image warm for at least five seconds each and supply seven
rotated batches. Ranges compare every pairing of worker medians. Warm elapsed
includes mediated RAM-backed reads, output and file verification, and prior-result
cleanup. Process CPU adds fixture reset and request framing. Compilation and
startup are outside the clocks.

| Workload | API | Reference elapsed | Candidate elapsed | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: | ---: | ---: |
| Per-row generator reduction | `Run` | 2.836–2.867 ms | 2.682–2.700 ms | −6.47% to −4.79% | −6.79% to −6.27% |
| Per-row generator reduction | `RunAsync` | 3.404–3.405 ms | 3.290–3.372 ms | −3.38% to −0.93% | −4.71% to −1.61% |
| File-fed map control | `Run` | 0.404–0.415 ms | 0.400–0.413 ms | −3.65% to +2.23% | −4.35% to +2.10% |
| File-fed map control | `RunAsync` | 0.409–0.423 ms | 0.420–0.422 ms | −0.77% to +3.25% | −0.41% to +2.56% |

The quiet result reverses the adverse sync CPU result from both WSL passes.
Sync reduction CPU medians differ by at most 0.29% within either image. Both
unaffected controls span zero in both clocks. Keep B1893 and its original
local evidence.

All 112 batches pass. Both quiet checks pass with zero sampled busy and steal
time. Staging streams about 11.3 MiB and reuses immutable files from the earlier
signed-regex image and the current candidate. Workers join and the guest
fixtures are removed before the persistent archive verifies under the lease.

[Timing summary](summary.json) · [Image identity](identity.json) ·
[Staging](stage-custody.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz).
