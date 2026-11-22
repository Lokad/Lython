# B1325 Compile fixed CSV unpack separately

The compiler marks iteration when a fixed unpack immediately consumes
its result. Only that instruction checks for an exact file-backed
`csv.reader`. The reader writes into existing private operands under one
field admission, bypassing the row list, its array and the temporary
reservation object. Ordinary iteration keeps acquiring owned list rows.
Both routes share the same field parsing and value construction. The
compiler still emits ordinary unpack for other iterator types.

Compared `8c81fb8d2` and `d17c1c42e` on November 22, 2026 UTC on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two independent
processes per revision warmed for 12 seconds each and supplied seven
rotated batches. Ranges cover all process-median pairings. Negative
changes favor the candidate. Warm elapsed includes mediated RAM-backed
I/O, output verification and result cleanup. Process CPU includes the
wider request. Startup, compilation and final teardown are excluded.

| Complete script | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Plain integer invoice control | Warm async | −1.66 to −0.40% | −1.94 to −0.98% | 0.21 / 1.06% |
| Plain integer invoice control | Warm | −0.31 to +2.89% | −1.52 to +3.85% | 0.31 / 2.89% |
| CSV meter report with explicit float | Warm | −5.81 to −2.63% | −4.81 to −3.10% | 1.15 / 2.21% |
| Typed ASCII CSV meter report | Warm | −7.04 to −4.95% | −5.89 to −4.88% | 1.84 / 0.39% |

Retain the compiled selection. Both meter reports use less CPU, and
the earlier async invoice regression does not recur. Synchronous invoice
elapsed and CPU span zero, with a 2.89% candidate elapsed spread. The
inspection of six scripts verifies identical instruction and call metadata
for the plain invoice and starred-unpack control. Each meter report has one
selected loop. Nested unpack retains the ordinary route for other iterators.

Local CLR allocation falls 17.92% across the two typed reports and 13.28%
in explicit-float. Each uses 589,824 fewer accounted bytes at peak. The
plain invoice is effectively flat and all allowances return to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 556 white-box and 1,070
public focused Release checks. Both 32-file worker images match CPython
goldens and all 112 timed batches verify. Cleanup joins workers, removes
fixtures and verifies the persistent archive under the quiet lease.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Original shared-pull variant](../2026-11-22-csv-direct-slots-shared-pull/README.md).
