# B1881 Compare the current shipment scripts with CPython

The November 27 local diagnostic uses `ab883268b`, .NET 11 RC3 and
CPython 3.13.16 on one logical CPU under Ubuntu WSL on an Intel i7-14700KF.
The source is unchanged at inspected `70ab6c77e`. The executable, full
32-file Lython image and independent output and file goldens verify.
All three scripts read the same 2,048-record, 27,350-byte CSV fixture.

The wide report passes five-item lists to `starmap`. The iterable report
passes four values through a generator per row. The file-fed map reads
and totals the quantities. Two workers per engine warm for five seconds
per lane and contribute seven rotated batches. The shipment API lanes
share workers and rotate both engine and hosting mode. CPython runs the
same synchronous script in both lanes. Elapsed includes mediated RAM-backed
I/O, verification and prior-result disposal. Process CPU also includes
fixture reset and request framing. Compilation and startup are separate.

| Workload | Lython API | Lython | CPython | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Wide shipment | `Run` | 7.04538–7.47891 ms | 5.97461–6.17809 ms | 1.140–1.252× | 1.038–1.131× |
| Wide shipment | `RunAsync` | 6.59418–7.32776 ms | 5.96587–6.06126 ms | 1.088–1.228× | 1.061–1.243× |
| File-fed map | `RunAsync` | 1.10825–1.11422 ms | 3.09238–3.14538 ms | 0.352–0.360× | 0.371–0.374× |
| Iterable shipment | `Run` | 3.42958–3.66051 ms | 2.25860–2.34191 ms | 1.464–1.621× | 1.538–1.574× |
| Iterable shipment | `RunAsync` | 4.14039–4.19706 ms | 2.23555–2.25895 ms | 1.833–1.877× | 1.837–1.901× |

The ratios divide Lython by CPython and span the pairings of worker medians.
The large current opportunity is iterable rows. Both elapsed and CPU point
in the same direction, with a larger gap in async. Wide async Lython
elapsed has 11.12% same-image spread. Its two API ranges overlap. The
file-fed map serves as a control for moving CSV and file-host costs. The
earlier quiet runs keep their original machine and revision identities.

All 140 timed batches verify. Workers join, RAM fixtures are removed and
the persistent archive verifies under the lease.

[Timing](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Verification and procedures](procedures.tar.xz).
