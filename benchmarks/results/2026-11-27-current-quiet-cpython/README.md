# B1896 Current-source CPython comparison on the quiet VM

On November 27, 2026 UTC, the Azure F4as_v7 ran Ubuntu 24.04.4, .NET 11
RC3 and stock CPython 3.13.16. The complete 32-file Lython image uses core
`36ed51e1b`. This is a bounded diagnostic of four scripts. Two workers
per engine warm independently for at least five seconds and contribute
seven rotated batches per lane. Ranges compare every pairing of worker
medians. Ratios divide Lython by CPython, so above 1× means Lython takes
longer or uses more CPU.

| Script and phase | Fixture | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Iterable-row `starmap` shipment, warm `RunAsync` | 2,048 rows / 27,350 B | 2.63059–2.67883 ms | 1.95137–2.00121 ms | 1.315–1.373× | 1.315–1.374× |
| Nested JSON document summary, warm | 2,048 rows / 103,331 B | 0.74922–0.76740 ms | 0.67741–0.68762 ms | 1.090–1.133× | 1.066–1.114× |
| File-fed CSV quantity map, warm | 2,048 rows / 27,350 B | 0.39860–0.40201 ms | 1.36269–1.36308 ms | 0.292–0.295× | 0.313–0.314× |
| Configuration edit, warm | 181 B input | 0.06942–0.06975 ms | 0.06181–0.06207 ms | 1.118–1.129× | 0.932–0.945× |
| Configuration edit, compile only | 357 B source | 0.01654–0.01668 ms | 0.03377–0.03403 ms | 0.486–0.494× | 0.494–0.501× |
| Configuration edit, compile-to-result | 357 B source / 181 B input | 0.09592–0.09717 ms | 0.12491–0.12615 ms | 0.760–0.778× | 0.724–0.737× |

Warm and compile-to-result elapsed include applicable RAM-backed file I/O,
golden checks and previous-result disposal. The configuration edit reads
JSON and rewrites it to a verified output file. Compilation alone uses
resident source and includes previous compiled-owner disposal where
applicable. Fixture reset and request framing are outside elapsed and
inside the wider process CPU clock. All lanes use an already-running
process. CPython runs the ordinary synchronous script in each case with
`-I -S -X utf8`. Lython uses mediated file I/O with accounting active.

All four independent goldens, 168 timed batches and both machine gates
pass. Worker median spread is at most 2.55% within the same engine and
lane. The service joins the workers, removes the fixtures, then verifies
the persistent archive under the machine lease. The sampling takes
254 seconds including gates. The synchronous nested document results
are about 62–90 µs apart across workers. Its older noisy WSL results and
prior quiet result remain at their measured revisions. The file-fed map
provides a different CSV path over the shipment input. Its ratio is an
external comparison of that script.

## Local attribution on the same Lython image

Three six-second EventPipe windows on Ubuntu WSL pinned to CPU 27
verified 764 batches. The local samples are a separate attribution run.
The initial launcher resolved the SDK path to `/dotnet` and stopped
before sampling. The archived launcher resolves the SDK in WSL.

In the iterable shipment, generator advancement accounts for 63.5% of
sampled time with a resolved Lython frame after filtering identifiable
waits. Iterable row invocation accounts for 51.9%, argument expansion
34.8%, binding 4.5% and argument buffer allocation 1.7%. These inclusive
regions overlap, and advancement includes the work the generator was
written to do. The measured gap is about 0.63–0.73 ms per whole report.
This favors looking for a redundant stage on the generator execution
path over creating another buffer pool.

Compilation covers 29.7% of the configuration compile-to-result profile.
In the compile-only profile, the inclusive shares for emission, abstract
analysis and parsing are 30.1%, 29.1% and 23.2%. These shares help locate
compiler work. The timing comparison above supplies the absolute costs.

[Timing summary](summary.json) · [Image identity](identity.json) ·
[Staging](stage-custody.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Profile attribution](profile-attribution.json) ·
[Profile observations](profile-observations.tar.xz) ·
[Profile receipt](profile-archive-receipt.json).
