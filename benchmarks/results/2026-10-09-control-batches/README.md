# Short control-batch diagnostics

Two predeclared diagnostics on clean Release producer
`100158e02b888ab405206e8f74c71d4d77c45cdd` completed in **13.279 and 13.382 seconds**.
They support using longer batches in a future milestone. They establish neither
a qualified campaign nor an engine speedup. No full comparison lane ran.

Both runs used the same empty/tiny sources, fixtures and independent goldens from
the core-loop catalog. Ordinary Lython .NET 10.0.12 and isolated optimized CPython
3.13.16 retained their normal GC, tiering/specialization, public limits and output
checks. Each case had untimed verification, at least one second of successful
warmup per worker, a fixed two-second pause, seven rotating/reversed batch-count
rounds with alternating engine order, and final verification. Fixed counts were
chosen before collection: Lython 64/128/384 and CPython 2,048/8,192/24,576.
Neither controls nor output checks were subtracted from worker timings.

The table retains every variant. Batch time is the median; spread is interpolated
IQR divided by median. These are diagnostics from two processes per run, without
multi-session, paired-interval or noise qualification.

| Engine | Control | Invocations/batch | Run 1 median ms | Run 1 IQR % | Run 2 median ms | Run 2 IQR % |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Lython | Empty | 64 | 5.357 | 11.94 | 5.597 | 5.17 |
| Lython | Empty | 128 | 10.075 | 2.41 | 10.939 | 3.68 |
| Lython | Empty | 384 | 31.293 | 2.91 | 33.442 | 4.10 |
| Lython | Tiny | 64 | 5.084 | 5.83 | 5.261 | 8.90 |
| Lython | Tiny | 128 | 10.124 | 10.84 | 10.043 | 2.19 |
| Lython | Tiny | 384 | 31.286 | 2.67 | 31.272 | 2.29 |
| CPython | Empty | 2,048 | 3.115 | 1.33 | 3.345 | 0.82 |
| CPython | Empty | 8,192 | 12.336 | 0.69 | 13.290 | 1.00 |
| CPython | Empty | 24,576 | 37.331 | 0.94 | 39.904 | 1.11 |
| CPython | Tiny | 2,048 | 4.382 | 0.41 | 4.410 | 1.06 |
| CPython | Tiny | 8,192 | 17.466 | 0.52 | 17.613 | 0.22 |
| CPython | Tiny | 24,576 | 52.309 | 0.31 | 52.740 | 0.26 |

Lython's longest batches had 2–4% spread in both runs; some shorter variants
exceeded 10%. This supports longer batches, without proving the cause of every
historical control failure or a specific optimal duration. Policy v6 prospectively
targets two confirming 25 ms calibration batches and a 20 ms measured floor.
Its existing stability thresholds and 45/600-second deadlines remain unchanged.

Four half-second idle snapshots per run retained aggregate CPU counters and
every visible owned process/thread. Busy activity ranged **0–1.49%**, with no
observed steal. After empty-control measurement, matching .NET tiered-compiler
threads advanced by 10/20 ms in runs 1/2. Lython process counters advanced by
0/30 ms; these independently read 100-Hz counters have rounding and sampling
skew, so their totals need not match. No other matching owned thread advanced.
The observer used 0.191/0.199 CPU seconds overall and 1.2–2.2 ms per idle window.
These sparse snapshots do not attribute activity during measured batches or
cover paging, pressure and throttle counters. They cannot certify quietness.

Every response, requested count, round order, clock and source/fixture/output
digest was audited offline. All 84 measured batches per run completed with
correct output. Workers exited successfully, their PIDs were absent, recorded
timed inputs had unchanged hashes, and the producer stayed clean. Actual systemd
configuration proved a 30-second whole-group cap and zero stop timeout; services
were stopped and the shared lease was released. Failed collections were not
repeated or replaced.

The standalone observer was system Python 3.12.3; timed CPython remained the
pinned 3.13.16. Its script hash was checked after collection. Before/after hash
claims apply to the recorded timed inputs, not to an unrecorded observer identity.

Raw receipts, responses, source/input checks, actual service configuration,
observer, launcher and independent audit are retained under the ignored notes
directory `.git/agent-notes/milestone-controls-20261009/`.

| Artifact | SHA-256 |
| --- | --- |
| Run 1 diagnostic.json | `133b9f97454a153e2d421f9e092029a7ba5b73438ab13b0d0c4ac5015c676534` |
| Run 1 responses.jsonl | `e06d0fab0641c6a2f9335275eafe4c28fe0965824e51aa0670245fa4f896a20d` |
| Run 2 diagnostic.json | `9029bd7a9b44849c114970ff2813c94e11b08e8e51db6c5a4ca73afa8c66ed16` |
| Run 2 responses.jsonl | `d528358d8f3a72d4a6855e01ffece68608977c073bbc495cb3da63ff1ad51681` |
| Shared catalog | `6f2a5f018909e56a1d7fc62cceec75f4803687a0413c86f7a17ffc9727cd2092` |
| Benchmark worker | `0d71211ad2fe2834c0afb74014257afdf96b34c92ccf66d84b5efc145c6b96b5` |
| Lython library | `976a7455d1b373d2f092d95d7d20de5c357a32337b6534d42f55db4f66613e31` |
| Observer control-batches.py | `c57c64d301d28dd611928348474c20475d3be146ba344621a35a8d7fa65603c6` |
| Offline audit.py | `806528d1d0c615d4b52bd5b6589c214a1b26abc27a8acb342663c573e0ed27e6` |

Compilation and fresh-process policy v6 qualifies their total declared boundaries,
including intentional compiler setup or startup. Both controls still must qualify;
warm and compile-run retain the ten-times-control floor. This is a prospective
measurement-contract correction, independent of the table's spreads or observed
engine ordering. Historical v4/v5 exclusions remain under their original policies.

The policy change passed **370 comparison-related public tests in each of local
Debug and Release**, including 16 new boundary, required-control, timer-floor and
old-policy regressions. The Release benchmark build passed with no warnings or
errors. These checks validate behavior; milestone qualification is still pending.
