# B1943 Quiet shipment comparison after iterator simplification

This checkpoint measures committed Lython `bf10eeaea` against stock CPython
3.13.16 on November 28, 2026 UTC. The four-vCPU Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3. Both quiet gates pass. The full 32-file worker
image matches its recorded hashes and verifies all four output and file
goldens locally and on the VM. All 168 timed batches verify.

| Warm workload | Lython API | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Iterable shipment | `Run` | 2.02553–2.04452 ms | 1.93197–1.97349 ms | 1.026–1.058× | 1.025–1.038× |
| Iterable shipment | `RunAsync` | 2.16532–2.22912 ms | 1.93969–1.95271 ms | 1.109–1.149× | 1.103–1.151× |
| Ordinary helper and tuple-generator reduction | `Run` | 2.56967–2.60110 ms | 1.97599–1.98347 ms | 1.296–1.316× | 1.284–1.305× |
| Ordinary helper and tuple-generator reduction | `RunAsync` | 2.94731–2.99740 ms | 1.96750–1.97411 ms | 1.493–1.523× | 1.520–1.522× |
| File-fed CSV quantity map | `RunAsync` | 0.43536–0.44757 ms | 1.36214–1.37161 ms | 0.317–0.329× | 0.352–0.366× |
| Range-generator control | `RunAsync` | 3.61478–3.87966 ms | 2.01849–2.03588 ms | 1.776–1.922× | 1.784–1.895× |

Ratios divide Lython by CPython. Ranges span all pairings of medians from
two workers per engine. All scripts use 2,048 rows from a 27,350-byte CSV.
The range control changes the ordinary reduction's generator to iterate
two range positions and index the same two fields. Lython's range elapsed
medians differ by 7.33%, so keep that range diagnostic.

Warm elapsed includes mediated RAM-backed I/O, output and file verification,
and previous-result disposal. Process CPU adds fixture reset and framing.
Compilation and startup are outside these clocks. Both APIs use the same
engine build. The benchmark remains a comparison of complete scripts.

The reported ordinary async gap is narrower than B1937's 1.584–1.619×.
Iterable shipment is still around 1.13× CPython async. The file-fed map
shows higher elapsed than its independent B1937 run. A matched run of the
two frozen Lython images confirms the direction for both workloads.
[Matched B1944](matched-control/README.md) · [B1937](../2026-11-28-current-cpython-shipment/README.md) ·
[Matched sum change](../2026-11-28-private-generator-sum/README.md) ·
[Matched iterator change](../2026-11-28-direct-sequence-iterator/refined/README.md).

## Managed attribution

Separate local Ubuntu WSL EventPipe captures sample the same frozen image
on one CPU. Each capture checks output before and after and between all
202 sampled batches. Four seconds of warmup precede the six-second trace.
The table uses shares of resolved Lython stack samples, excluding known
waits and unclassified native leaves. Inclusive shares overlap.

| `RunAsync` workload | Verified invocations | Generator advance, inclusive | Generator setup and detach, inclusive | Async loop advance, inclusive | Instruction prefix, nearest |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ordinary tuple reduction | 1,664 | 26.8% | 1.1% | 1.8% | 17.8% |
| Iterable shipment | 2,432 | 73.3% | 2.1% | 16.1% | 17.0% |
| File-fed map | 8,832 | 0.0% | 0.0% | 0.0% | Outside the top 35 |

The nearest async loop-advance method accounts for 1.7% on ordinary
reduction, compared with B1939's 7.2%. Memory reservation is 6.1% nearest
within the ordinary reduction's generator region and 9.8% across the
whole script. It is 4.2% nearest across iterable shipment. Sample shares
are descriptive and depend on each capture's denominator. The frame setup
share gives little reason to reopen isolated generator initialization.
[B1939](../2026-11-28-ordinary-reduction-profile/README.md).

The first local verification command failed before worker launch due to
shell argument quoting. The corrected LF launcher verified the same
image and input before remote staging. The VM reused 30 immutable donor
files under the lease. All workers join, fixtures are removed and the
archive is verified before the lease ends.

[Timing](summary.json) · [Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Managed attribution](managed-attribution.json) · [Managed captures](managed-captures.tar.xz).
