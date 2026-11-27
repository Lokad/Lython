# B1858 CPython comparison for the signed regex adapter and comprehension compiler

Lython `c665a179e` uses the signed optional PythonRe 0.5.0 adapter from
`807d6e745`. It runs against stock CPython 3.13.16 on November 27, 2026 UTC.
The machine is an Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3,
with comparison host `05c1059de`. The Lython core, worker and all auxiliary
DLLs reuse the B1856 frozen candidate. Both verifiers match all three
independent CPython output and file goldens.

The two log searches scan the same 3,072-line, 86,954-byte fixture. Each
successful search reads one capture. One script calls a compiled pattern
and the other calls `re.search`. Both engines use their normal pattern
reuse in a running host. Lython reads through its mediated host with an
active shared allowance. CPython reads through its standard file API.
The generated comprehension report compiles 96 rules from 20,988 bytes of
source and uses no host input files.

Two workers per engine warm for at least five seconds each and supply seven
rotated batches. Ranges span pairings of worker medians. Ratios divide
Lython elapsed by CPython elapsed. Warm execution includes verification,
applicable RAM-backed I/O and previous-result disposal. Compilation only
includes compiled-owner cleanup. Compile to result adds one execution and
its cleanup. The wider process CPU clock also covers fixture reset and
request framing. Startup sits outside these clocks.

| Workload | Boundary | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| Compiled-pattern log search | Warm | 3.27639–3.30195 ms | 0.95498–0.99697 ms | 3.286–3.458× |
| Module-level log search | Warm | 3.70378–3.76358 ms | 1.55773–1.57929 ms | 2.345–2.416× |
| Compiled-pattern log search | Compile to result | 3.47070–3.54680 ms | 1.01756–1.06519 ms | 3.258–3.486× |
| Generated comprehension report | Compile only | 1.92082–1.94344 ms | 2.74657–2.75148 ms | 0.698–0.708× |
| Generated comprehension report | Compile to result | 2.37403–2.38947 ms | 2.85875–2.89189 ms | 0.821–0.836× |

The log search remains slower in these complete commands after the signed
adapter improvement. CPython compiled-log worker spread is 4.40% warm and
4.68% through result. The generated report is faster in both measured phases.
These are workload diagnostics. A separate source-pair comparison records
the allocation and CPU impact of the adapter update. [B1855](../2026-11-27-regex-signed-release/README.md).

All 140 timed batches and both quiet gates pass. Staging reuses all 32
worker files. Workers join and the persistent archive verifies under the
lease after the RAM-backed fixtures are removed.

[Timing summary](summary.json) · [Fixture sizes](fixture-census.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and procedures](procedures.tar.xz).
