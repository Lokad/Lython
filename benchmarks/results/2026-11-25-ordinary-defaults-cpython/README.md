# B1677 Ordinary definitions and complete reports against CPython

This checkpoint measures committed Lython `2b1e8a6f2` and CPython 3.13.16 on
November 25, 2026 UTC. The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3.
Lython uses a fresh nonincremental Release build and the existing comparison
worker. Two independent processes per engine verified every requested result.

Each phase warms the four processes for five seconds and measures seven
rotated batches with a 0.38-second target. Warm execution includes applicable
RAM-backed file reads and writes, verification and previous-result disposal.
Lython uses its mediated file host with shared admission active. CPython uses
native file APIs. Compile-only includes disposal of compiled owners. Time to
result includes compiling, running and cleanup within a running process.
Fixture reset and request framing live outside elapsed and inside the wider
CPU clock. Startup and final process teardown are separate.

| Workload | Phase | Lython | CPython | Elapsed ratio | Wider CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary definitions | Warm | 0.33095–0.33407 ms | 0.14815–0.14969 ms | 2.211–2.255× | 2.221–2.279× |
| Ordinary definitions | Compilation | 0.57605–0.57925 ms | 0.95232–1.12055 ms | 0.514–0.608× | 0.515–0.596× |
| Ordinary definitions | Compile to result | 0.90849–0.92274 ms | 1.11483–1.12533 ms | 0.807–0.828× | 0.813–0.819× |
| Defaulted rule pairs | Warm | 0.85428–0.87529 ms | 0.68466–0.68999 ms | 1.238–1.278× | 1.233–1.284× |
| Defaulted rule pairs | Compilation | 0.02191–0.02197 ms | 0.04158–0.04168 ms | 0.526–0.528× | 0.521–0.545× |
| Defaulted rule pairs | Compile to result | 0.91498–0.94855 ms | 0.73768–0.74033 ms | 1.236–1.286× | 1.265–1.293× |
| Shipment routing | Warm | 2.39657–2.41685 ms | 1.62635–1.64929 ms | 1.453–1.486× | 1.434–1.461× |
| Quote-heavy invoice | Warm | 6.16075–6.18561 ms | 6.31161–6.41122 ms | 0.961–0.980× | 0.967–0.993× |

Ratios divide Lython by CPython and span all four worker-median pairings.
The ordinary-definition warm gap is larger than the rule or shipment gaps.
The elapsed time to result is lower for Lython on the ordinary workload.
CPython compilation worker spread is 17.66% there, limiting the standalone
compilation ratio's precision. The time-to-result samples have separate
clocks and smaller worker spread. Defaulted-rule time-to-result spread is
3.67% for Lython. Treat these ranges as diagnostic, without subtracting
independently measured phases or attributing the gaps to specific functions.

The complete shipment, invoice and two definition goldens pass. All 224
batches, worker joins, quiet-machine gates and persistent archive checks pass.

[Identity](identity.json) · [Timings and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
