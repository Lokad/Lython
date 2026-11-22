# B1361 Integer loops, factories and CSV against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image was built from
`eb227dcac`. The external baseline is stock
CPython 3.13.16. The range diagnostic sums 100,000 integers in a
Python function. The supplied helper is called 2,048 times. The
mixed factory creates 4,096 functions for 2,048 inputs. The ASCII
meter and quoted policy invoice each read 4,096 records, from
139,299 and 174,147 input bytes respectively.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Integer range sum | Warm | 1.7984–1.8129 ms | 1.7092–1.7093 ms | 1.052–1.061× | 0.80 / 0.004% | 0.61% |
| Fully supplied arithmetic helper | Warm | 0.3546–0.3587 ms | 0.2008–0.2117 ms | 1.675–1.787× | 1.17 / 5.457% | 0.67% |
| Typed ASCII CSV meter | Warm | 3.0344–3.0493 ms | 2.4877–2.5222 ms | 1.203–1.226× | 0.49 / 1.388% | 1.14% |
| Mixed literal-default factory | Compile to result | 1.031–1.122 ms | 0.622–0.637 ms | 1.618–1.805× | 8.88 / 2.432% | 0.73% |
| Quoted policy invoice | Warm | 7.2025–7.2303 ms | 6.2855–6.2984 ms | 1.144–1.150× | 0.39 / 0.206% | 1.42% |

The integer range diagnostic is 5.2–6.1% slower than CPython. The
supplied helper is 1.675–1.787×. Its CPython workers differ by 5.46%.
The mixed factory has a wider 8.88% Lython worker spread. Its current
range includes the entire previous 1.743–1.770× reading, so the
milestone comparison leaves the size of that change uncertain.

Ratios divide Lython elapsed by CPython elapsed and span all pairings
of two process medians per engine. Above one means Lython takes longer.
Two workers per engine and phase warmed for at least eight seconds
and supplied seven rotated timed batches each. Controls show the spread
of the two process medians for each engine.

Warm execution includes mediated RAM-backed input and output, captured
stdout, golden verification and prior-result disposal.
Time to result covers compilation and execution with their cleanup. Process startup and
final teardown have separate boundaries.

All 140 timed batches and both quiet gates passed in
264 seconds. Stdout and output files matched their CPython goldens. The 32-file Lython image and both CPython worker identities
verified. Every worker joined and the persistent archive was checked
before releasing the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Previous factory and CSV comparison](../2026-11-22-construction-csv-cpython/README.md) ·
[Signed kernel source pair](../2026-11-22-signed-integer-kernel/README.md) ·
[Image verification and procedures](procedures.tar.xz).
