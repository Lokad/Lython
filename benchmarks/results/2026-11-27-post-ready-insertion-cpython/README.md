# B1849 Quiet comparison after ready CSV and collection inserts

November 27, 2026 UTC. Lython `9cee6f423` and stock CPython 3.13.16 run
on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Lython uses comparison
host `05c1059de` with an active shared allowance. Both engines use the same
guest source. The fixtures are RAM-backed and Lython reads them through
the mediated host. Windows and VM verification both match all six
independent CPython output and file goldens.

Two workers per engine warm for at least five seconds each. Seven batches
rotate across the workers. Ranges span all pairings of worker medians and
ratios divide Lython elapsed by CPython elapsed. Warm execution includes
verification and previous-result disposal. The wider process CPU in the
raw data also includes fixture reset and request framing. Compile-only
includes compiled-owner cleanup. Compile-to-result adds one execution and
its cleanup. Startup is outside these clocks.

## Results

| Workload | Input | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| Five-item `starmap` shipment report, Lython `RunAsync` | 2,048 records / 27,350 B | 2.21170–2.25079 ms | 1.81365–1.83653 ms | 1.204–1.241× |
| Five-item `starmap` shipment report | 2,048 records / 27,350 B | 1.88707–1.97045 ms | 1.82219–1.83011 ms | 1.031–1.081× |
| Bound `starmap` shipment report, Lython `RunAsync` | 2,048 records / 27,350 B | 1.72813–1.81626 ms | 1.66921–1.66997 ms | 1.035–1.088× |
| File-fed CSV quantity map, Lython `RunAsync` | 2,048 records / 27,350 B | 0.41105–0.42158 ms | 1.36997–1.37152 ms | 0.300–0.308× |
| Nested-object JSON document summary | 2,048 records / 103,331 B | 0.77037–0.77301 ms | 0.66955–0.67079 ms | 1.148–1.155× |
| Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | 0.89492–0.90934 ms | 0.96086–0.96766 ms | 0.925–0.946× |
| Ordinary factory, compile-only | 96 definitions / 8,641 B source | 0.56833–0.56893 ms | 0.95428–0.95543 ms | 0.595–0.596× |
| Ordinary factory, compile-to-result | 96 definitions / 8,641 B source | 0.76639–0.76762 ms | 1.12265–1.13974 ms | 0.672–0.684× |

Wide async shipment overlaps the [B1844](../2026-11-27-post-csv-json-cpython/README.md)
range of 1.213–1.236×. Wide sync is closer to CPython. Bound async Lython
workers differ in elapsed by 5.10%. Its CPython baseline is much tighter.
The within-Lython B1847 bound CPU result remains open.

The same nested JSON fixture takes 0.77037–0.77301 ms, compared with
0.73690–0.74752 ms in B1844. CPython remains near 0.67 ms. The checkpoints
bracket several runtime changes. The [B1848](../2026-11-27-dictreader-json-control/README.md)
repeat of the intervening CSV-only source pair spans zero at both positions.
Keep the current JSON increase visible and use a direct source comparison
before attributing it.

All 224 batches and both quiet gates pass. Staging verifies and reuses all
32 worker files, streams only 356,923 B of new inputs and keeps the required
persistent space. All workers join, the RAM-backed fixtures are removed and
the persistent archive verifies under the lease.

## Callable storage assessment

The read-only compiler snapshot uses the same core DLL as this comparison.
The ordinary factory has 97 local slots, 96 default starts and 93 deferred
declarations. Its operand extent peaks at 101 even when those 93 declaration
intervals are excluded. Two binary instructions outside them reach 101.
The whole workspace has 164 slots. The escaping factory has two default
starts and zero deferred declarations. Its workspace has seven slots.

Writing the 93 deferred declarations directly to their final slots could
remove writes and reads. It would leave the ordinary factory's physical
capacity unchanged because the four operand slots are still needed and the
separately live keyword defaults still have their final slots. The earlier
B1820 version also kept the fallback staging and used more warm sync CPU.
Keep the current admission and staging. Revisit after identifying an owner
or persistent slot that can be removed across both execution paths.
[Snapshot](defaults-census.json) ·
[B1820](../2026-11-27-final-default-slots/README.md).

[Timing summary](summary.json) · [Fixture sizes](fixture-census.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and procedures](procedures.tar.xz).
