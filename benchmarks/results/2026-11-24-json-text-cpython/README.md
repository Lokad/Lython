# B1555 Expanded JSON and short text against CPython

The frozen Lython image measures `eabbf02ed`, with the same production
and test sources as combined delivery `000ab0632`. The external workers
run stock CPython 3.13.16. Measurements ran November 24, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.

| Workload and phase | Lython elapsed | CPython elapsed | Lython / CPython | Worker spread, Lython / CPython |
| --- | ---: | ---: | ---: | ---: |
| Expanded JSON Lines, warm | 5.17670–5.33134 ms | 6.71809–6.78934 ms | 0.762–0.794× | 2.99 / 1.06% |
| Short nonlocal word report, warm | 1.25743–1.30115 ms | 0.93700–0.93752 ms | 1.341–1.389× | 3.48 / 0.06% |
| Short nonlocal word report, compile to result | 1.35625–1.39144 ms | 1.08911–1.09256 ms | 1.241–1.278× | 2.59 / 0.32% |
| Short nonlocal word report, compile only | 0.02846–0.02850 ms | 0.05509–0.05557 ms | 0.512–0.517× | 0.15 / 0.86% |

The ranges divide Lython elapsed by CPython elapsed across all pairings
of independent process medians. The JSON script reads 3,072 records
and 189,441 bytes. The report reads 3,072 lines and 35,840 bytes.
Both execution clocks include mediated or native RAM-backed I/O,
golden checks and disposal of the preceding result. Compile to result
includes compilation and its owner disposal. Compile only measures
that phase independently. Fixture reset, protocol, startup and process
teardown are outside elapsed. The raw data includes wider process CPU.

Expanded export runs in 76–79% of CPython warm time. The short report
shows a larger remaining warm gap than its first-use gap, while its
compilation takes about half the reference time. This points the next
short-report investigation toward execution. The comparison preserves
the shared host budget on Lython. All 112 batches, both quiet gates,
joined cleanup and archive verification pass.
[Matched JSON change](../2026-11-24-json-encoding-flags/README.md) ·
[Earlier short report](../2026-11-23-combined-cased-split-cpython/README.md).

[Source and image](identity.json) · [Timing](summary.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
