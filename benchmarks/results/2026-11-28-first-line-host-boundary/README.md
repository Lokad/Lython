# B1983 Isolate the first-line comparison host costs

The quiet comparison keeps the `f231ce76a` Lython core, adapter, script
and file fixture fixed. Only the second worker's benchmark assembly
adds clocks around existing host operations, the public run and output
verification. The native-file host still checks real path components,
uses the borrowed allowance and closes the same handles. Both images
match the independent CPython output and file golden locally and on
the VM.

November 28, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds and contribute
seven rotated batches in each API. Fixture staging sits outside elapsed.
Elapsed includes public execution, file verification and previous-result
disposal. Process CPU also includes fixture reset and framing. Times are
microseconds per invocation and span the two instrumented worker medians.

| Timed phase | `Run` | `RunAsync` |
| --- | ---: | ---: |
| Open input | 8.72–8.85 µs | 9.27–9.31 µs |
| Open output | 11.72–11.87 µs | 11.79 µs |
| Path resolution within both opens | 8.81–8.90 µs | 9.29–9.40 µs |
| First read | 1.50–1.51 µs | 1.52–1.53 µs |
| Write | 2.06–2.10 µs | 2.10–2.13 µs |
| Flush | 0.02 µs | 0.02 µs |
| Close both handles | 2.65–2.67 µs | 2.61–2.65 µs |
| Total inside host methods during the run | 26.68–27.00 µs | 27.36 µs |
| Public run outside those host methods | 22.63–22.81 µs | 24.79–25.60 µs |
| Output file verification | 16.09–16.21 µs | 16.01–16.17 µs |
| Complete command | 65.63–66.28 µs | 68.52–69.40 µs |

Resolution is included in open. Each batch confirms one read, one
write, two opens and two closes per command. Medians for nested phases
are calculated independently. The observed elapsed change from adding
the clocks spans −0.36% to +0.64% in `Run` and approximately 0.00% to
+2.29% in `RunAsync`. Observer process CPU spans zero in both APIs.

The phase breakdown applies to Lython. Interpret the external ratio
at its whole-command boundary, including CPython's native file work
and checks. The native-file boundary remains the external baseline.

The first collection stopped before timing because the manifest lacked
a purpose field. The retry used the same binaries and input. All 56
timed batches and both quiet gates verify. The workers join, the
RAM-backed fixtures are removed, and the persistent archive verifies.

[File phases](phases.json) · [Observer comparison](summary.json) ·
[Worker identities](identity.json) · [Observations](observations.tar.xz) ·
[Procedures and first attempt](procedures.tar.xz) · [Terminal status](terminal-status.json).
